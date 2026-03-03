using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace ExtensibilityMargin
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(BottomMargin.MarginName)]
    [Order(After = PredefinedMarginNames.BottomControl)]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Zoomable)]
    class BottomMarginFactory : IWpfTextViewMarginProvider
    {
        [Import]
        IViewClassifierAggregatorService _classifierService = null;

        [Import]
        IClassifierAggregatorService _bufferClassifierService = null;

        [Import]
        IViewTagAggregatorFactoryService _tagAggregatorFactory = null;

        [Import]
        public ITextDocumentFactoryService _documentService = null;

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            if (wpfTextViewHost == null || _classifierService == null || _bufferClassifierService == null || _tagAggregatorFactory == null || _documentService == null)
                return null;

            return new BottomMargin(wpfTextViewHost.TextView, _classifierService, _bufferClassifierService, _tagAggregatorFactory, _documentService);
        }
    }
}
