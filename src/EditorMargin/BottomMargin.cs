using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Tagging;
using Task = System.Threading.Tasks.Task;

namespace ExtensibilityMargin
{
    internal class BottomMargin : DockPanel, IWpfTextViewMargin
    {
        public const string MarginName = "Extensibility Margin";
        private readonly IWpfTextView _textView;
        private bool _isDisposed = false;
        private readonly IClassifierAggregatorService _bufferClassifierService;
        private readonly ITagAggregator<ClassificationTag> _classificationTagAggregator;
        private readonly IClassifier _viewClassifier;
        private readonly List<ITextBuffer> _candidateBuffers;
        private readonly TextControl _lblClassification, _lblEncoding, _lblContentType, _lblSelection, _lblRoles;
        private bool _updatingEncodingLabel, _updatingClassificationLabel, _updatingContentSelectionLabel, _updatingRolesLabel, _updatingTypeLabel;
        private CancellationTokenSource _caretUpdateCancellation;
        private ITextBuffer _lastContentTypeBuffer;
        private ITextSnapshot _lastClassificationSnapshot;
        private int _lastClassificationPosition = -1;
        private readonly ITextDocument _doc;

        public BottomMargin(IWpfTextView textView, IViewClassifierAggregatorService classifier, IClassifierAggregatorService bufferClassifierService, IViewTagAggregatorFactoryService tagAggregatorFactory, ITextDocumentFactoryService documentService)
        {
            _textView = textView;
            _bufferClassifierService = bufferClassifierService;
            _classificationTagAggregator = tagAggregatorFactory.CreateTagAggregator<ClassificationTag>(_textView);
            _viewClassifier = classifier.GetClassifier(_textView);
            _candidateBuffers = BuildCandidateBuffers();

            SetResourceReference(BackgroundProperty, EnvironmentColors.ScrollBarBackgroundBrushKey);

            ClipToBounds = true;

            _lblEncoding = new TextControl("Encoding");
            Children.Add(_lblEncoding);

            _lblContentType = new TextControl("Content type");
            Children.Add(_lblContentType);

            _lblClassification = new TextControl("Classification");
            Children.Add(_lblClassification);

            _lblSelection = new TextControl("Selection");
            Children.Add(_lblSelection);

            _lblRoles = new TextControl("Roles");
            Children.Add(_lblRoles);

            if (documentService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out _doc))
            {
                _doc.FileActionOccurred += FileChangedOnDisk;
            }

            textView.Caret.PositionChanged += CaretPositionChanged;
            _viewClassifier.ClassificationChanged += ViewClassifierOnClassificationChanged;
            MarginToggleCommand.Clicked += OnOptionsSaved;

            OnOptionsSaved(this, MarginToggleCommand.Enabled);
        }

        private void ViewClassifierOnClassificationChanged(object sender, ClassificationChangedEventArgs e)
        {
            if (!ShouldUpdate())
            {
                return;
            }

            UpdateClassificationLabelAsync(true).FileAndForget(nameof(BottomMargin));
        }

        private void OnOptionsSaved(object sender, bool enabled)
        {
            Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            if (!enabled)
            {
                CancelPendingCaretUpdate();
                return;
            }

            UpdateClassificationLabelAsync(true).FileAndForget(nameof(BottomMargin));
            UpdateContentTypeLabelAsync().FileAndForget(nameof(BottomMargin));
            UpdateContentSelectionLabelAsync().FileAndForget(nameof(BottomMargin));
            UpdateRolesLabelAsync().FileAndForget(nameof(BottomMargin));

            if (_doc != null)
            {
                UpdateEncodingLabelAsync(_doc).FileAndForget(nameof(BottomMargin));
            }
        }

        private void FileChangedOnDisk(object sender, TextDocumentFileActionEventArgs e)
        {
            if (!ShouldUpdate())
            {
                return;
            }

            UpdateEncodingLabelAsync(_doc).FileAndForget(nameof(BottomMargin));
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (!ShouldUpdate())
            {
                return;
            }

            ScheduleCaretUpdate();
        }

        private void ScheduleCaretUpdate()
        {
            CancelPendingCaretUpdate();

            _caretUpdateCancellation = new CancellationTokenSource();
            PerformCaretUpdateAsync(_caretUpdateCancellation.Token).FileAndForget(nameof(BottomMargin));
        }

        private async Task PerformCaretUpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(75, cancellationToken);

                if (cancellationToken.IsCancellationRequested || !ShouldUpdate())
                {
                    return;
                }

                UpdateClassificationLabelAsync().FileAndForget(nameof(BottomMargin));
                UpdateContentTypeLabelAsync().FileAndForget(nameof(BottomMargin));
                UpdateContentSelectionLabelAsync().FileAndForget(nameof(BottomMargin));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void CancelPendingCaretUpdate()
        {
            _caretUpdateCancellation?.Cancel();
            _caretUpdateCancellation?.Dispose();
            _caretUpdateCancellation = null;
        }

