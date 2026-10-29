using System;
using ExtensibilityMargin;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ExtensibilityMargin.Tests
{
    [TestClass]
    public class MarginVisibilitySettingsTests
    {
        [TestMethod]
        public void NewProfileStartsDisabledWithoutReadingMissingCollection()
        {
            var store = new Mock<WritableSettingsStore>(MockBehavior.Strict);
            store.Setup(s => s.CollectionExists(MarginVisibilitySettings.Collection)).Returns(false);

            Assert.IsFalse(new MarginVisibilitySettings(store.Object).Load());
            store.VerifyAll();
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ExistingProfileRestoresVisibility(bool enabled)
        {
            var store = new Mock<WritableSettingsStore>(MockBehavior.Strict);
            store.Setup(s => s.CollectionExists(MarginVisibilitySettings.Collection)).Returns(true);
            store.Setup(s => s.GetBoolean(MarginVisibilitySettings.Collection, MarginVisibilitySettings.Property, false)).Returns(enabled);

            Assert.AreEqual(enabled, new MarginVisibilitySettings(store.Object).Load());
            store.VerifyAll();
        }

        [TestMethod]
        public void FirstToggleCreatesCollectionBeforeSaving()
        {
            var store = new Mock<WritableSettingsStore>(MockBehavior.Strict);
            var sequence = new MockSequence();
            store.InSequence(sequence).Setup(s => s.CollectionExists(MarginVisibilitySettings.Collection)).Returns(false);
            store.InSequence(sequence).Setup(s => s.CreateCollection(MarginVisibilitySettings.Collection));
            store.InSequence(sequence).Setup(s => s.SetBoolean(MarginVisibilitySettings.Collection, MarginVisibilitySettings.Property, true));

            new MarginVisibilitySettings(store.Object).Save(true);
            store.VerifyAll();
        }

        [TestMethod]
        public void SubsequentToggleUpdatesExistingCollection()
        {
            var store = new Mock<WritableSettingsStore>(MockBehavior.Strict);
            store.Setup(s => s.CollectionExists(MarginVisibilitySettings.Collection)).Returns(true);
            store.Setup(s => s.SetBoolean(MarginVisibilitySettings.Collection, MarginVisibilitySettings.Property, false));

            new MarginVisibilitySettings(store.Object).Save(false);
            store.VerifyAll();
        }

        [TestMethod]
        public void SaveFailureIsNotReportedAsSuccess()
        {
            var failure = new InvalidOperationException("Settings store unavailable");
            var store = new Mock<WritableSettingsStore>(MockBehavior.Strict);
            store.Setup(s => s.CollectionExists(MarginVisibilitySettings.Collection)).Returns(true);
            store.Setup(s => s.SetBoolean(MarginVisibilitySettings.Collection, MarginVisibilitySettings.Property, true)).Throws(failure);

            InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => new MarginVisibilitySettings(store.Object).Save(true));

            Assert.AreSame(failure, actual);
        }
    }
}
