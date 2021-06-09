using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Task = System.Threading.Tasks.Task;

namespace ExtensibilityMargin
{
    internal class BottomMargin : DockPanel, IWpfTextViewMargin
    {
        public const string MarginName = "Extensibility Margin";
        private readonly IWpfTextView _textView;
        private bool _isDisposed = false;
        private readonly IClassifier _classifier;
        private readonly TextControl _lblClassification, _lblEncoding, _lblContentType, _lblSelection, _lblRoles;
        private bool _updatingEncodingLabel, _updatingClassificationLabel, _updatingContentSelectionLabel, _updatingRolesLabel, _updatingTypeLabel;
        private readonly ITextDocument _doc;

        public BottomMargin(IWpfTextView textView, IClassifierAggregatorService classifier, ITextDocumentFactoryService documentService)
        {
            OnOptionsSaved(null, MarginToggleCommand.Enabled);

            _textView = textView;
            _classifier = classifier.GetClassifier(textView.TextBuffer);

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

            UpdateClassificationLabelAsync().FileAndForget(nameof(BottomMargin));
            UpdateContentTypeLabelAsync().FileAndForget(nameof(BottomMargin));
            UpdateContentSelectionLabelAsync().FileAndForget(nameof(BottomMargin));
            UpdateRolesLabelAsync().FileAndForget(nameof(BottomMargin));

            if (documentService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out _doc))
            {
                _doc.FileActionOccurred += FileChangedOnDisk;
                UpdateEncodingLabelAsync(_doc).FileAndForget(nameof(BottomMargin));
            }

            textView.Caret.PositionChanged += CaretPositionChanged;
            MarginToggleCommand.Clicked += OnOptionsSaved;
        }

        private void OnOptionsSaved(object sender, bool enabled)
        {
            Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FileChangedOnDisk(object sender, TextDocumentFileActionEventArgs e)
        {
            UpdateEncodingLabelAsync(_doc).FileAndForget(nameof(BottomMargin));
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            UpdateClassificationLabelAsync().FileAndForget(nameof(BottomMargin));
            UpdateContentTypeLabelAsync().FileAndForget(nameof(BottomMargin));
            UpdateContentSelectionLabelAsync().FileAndForget(nameof(BottomMargin));
        }

        private async Task UpdateEncodingLabelAsync(ITextDocument doc)
        {
            if (_updatingEncodingLabel)
            {
                return;
            }

            _updatingEncodingLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
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

            _updatingEncodingLabel = false;
        }

        private async Task UpdateContentTypeLabelAsync()
        {
            if (_updatingTypeLabel)
            {
                return;
            }

            _updatingTypeLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            ITextBuffer buffer = GetTextBuffer(out SnapshotPoint? point);

            _lblContentType.Value = buffer.ContentType.TypeName;

            System.Collections.Generic.IEnumerable<string> typeNames = buffer.ContentType.BaseTypes.Select(t => t.DisplayName);

            if (typeNames.Any())
            {
                _lblContentType.SetTooltip("base types: " + string.Join(", ", typeNames) + Environment.NewLine +
                                           "Snapshot: " + buffer.CurrentSnapshot.Version);
            }

            _updatingTypeLabel = false;
        }

        private async Task UpdateClassificationLabelAsync()
        {
            if (_updatingClassificationLabel)
            {
                return;
            }

            _updatingClassificationLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (_textView.TextBuffer.CurrentSnapshot.Length <= 1)
                {
                    return;
                }

                ITextBuffer buffer = GetTextBuffer(out SnapshotPoint? point);
                var position = point.Value.Position;

                if (position == buffer.CurrentSnapshot.Length)
                {
                    position--;
                }

                var span = new SnapshotSpan(buffer.CurrentSnapshot, position, 1);
                System.Collections.Generic.IList<ClassificationSpan> cspans = _classifier.GetClassificationSpans(span);

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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.Fail(ex.ToString());
            }

            _updatingClassificationLabel = false;
        }

        private async Task UpdateContentSelectionLabelAsync()
        {
            if (_updatingContentSelectionLabel)
            {
                return;
            }

            _updatingContentSelectionLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
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

            _updatingContentSelectionLabel = false;
        }

        private async Task UpdateRolesLabelAsync()
        {
            if (_updatingRolesLabel)
            {
                return;
            }

            _updatingRolesLabel = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
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

            _updatingRolesLabel = false;
        }

        private ITextBuffer GetTextBuffer(out SnapshotPoint? point)
        {
            if (_textView.TextBuffer is IProjectionBuffer projection)
            {
                SnapshotPoint snapshotPoint = _textView.Caret.Position.BufferPosition;

                foreach (ITextBuffer buffer in projection.SourceBuffers.Where(s => !s.ContentType.IsOfType("htmlx")))
                {
                    point = _textView.BufferGraph.MapDownToBuffer(snapshotPoint, PointTrackingMode.Negative, buffer, PositionAffinity.Predecessor);

                    if (point.HasValue)
                    {
                        return buffer;
                    }
                }
            }

            point = _textView.Caret.Position.BufferPosition;

            return _textView.TextBuffer;
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

                _doc.FileActionOccurred -= FileChangedOnDisk;
                _textView.Caret.PositionChanged -= CaretPositionChanged;
                MarginToggleCommand.Clicked -= OnOptionsSaved;

                (_classifier as IDisposable)?.Dispose();
            }
        }
    }
}