        private async Task UpdateEncodingLabelAsync(ITextDocument doc)
        {
            if (!ShouldUpdate() || _updatingEncodingLabel)
            {
                return;
            }

            _updatingEncodingLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (!ShouldUpdate())
                {
                    return;
                }

                var preamble = doc.Encoding.GetPreamble();
                var bom = preamble != null && preamble.Length > 2 ? " - BOM" : string.Empty;

                _lblEncoding.Value = doc.Encoding.EncodingName + bom;
                _lblEncoding.SetTooltip("Codepage:         " + doc.Encoding.CodePage + Environment.NewLine +
                                        "Windows codepage: " + doc.Encoding.CodePage + Environment.NewLine +
                                        "Header name:      " + doc.Encoding.HeaderName + Environment.NewLine +
                                        "Body name:        " + doc.Encoding.BodyName,
                                        true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
            finally
            {
                _updatingEncodingLabel = false;
            }
        }

        private async Task UpdateContentTypeLabelAsync()
        {
            if (!ShouldUpdate() || _updatingTypeLabel)
            {
                return;
            }

            _updatingTypeLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (!ShouldUpdate())
                {
                    return;
                }

                ITextBuffer buffer = GetTextBuffer(out SnapshotPoint? point);

                if (ReferenceEquals(buffer, _lastContentTypeBuffer))
                {
                    return;
                }

                _lastContentTypeBuffer = buffer;

                _lblContentType.Value = buffer.ContentType.TypeName;

                System.Collections.Generic.IEnumerable<string> typeNames = buffer.ContentType.BaseTypes.Select(t => t.DisplayName);

                if (typeNames.Any())
                {
                    _lblContentType.SetTooltip("base types: " + string.Join(", ", typeNames) + Environment.NewLine +
                                               "Snapshot: " + buffer.CurrentSnapshot.Version);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.Fail(ex.ToString());
            }
            finally
            {
                _updatingTypeLabel = false;
            }
        }

        private async Task UpdateClassificationLabelAsync(bool forceRefresh = false)
        {
            if (!ShouldUpdate() || _updatingClassificationLabel)
            {
                return;
            }

            _updatingClassificationLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                ITextSnapshot snapshot = _textView.TextSnapshot;

                if (!ShouldUpdate())
                {
                    return;
                }

                if (snapshot.Length == 0)
                {
                    _lblClassification.Value = "None";
                    _lblClassification.SetTooltip("None");
                    return;
                }

                int position = _textView.Caret.Position.BufferPosition.Position;

                if (position >= snapshot.Length)
                {
                    position--;
                }

                if (!forceRefresh && ReferenceEquals(snapshot, _lastClassificationSnapshot) && position == _lastClassificationPosition)
                {
                    return;
                }

                var span = new SnapshotSpan(snapshot, position, 1);
                System.Collections.Generic.IList<ClassificationSpan> cspans = _viewClassifier.GetClassificationSpans(span);

                if (cspans.Count == 0)
                {
                    SnapshotPoint caretPoint = _textView.Caret.Position.BufferPosition;

                    foreach (ITextBuffer candidateBuffer in _candidateBuffers)
                    {
                        SnapshotPoint? mappedPoint = _textView.BufferGraph.MapDownToBuffer(caretPoint, PointTrackingMode.Negative, candidateBuffer, PositionAffinity.Predecessor);

                        if (!mappedPoint.HasValue || candidateBuffer.CurrentSnapshot.Length == 0)
                        {
                            continue;
                        }

                        int mappedPosition = mappedPoint.Value.Position;

                        if (mappedPosition >= candidateBuffer.CurrentSnapshot.Length)
                        {
                            mappedPosition--;
                        }

                        var mappedSpan = new SnapshotSpan(candidateBuffer.CurrentSnapshot, mappedPosition, 1);
                        IClassifier bufferClassifier = _bufferClassifierService.GetClassifier(candidateBuffer);
                        cspans = bufferClassifier.GetClassificationSpans(mappedSpan);

                        if (cspans.Count > 0)
                        {
                            break;
                        }
                    }
                }

                if (cspans.Count == 0)
                {
                    var spans = new NormalizedSnapshotSpanCollection(span);
                    var tagSpan = _classificationTagAggregator.GetTags(spans).FirstOrDefault();

                    if (tagSpan != null)
                    {
                        cspans = new[] { new ClassificationSpan(span, tagSpan.Tag.ClassificationType) };
                    }
                }

                if (cspans.Count == 0)
                {
                    _lblClassification.Value = "None";
                    _lblClassification.SetTooltip("None");
                }
                else
                {
                    IClassificationType ctype = cspans[0].ClassificationType;
                    var name = ctype.Classification;

                    if (name.Contains(" - "))
                    {
                        var index = name.IndexOf(" - ", StringComparison.Ordinal);
                        name = name.Substring(0, index).Trim();
                    }

                    _lblClassification.SetTooltip(ctype.Classification);
                    _lblClassification.Value = name;
                }

                _lastClassificationSnapshot = snapshot;
                _lastClassificationPosition = position;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.Fail(ex.ToString());
            }
            finally
            {
                _updatingClassificationLabel = false;
            }
        }

