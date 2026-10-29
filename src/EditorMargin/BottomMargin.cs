using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace ExtensibilityMargin
{
    internal sealed class BottomMargin : DockPanel, IWpfTextViewMargin
    {
        public const string MarginName = "Extensibility Margin";
        private readonly IWpfTextView _textView;
        private readonly IViewClassifierAggregatorService _viewClassifierService;
        private readonly IClassifierAggregatorService _bufferClassifierService;
        private readonly IViewTagAggregatorFactoryService _tagAggregatorFactory;
        private readonly Dictionary<ITextBuffer, IClassifier> _bufferClassifiers = new Dictionary<ITextBuffer, IClassifier>();
        private readonly TextControl _contentType, _classification;
        private readonly DispatcherTimer _updateTimer;
        private IClassifier _viewClassifier;
        private ITagAggregator<ClassificationTag> _tagAggregator;
        private IList<ITextBuffer> _candidateBuffers = Array.Empty<ITextBuffer>();
        private IList<IClassificationType> _classificationTypes = Array.Empty<IClassificationType>();
        private IContentType _lastContentType;
        private ITextSnapshot _lastSnapshot;
        private int _lastPosition = -1;
        private readonly ConcurrentQueue<ClassificationChangedEventArgs> _classificationChanges = new ConcurrentQueue<ClassificationChangedEventArgs>();
        private int _refreshQueued;
        private int _backgroundRefreshRequested;
        private bool _forceRefresh = true;
        private bool _buffersDirty = true;
        private bool _isDisposed;

        public BottomMargin(IWpfTextView textView, IViewClassifierAggregatorService classifier, IClassifierAggregatorService bufferClassifierService, IViewTagAggregatorFactoryService tagAggregatorFactory)
        {
            _textView = textView;
            _viewClassifierService = classifier;
            _bufferClassifierService = bufferClassifierService;
            _tagAggregatorFactory = tagAggregatorFactory;
            SetResourceReference(BackgroundProperty, EnvironmentColors.ScrollBarBackgroundBrushKey);
            ClipToBounds = true;
            LastChildFill = false;
            VerticalAlignment = VerticalAlignment.Center;

            _contentType = new TextControl("Content type");
            _classification = new TextControl("Classification");
            Children.Add(_contentType);
            Children.Add(_classification);
            _contentType.ToolTip = "Content type and text view roles";
            _classification.ToolTip = "Classifications at caret";
            _contentType.ToolTipOpening += OnContentTypeTooltipOpening;
            _classification.ToolTipOpening += OnClassificationTooltipOpening;
            _contentType.Click += CopyDiagnostics;
            _classification.Click += CopyDiagnostics;

            _updateTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(75),
            };
            _updateTimer.Tick += OnUpdateTick;
            textView.Caret.PositionChanged += OnCaretPositionChanged;
            textView.LayoutChanged += OnLayoutChanged;
            textView.BufferGraph.GraphBuffersChanged += OnGraphBuffersChanged;
            textView.BufferGraph.GraphBufferContentTypeChanged += OnGraphContentTypeChanged;
            textView.VisualElement.IsVisibleChanged += OnEditorVisibilityChanged;
            textView.Closed += OnViewClosed;
            IsVisibleChanged += OnEditorVisibilityChanged;
            MarginToggleCommand.Clicked += OnVisibilitySettingChanged;
            OnVisibilitySettingChanged(this, MarginToggleCommand.Enabled);
        }

        private bool ShouldUpdate => !_isDisposed && !_textView.IsClosed && MarginToggleCommand.Enabled && IsVisible && _textView.VisualElement.IsVisible;

        private void OnVisibilitySettingChanged(object sender, bool enabled)
        {
            Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            UpdateActivity();
        }

        private void OnEditorVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateActivity();

        private void UpdateActivity()
        {
            if (ShouldUpdate)
            {
                _forceRefresh = true;
                QueueRefresh();
            }
            else
            {
                _updateTimer.Stop();
                ReleaseClassifiers();
                _buffersDirty = true;
                _lastSnapshot = null;
            }
        }

        private void QueueRefresh(bool force = false)
        {
            if (!Dispatcher.CheckAccess())
            {
                Interlocked.Exchange(ref _backgroundRefreshRequested, 1);
                QueueUIRefresh();
                return;
            }
            if (!ShouldUpdate)
            {
                return;
            }
            _forceRefresh |= force;
            if (!_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
        }

        private void QueueUIRefresh()
        {
            if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
            {
                RefreshOnUIThreadAsync().FileAndForget(nameof(BottomMargin));
            }
        }

        private async Task RefreshOnUIThreadAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Interlocked.Exchange(ref _refreshQueued, 0);
            int pendingChanges = _classificationChanges.Count;
            while (pendingChanges-- > 0 && _classificationChanges.TryDequeue(out ClassificationChangedEventArgs change))
            {
                OnClassificationChanged(this, change);
            }
            if (Interlocked.Exchange(ref _backgroundRefreshRequested, 0) != 0)
            {
                QueueRefresh(true);
            }
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e) => QueueRefresh();

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OldSnapshot, e.NewSnapshot))
            {
                QueueRefresh();
            }
        }

        private void OnClassificationChanged(object sender, ClassificationChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                _classificationChanges.Enqueue(e);
                QueueUIRefresh();
                return;
            }
            if (!ShouldUpdate)
            {
                return;
            }
            ITextBuffer changedBuffer = e.ChangeSpan.Snapshot.TextBuffer;
            SnapshotPoint? caret = _textView.BufferGraph.MapDownToBuffer(_textView.Caret.Position.BufferPosition,
                PointTrackingMode.Negative, changedBuffer, PositionAffinity.Predecessor);
            if (!caret.HasValue)
            {
                return;
            }
            ITextSnapshot snapshot = changedBuffer.CurrentSnapshot;
            SnapshotSpan changed = e.ChangeSpan.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);
            int position = Math.Max(0, Math.Min(caret.Value.Position, snapshot.Length - 1));
            if (changed.Contains(position) || (changed.Length == 0 && changed.Start.Position == position))
            {
                QueueRefresh(true);
            }
        }

        private void OnTagsChanged(object sender, TagsChangedEventArgs e) => QueueRefresh(true);

        private void OnGraphBuffersChanged(object sender, GraphBuffersChangedEventArgs e)
        {
            _buffersDirty = true;
            QueueRefresh(true);
        }

        private void OnGraphContentTypeChanged(object sender, GraphBufferContentTypeChangedEventArgs e)
        {
            _buffersDirty = true;
            QueueRefresh(true);
        }

        private void OnUpdateTick(object sender, EventArgs e)
        {
            _updateTimer.Stop();
            if (ShouldUpdate)
            {
                RefreshDiagnostics();
            }
        }

        private void RefreshDiagnostics()
        {
            try
            {
                if (_buffersDirty)
                {
                    ReleaseClassifiers();
                    _buffersDirty = false;
                    _candidateBuffers = _textView.BufferGraph.GetTextBuffers(_ => true);
                    _lastContentType = null;
                    _forceRefresh = true;
                }

                ITextBuffer buffer = GetContentBuffer();
                if (!ReferenceEquals(_lastContentType, buffer.ContentType))
                {
                    _contentType.Value = buffer.ContentType.TypeName;
                    _lastContentType = buffer.ContentType;
                }

                ITextSnapshot snapshot = _textView.TextSnapshot;
                int position = Math.Max(0, Math.Min(_textView.Caret.Position.BufferPosition.Position, snapshot.Length - 1));
                if (!_forceRefresh && ReferenceEquals(snapshot, _lastSnapshot) && position == _lastPosition)
                {
                    return;
                }
                _forceRefresh = false;
                if (snapshot.Length == 0)
                {
                    _classificationTypes = Array.Empty<IClassificationType>();
                }
                else
                {
                    if (_viewClassifier == null)
                    {
                        _viewClassifier = _viewClassifierService.GetClassifier(_textView);
                        _viewClassifier.ClassificationChanged += OnClassificationChanged;
                    }
                    _classificationTypes = GetClassifications(new SnapshotSpan(snapshot, position, 1));
                }
                _classification.Value = _classificationTypes.Count == 0 ? "None" : _classificationTypes[0].Classification;
                _lastSnapshot = snapshot;
                _lastPosition = position;
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(nameof(BottomMargin), ex.ToString());
                _lastSnapshot = null;
                _classificationTypes = Array.Empty<IClassificationType>();
                _classification.Value = "Error";
                _classification.SetTooltip("Unable to read editor diagnostics. See the Visual Studio Activity Log.");
            }
        }

        private IList<IClassificationType> GetClassifications(SnapshotSpan span)
        {
            IList<ClassificationSpan> spans = _viewClassifier.GetClassificationSpans(span);
            if (spans.Count == 0)
            {
                foreach (ITextBuffer candidate in _candidateBuffers)
                {
                    if (ReferenceEquals(candidate, _textView.TextBuffer))
                    {
                        continue;
                    }
                    SnapshotPoint? mapped = _textView.BufferGraph.MapDownToBuffer(span.Start, PointTrackingMode.Negative, candidate, PositionAffinity.Predecessor);
                    ITextSnapshot snapshot = candidate.CurrentSnapshot;
                    if (!mapped.HasValue || snapshot.Length == 0)
                    {
                        continue;
                    }
                    if (!_bufferClassifiers.TryGetValue(candidate, out IClassifier classifier))
                    {
                        classifier = _bufferClassifierService.GetClassifier(candidate);
                        classifier.ClassificationChanged += OnClassificationChanged;
                        _bufferClassifiers.Add(candidate, classifier);
                    }
                    int position = Math.Min(mapped.Value.Position, snapshot.Length - 1);
                    spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, position, 1));
                    if (spans.Count != 0)
                    {
                        break;
                    }
                }
            }
            if (spans.Count != 0)
            {
                return spans.Select(s => s.ClassificationType).Distinct().ToArray();
            }
            if (_tagAggregator == null)
            {
                _tagAggregator = _tagAggregatorFactory.CreateTagAggregator<ClassificationTag>(_textView);
                _tagAggregator.TagsChanged += OnTagsChanged;
            }
            return _tagAggregator.GetTags(new NormalizedSnapshotSpanCollection(span)).Select(t => t.Tag.ClassificationType).Distinct().ToArray();
        }

        private ITextBuffer GetContentBuffer()
        {
            SnapshotPoint caret = _textView.Caret.Position.BufferPosition;
            SnapshotPoint? mapped = _textView.BufferGraph.MapDownToFirstMatch(caret, PointTrackingMode.Negative,
                s => !(s.TextBuffer is IProjectionBuffer) && !s.ContentType.IsOfType("htmlx"), PositionAffinity.Predecessor);
            return mapped?.Snapshot.TextBuffer ?? _textView.TextDataModel.DocumentBuffer;
        }

        private string GetContentDetails() => DiagnosticDetails.ContentType(GetContentBuffer().ContentType,
            _textView.TextBuffer.ContentType.TypeName, _textView.TextDataModel.DocumentBuffer.ContentType.TypeName, _textView.Roles);

        private void OnContentTypeTooltipOpening(object sender, ToolTipEventArgs e)
        {
            if (ShouldUpdate)
            {
                try
                {
                    _contentType.SetTooltip(DiagnosticDetails.ContentTypeSummary(GetContentBuffer().ContentType,
                        _textView.TextBuffer.ContentType.TypeName, _textView.TextDataModel.DocumentBuffer.ContentType.TypeName, _textView.Roles)
                        + Environment.NewLine + Environment.NewLine + "Click to copy full diagnostics (no document text).");
                }
                catch (Exception ex)
                {
                    ActivityLog.LogError(nameof(BottomMargin), ex.ToString());
                    _contentType.SetTooltip("Unable to read content type details. See the Visual Studio Activity Log.");
                }
            }
        }

        private void OnClassificationTooltipOpening(object sender, ToolTipEventArgs e)
        {
            if (ShouldUpdate && _classification.Value != "Error")
            {
                try
                {
                    _classification.SetTooltip(DiagnosticDetails.Classifications(_classificationTypes) + Environment.NewLine + Environment.NewLine + "Click to copy editor diagnostics (no document text).");
                }
                catch (Exception ex)
                {
                    ActivityLog.LogError(nameof(BottomMargin), ex.ToString());
                    _classification.SetTooltip("Unable to read classification details. See the Visual Studio Activity Log.");
                }
            }
        }

        private void CopyDiagnostics(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!ShouldUpdate)
            {
                return;
            }
            _forceRefresh = true;
            RefreshDiagnostics();
            if (_classification.Value == "Error")
            {
                ShowCopyError("Unable to read editor diagnostics. See the Visual Studio Activity Log for details.");
                return;
            }
            try
            {
                Clipboard.SetText(GetContentDetails() + Environment.NewLine + Environment.NewLine + DiagnosticDetails.Classifications(_classificationTypes));
            }
            catch (ExternalException ex)
            {
                ActivityLog.LogError(nameof(BottomMargin), ex.ToString());
                ShowCopyError("Unable to copy diagnostics. The clipboard may be in use by another application.");
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(nameof(BottomMargin), ex.ToString());
                ShowCopyError("Unable to copy editor diagnostics. See the Visual Studio Activity Log for details.");
            }
        }

        private static void ShowCopyError(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider, message, Vsix.Name,
                Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_WARNING,
                Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK, Microsoft.VisualStudio.Shell.Interop.OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void ReleaseClassifiers()
        {
            if (_viewClassifier != null)
            {
                _viewClassifier.ClassificationChanged -= OnClassificationChanged;
                (_viewClassifier as IDisposable)?.Dispose();
                _viewClassifier = null;
            }
            if (_tagAggregator != null)
            {
                _tagAggregator.TagsChanged -= OnTagsChanged;
                _tagAggregator.Dispose();
                _tagAggregator = null;
            }
            foreach (IClassifier classifier in _bufferClassifiers.Values)
            {
                classifier.ClassificationChanged -= OnClassificationChanged;
                (classifier as IDisposable)?.Dispose();
            }
            _bufferClassifiers.Clear();
        }

        private void OnViewClosed(object sender, EventArgs e) => Dispose();

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(MarginName);
            }
        }

        public FrameworkElement VisualElement { get { ThrowIfDisposed(); return this; } }
        public double MarginSize { get { ThrowIfDisposed(); return ActualWidth; } }
        public bool Enabled { get { ThrowIfDisposed(); return MarginToggleCommand.Enabled; } }

        public ITextViewMargin GetTextViewMargin(string marginName) => string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            _updateTimer.Stop();
            _updateTimer.Tick -= OnUpdateTick;
            ReleaseClassifiers();
            _candidateBuffers = Array.Empty<ITextBuffer>();
            _classificationTypes = Array.Empty<IClassificationType>();
            _textView.Caret.PositionChanged -= OnCaretPositionChanged;
            _textView.LayoutChanged -= OnLayoutChanged;
            _textView.BufferGraph.GraphBuffersChanged -= OnGraphBuffersChanged;
            _textView.BufferGraph.GraphBufferContentTypeChanged -= OnGraphContentTypeChanged;
            _textView.VisualElement.IsVisibleChanged -= OnEditorVisibilityChanged;
            _textView.Closed -= OnViewClosed;
            IsVisibleChanged -= OnEditorVisibilityChanged;
            MarginToggleCommand.Clicked -= OnVisibilitySettingChanged;
            _contentType.ToolTipOpening -= OnContentTypeTooltipOpening;
            _classification.ToolTipOpening -= OnClassificationTooltipOpening;
            _contentType.Click -= CopyDiagnostics;
            _classification.Click -= CopyDiagnostics;
            GC.SuppressFinalize(this);
        }
    }
}
