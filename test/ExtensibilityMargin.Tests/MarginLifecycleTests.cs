using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ExtensibilityMargin;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;

namespace ExtensibilityMargin.Tests
{
    [TestClass]
    public class MarginLifecycleTests
    {
        [STATestMethod]
        public void HiddenMarginDoesNotAcquireClassifiersAndDisposesIdempotently()
        {
            var view = new Mock<IWpfTextView>();
            var caret = new Mock<ITextCaret>();
            var graph = new Mock<IBufferGraph>();
            view.SetupGet(v => v.Caret).Returns(caret.Object);
            view.SetupGet(v => v.BufferGraph).Returns(graph.Object);
            view.SetupGet(v => v.VisualElement).Returns(new Grid());
            var classifiers = new Mock<IViewClassifierAggregatorService>(MockBehavior.Strict);
            var buffers = new Mock<IClassifierAggregatorService>(MockBehavior.Strict);
            var tags = new Mock<IViewTagAggregatorFactoryService>(MockBehavior.Strict);
            var margin = new BottomMargin(view.Object, classifiers.Object, buffers.Object, tags.Object);
            try
            {
                Assert.IsFalse(margin.IsVisible);
                margin.Dispose();
                margin.Dispose();
                Assert.ThrowsExactly<ObjectDisposedException>(() => _ = margin.VisualElement);
                Assert.ThrowsExactly<ObjectDisposedException>(() => _ = margin.MarginSize);
                Assert.ThrowsExactly<ObjectDisposedException>(() => _ = margin.Enabled);
                view.VerifyRemove(v => v.Closed -= It.IsAny<EventHandler>(), Times.Once);
                caret.VerifyRemove(c => c.PositionChanged -= It.IsAny<EventHandler<CaretPositionChangedEventArgs>>(), Times.Once);
                graph.VerifyRemove(g => g.GraphBuffersChanged -= It.IsAny<EventHandler<GraphBuffersChangedEventArgs>>(), Times.Once);
                graph.VerifyRemove(g => g.GraphBufferContentTypeChanged -= It.IsAny<EventHandler<GraphBufferContentTypeChangedEventArgs>>(), Times.Once);
                classifiers.VerifyNoOtherCalls();
                buffers.VerifyNoOtherCalls();
                tags.VerifyNoOtherCalls();
            }
            finally
            {
                margin.Dispose();
            }
        }

        [STATestMethod]
        public void ClosingViewDisposesMarginWithoutCreatingEditorServices()
        {
            var view = new Mock<IWpfTextView>();
            view.SetupGet(v => v.Caret).Returns(Mock.Of<ITextCaret>());
            view.SetupGet(v => v.BufferGraph).Returns(Mock.Of<IBufferGraph>());
            view.SetupGet(v => v.VisualElement).Returns(new Grid());
            using var margin = new BottomMargin(view.Object,
                Mock.Of<IViewClassifierAggregatorService>(), Mock.Of<IClassifierAggregatorService>(), Mock.Of<IViewTagAggregatorFactoryService>());

            view.Raise(v => v.Closed += null, EventArgs.Empty);

            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = margin.VisualElement);
        }

        [STATestMethod]
        public void CopyConfirmationPreservesLatestDiagnosticValueAndAccessibleName()
        {
            var control = new TextControl("Classification", "keyword");
            try
            {
                control.ShowCopyConfirmation();
                Assert.AreEqual("Copied", ((TextBlock)control.Content).Text);
                Assert.AreEqual("Editor diagnostics copied", AutomationProperties.GetName(control));
                Assert.AreEqual("keyword", control.Value);

                control.Value = "class name";
                Assert.AreEqual("Copied", ((TextBlock)control.Content).Text);
                control.ResetCopyConfirmation();

                Assert.AreEqual("class name", ((TextBlock)control.Content).Text);
                Assert.AreEqual("Classification: class name", AutomationProperties.GetName(control));
            }
            finally
            {
                control.ResetCopyConfirmation();
            }
        }

        [STATestMethod]
        public void RepeatedCopyAndCleanupLeaveTheControlReadyForNormalUpdates()
        {
            var control = new TextControl("Content type", "CSharp");
            try
            {
                control.ShowCopyConfirmation();
                control.ShowCopyConfirmation();
                control.ResetCopyConfirmation();
                control.ResetCopyConfirmation();
                control.Value = "Razor";

                Assert.AreEqual("Razor", ((TextBlock)control.Content).Text);
                Assert.AreEqual("Content type: Razor", AutomationProperties.GetName(control));
            }
            finally
            {
                control.ResetCopyConfirmation();
            }
        }
    }
}
