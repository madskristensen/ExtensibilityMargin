using ExtensibilityMargin;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Moq;

namespace ExtensibilityMargin.Tests
{
    [TestClass]
    public class DiagnosticChangeFilterTests
    {
        [TestMethod]
        [DataRow(2, 4, 2, true)]
        [DataRow(2, 4, 5, true)]
        [DataRow(2, 4, 6, false)]
        [DataRow(2, 4, 1, false)]
        [DataRow(4, 0, 4, true)]
        [DataRow(4, 0, 5, false)]
        [DataRow(19, 1, 20, true)]
        public void SpanFilteringMatchesTheCharacterQueriedAtCaret(int start, int length, int caret, bool expected)
        {
            Assert.AreEqual(expected, DiagnosticChangeFilter.ContainsCaret(new SnapshotSpan(Snapshot(20), start, length), caret));
        }

        [TestMethod]
        public void ProjectionTagChangeIsMappedIntoTheCurrentViewSnapshot()
        {
            ITextSnapshot snapshot = Snapshot(20);
            var mapping = new Mock<IMappingSpan>(MockBehavior.Strict);
            mapping.Setup(m => m.GetSpans(snapshot)).Returns(new NormalizedSnapshotSpanCollection(new[]
            {
                new SnapshotSpan(snapshot, 1, 2),
                new SnapshotSpan(snapshot, 10, 4),
            }));

            Assert.IsTrue(DiagnosticChangeFilter.ContainsCaret(mapping.Object, snapshot, 11));
            Assert.IsFalse(DiagnosticChangeFilter.ContainsCaret(mapping.Object, snapshot, 7));
            mapping.Verify(m => m.GetSpans(snapshot), Times.Exactly(2));
        }

        [TestMethod]
        public void UnmappedProjectionTagChangeIsIgnored()
        {
            ITextSnapshot snapshot = Snapshot(20);
            var mapping = new Mock<IMappingSpan>(MockBehavior.Strict);
            mapping.Setup(m => m.GetSpans(snapshot)).Returns(new NormalizedSnapshotSpanCollection());

            Assert.IsFalse(DiagnosticChangeFilter.ContainsCaret(mapping.Object, snapshot, 5));
        }

        [TestMethod]
        public void EmptyDocumentAcceptsZeroLengthInvalidationAtCaret()
        {
            ITextSnapshot snapshot = Snapshot(0);
            Assert.IsTrue(DiagnosticChangeFilter.ContainsCaret(new SnapshotSpan(snapshot, 0, 0), 0));
        }

        private static ITextSnapshot Snapshot(int length)
        {
            var snapshot = new Mock<ITextSnapshot>();
            snapshot.SetupGet(s => s.Length).Returns(length);
            return snapshot.Object;
        }
    }
}
