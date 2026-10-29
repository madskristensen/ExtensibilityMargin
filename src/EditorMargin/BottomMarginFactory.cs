using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace ExtensibilityMargin
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(BottomMargin.MarginName)]
    [Order(After = "BottomRightCornerSpacerMargin", Before = "LineColCharMargin")]
    [MarginContainer(PredefinedMarginNames.BottomRightCorner)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Zoomable)]
    class BottomMarginFactory : IWpfTextViewMarginProvider
    {
        [Import(typeof(SVsServiceProvider))]
        IServiceProvider _serviceProvider = null;

        [Import]
        IViewClassifierAggregatorService _classifierService = null;

        [Import]
        IClassifierAggregatorService _bufferClassifierService = null;

        [Import]
        IViewTagAggregatorFactoryService _tagAggregatorFactory = null;

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (wpfTextViewHost == null || _classifierService == null || _bufferClassifierService == null || _tagAggregatorFactory == null)
                return null;

            MarginToggleCommand.InitializeSettings(_serviceProvider);
            return new BottomMargin(wpfTextViewHost.TextView, _classifierService, _bufferClassifierService, _tagAggregatorFactory);
        }
    }
}
