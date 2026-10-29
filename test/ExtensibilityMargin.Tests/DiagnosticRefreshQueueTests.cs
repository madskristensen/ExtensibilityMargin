using System.Threading;
using System.Threading.Tasks;
using ExtensibilityMargin;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Moq;

namespace ExtensibilityMargin.Tests
{
    [TestClass]
    public class DiagnosticRefreshQueueTests
    {
        [TestMethod]
        public void InitiallyInactiveQueueDoesNotRetainEditorNotifications()
        {
            using var queue = new DiagnosticRefreshQueue();
            Assert.IsFalse(queue.Request(classification: Span(1)));
            Assert.IsFalse(queue.Request(tags: Mock.Of<IMappingSpan>()));
            DiagnosticRefreshQueue.Batch batch = queue.Drain();
            Assert.IsEmpty(batch.Classifications);
            Assert.IsEmpty(batch.Tags);
            Assert.IsFalse(batch.Force);
            queue.SetActive(true);
            Assert.IsTrue(queue.Request(force: true));
        }

        [TestMethod]
        public void NormalBurstSchedulesOneCallbackAndPreservesSpans()
        {
            using var queue = ActiveQueue();
            SnapshotSpan first = Span(1);
            SnapshotSpan second = Span(4);
            Assert.IsTrue(queue.Request(classification: first));
            Assert.IsFalse(queue.Request(classification: second));

            DiagnosticRefreshQueue.Batch batch = queue.Drain();

            CollectionAssert.AreEqual(new[] { first, second }, batch.Classifications);
            Assert.IsFalse(batch.Force);
            Assert.IsFalse(batch.BuffersDirty);
            Assert.IsTrue(queue.Request(classification: first), "Draining must allow the next callback.");
        }

        [TestMethod]
        public void ExactCapacityRetainsSpansButOverflowBecomesOneFullRefresh()
        {
            using var queue = ActiveQueue();
            SnapshotSpan span = Span(1);
            for (int i = 0; i < DiagnosticRefreshQueue.Capacity; i++)
            {
                Assert.AreEqual(i == 0, queue.Request(classification: span));
            }
            DiagnosticRefreshQueue.Batch atCapacity = queue.Drain();
            Assert.HasCount(DiagnosticRefreshQueue.Capacity, atCapacity.Classifications);
            Assert.IsFalse(atCapacity.Force);

            int callbacks = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (queue.Request(classification: span)) callbacks++;
            }
            DiagnosticRefreshQueue.Batch overflow = queue.Drain();
            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(overflow.Force);
            Assert.IsEmpty(overflow.Classifications);
            Assert.IsEmpty(overflow.Tags);
        }

        [TestMethod]
        public void ConcurrentPublishersStillScheduleOnlyOneCallback()
        {
            using var queue = ActiveQueue();
            var tag = Mock.Of<IMappingSpan>();
            int callbacks = 0;
            Parallel.For(0, 10000, _ =>
            {
                if (queue.Request(tags: tag)) Interlocked.Increment(ref callbacks);
            });

            DiagnosticRefreshQueue.Batch batch = queue.Drain();
            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(batch.Force);
            Assert.IsEmpty(batch.Tags);
        }

        [TestMethod]
        public void MixedClassificationAndTagChangesShareTheSameBound()
        {
            using var queue = ActiveQueue();
            SnapshotSpan span = Span(1);
            var tag = Mock.Of<IMappingSpan>();
            for (int i = 0; i <= DiagnosticRefreshQueue.Capacity / 2; i++)
            {
                queue.Request(classification: span);
                queue.Request(tags: tag);
            }
            DiagnosticRefreshQueue.Batch batch = queue.Drain();
            Assert.IsTrue(batch.Force);
            Assert.IsEmpty(batch.Classifications);
            Assert.IsEmpty(batch.Tags);
        }

        [TestMethod]
        public void HiddenQueueDiscardsSnapshotsAndRejectsLateNotifications()
        {
            using var queue = ActiveQueue();
            queue.Request(classification: Span(1));
            queue.SetActive(false);
            Assert.IsFalse(queue.Request(force: true, buffersDirty: true));

            DiagnosticRefreshQueue.Batch batch = queue.Drain();
            Assert.IsEmpty(batch.Classifications);
            Assert.IsFalse(batch.Force);
            Assert.IsFalse(batch.BuffersDirty);

            queue.SetActive(true);
            Assert.IsTrue(queue.Request(force: true));
            Assert.IsTrue(queue.Drain().Force);
        }

        [TestMethod]
        public void ReactivatingBeforePostedCallbackDoesNotPostADuplicate()
        {
            using var queue = ActiveQueue();
            Assert.IsTrue(queue.Request(classification: Span(1)));
            queue.SetActive(false);
            queue.SetActive(true);
            Assert.IsFalse(queue.Request(force: true));
            Assert.IsTrue(queue.Drain().Force);
            Assert.IsTrue(queue.Request(force: true));
        }

        [TestMethod]
        public void DisposeClearsWorkAndCannotBeReactivated()
        {
            var queue = ActiveQueue();
            queue.Request(classification: Span(1));
            queue.Dispose();
            queue.Dispose();
            queue.SetActive(true);
            Assert.IsFalse(queue.Request(force: true));
            DiagnosticRefreshQueue.Batch batch = queue.Drain();
            Assert.IsEmpty(batch.Classifications);
            Assert.IsFalse(batch.Force);
        }

        [TestMethod]
        public void ProjectionInvalidationSurvivesCoalescingAndResetsAfterDrain()
        {
            using var queue = ActiveQueue();
            queue.Request(classification: Span(1));
            Assert.IsFalse(queue.Request(force: true, buffersDirty: true));
            queue.Request(tags: Mock.Of<IMappingSpan>());
            DiagnosticRefreshQueue.Batch batch = queue.Drain();

            Assert.IsTrue(batch.BuffersDirty);
            Assert.IsTrue(batch.Force);
            Assert.IsEmpty(batch.Classifications);
            Assert.IsEmpty(batch.Tags);
            Assert.IsFalse(queue.Drain().BuffersDirty);
        }

        private static DiagnosticRefreshQueue ActiveQueue()
        {
            var queue = new DiagnosticRefreshQueue();
            queue.SetActive(true);
            return queue;
        }

        private static SnapshotSpan Span(int start)
        {
            var snapshot = new Mock<ITextSnapshot>();
            snapshot.SetupGet(s => s.Length).Returns(20);
            return new SnapshotSpan(snapshot.Object, start, 1);
        }
    }
}