        private async Task UpdateContentSelectionLabelAsync()
        {
            if (!ShouldUpdate() || _updatingContentSelectionLabel)
            {
                return;
            }

            _updatingContentSelectionLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (!ShouldUpdate())
                {
                    return;
                }

                var start = _textView.Selection.Start.Position.Position;
                var end = _textView.Selection.End.Position.Position;

                if (end == start)
                {
                    _lblSelection.Value = start.ToString();
                }
                else
                {
                    _lblSelection.Value = $"{start}-{end} ({end - start} chars)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.Fail(ex.ToString());
            }
            finally
            {
                _updatingContentSelectionLabel = false;
            }
        }

        private async Task UpdateRolesLabelAsync()
        {
            if (!ShouldUpdate() || _updatingRolesLabel)
            {
                return;
            }

            _updatingRolesLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (!ShouldUpdate())
                {
                    return;
                }

                if (_textView.Roles.Any())
                {
                    System.Collections.Generic.IEnumerable<string> roles = _textView.Roles.Select(r => r);
                    var content = string.Join(Environment.NewLine, roles);

                    _lblRoles.SetTooltip(content);
                    _lblRoles.Value = roles.Last();
                }
                else
                {
                    _lblRoles.Value = "n/a";
                    _lblRoles.ToolTip = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.Fail(ex.ToString());
            }
            finally
            {
                _updatingRolesLabel = false;
            }
        }

        private ITextBuffer GetTextBuffer(out SnapshotPoint? point)
        {
            if (_textView.IsClosed)
            {
                point = null;
                return _textView.TextBuffer;
            }

            SnapshotPoint caretPoint = _textView.Caret.Position.BufferPosition;

            ITextBuffer documentBuffer = _textView.TextDataModel.DocumentBuffer;
            point = _textView.BufferGraph.MapDownToBuffer(caretPoint, PointTrackingMode.Negative, documentBuffer, PositionAffinity.Predecessor);

            if (point.HasValue)
            {
                return documentBuffer;
            }

            if (_textView.TextBuffer is IProjectionBuffer projection)
            {
                foreach (ITextBuffer buffer in projection.SourceBuffers.Where(s => !s.ContentType.IsOfType("htmlx")))
                {
                    point = _textView.BufferGraph.MapDownToBuffer(caretPoint, PointTrackingMode.Negative, buffer, PositionAffinity.Predecessor);

                    if (point.HasValue)
                    {
                        return buffer;
                    }
                }
            }

            point = caretPoint;

            return _textView.TextBuffer;
        }

        private bool ShouldUpdate()
        {
            return !_isDisposed && !_textView.IsClosed && Visibility == Visibility.Visible;
        }

        private List<ITextBuffer> BuildCandidateBuffers()
        {
            var visited = new HashSet<ITextBuffer>();
            var buffers = new List<ITextBuffer>();

            foreach (ITextBuffer buffer in EnumerateCandidateBuffers(_textView.TextBuffer, visited))
            {
                buffers.Add(buffer);
            }

            return buffers;
        }

        private IEnumerable<ITextBuffer> EnumerateCandidateBuffers(ITextBuffer buffer, HashSet<ITextBuffer> visited)
        {
            if (!visited.Add(buffer))
            {
                yield break;
            }

            yield return buffer;

            if (buffer is IProjectionBuffer projection)
            {
                foreach (ITextBuffer sourceBuffer in projection.SourceBuffers)
                {
                    foreach (ITextBuffer nested in EnumerateCandidateBuffers(sourceBuffer, visited))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(MarginName);
            }
        }
        public FrameworkElement VisualElement
        {
            get
            {
                ThrowIfDisposed();
                return this;
            }
        }
        public double MarginSize
        {
            get
            {
                ThrowIfDisposed();
                return ActualHeight;
            }
        }

        public bool Enabled
        {
            // The margin should always be enabled
            get
            {
                ThrowIfDisposed();
                return true;
            }
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return (marginName == BottomMargin.MarginName) ? (IWpfTextViewMargin)this : null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                GC.SuppressFinalize(this);
                _isDisposed = true;

                if (_doc != null)
                {
                    _doc.FileActionOccurred -= FileChangedOnDisk;
                }

                _textView.Caret.PositionChanged -= CaretPositionChanged;
                _viewClassifier.ClassificationChanged -= ViewClassifierOnClassificationChanged;
                MarginToggleCommand.Clicked -= OnOptionsSaved;
                _classificationTagAggregator.Dispose();
                CancelPendingCaretUpdate();

            }
        }
    }
}
