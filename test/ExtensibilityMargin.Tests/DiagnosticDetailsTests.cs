using System;
using ExtensibilityMargin;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ExtensibilityMargin.Tests
{
    [TestClass]
    public class DiagnosticDetailsTests
    {
        [TestMethod]
        public void ContentTypeIncludesFullInheritanceAndSortedRoles()
        {
            IContentType text = ContentType("text");
            IContentType code = ContentType("code", text);
            string details = DiagnosticDetails.ContentType(ContentType("CSharp", code), "projection", "Razor", new[] { "ZOOMABLE", "DOCUMENT" });

            StringAssert.Contains(details, "Content type: CSharp" + Environment.NewLine + "  code" + Environment.NewLine + "    text");
            StringAssert.Contains(details, "View content type: projection");
            StringAssert.Contains(details, "Document content type: Razor");
            StringAssert.Contains(details, "Text view roles:" + Environment.NewLine + "  DOCUMENT" + Environment.NewLine + "  ZOOMABLE");
        }

        [TestMethod]
        public void ContentTypeSortsSiblingBaseTypes()
        {
            string details = DiagnosticDetails.ContentType(ContentType("custom", ContentType("z"), ContentType("a")), "custom", "custom", Array.Empty<string>());
            StringAssert.Contains(details, "custom" + Environment.NewLine + "  a" + Environment.NewLine + "  z");
        }

        [TestMethod]
        public void ContentTypeHandlesCyclicInheritance()
        {
            var type = new Mock<IContentType>();
            type.SetupGet(t => t.TypeName).Returns("cyclic");
            type.SetupGet(t => t.BaseTypes).Returns(new[] { type.Object });

            string details = DiagnosticDetails.ContentType(type.Object, "cyclic", "cyclic", Array.Empty<string>());

            StringAssert.Contains(details, "  cyclic (cycle)");
        }

        [TestMethod]
        public void ContentTypeDoesNotCarryDetailsBetweenBuffers()
        {
            DiagnosticDetails.ContentType(ContentType("CSharp", ContentType("code")), "CSharp", "CSharp", new[] { "OLD_ROLE" });
            string details = DiagnosticDetails.ContentType(ContentType("plaintext"), "plaintext", "plaintext", new[] { "DOCUMENT" });

            Assert.DoesNotContain("CSharp", details);
            Assert.DoesNotContain("OLD_ROLE", details);
            StringAssert.Contains(details, "Content type: plaintext");
        }

        [TestMethod]
        public void EmptyClassificationIsExplicit()
        {
            Assert.AreEqual("Classifications at caret:" + Environment.NewLine + "  None", DiagnosticDetails.Classifications(Array.Empty<IClassificationType>()));
        }

        [TestMethod]
        public void ClassificationsIncludeOverlapsAndInheritance()
        {
            IClassificationType identifier = Classification("identifier");
            string details = DiagnosticDetails.Classifications(new[]
            {
                Classification("class name", identifier),
                Classification("static symbol", Classification("symbol")),
            });

            StringAssert.Contains(details, "  class name" + Environment.NewLine + "    identifier");
            StringAssert.Contains(details, "  static symbol" + Environment.NewLine + "    symbol");
        }

        [TestMethod]
        public void ClassificationsDeduplicateNamesWithoutTruncatingThem()
        {
            const string name = "custom classification - important suffix";
            string details = DiagnosticDetails.Classifications(new[] { Classification(name), Classification(name) });

            Assert.AreEqual("Classifications at caret:" + Environment.NewLine + "  " + name, details);
        }

        [TestMethod]
        public void ClassificationHierarchyHandlesDiamondAndCycles()
        {
            var cycle = new Mock<IClassificationType>();
            cycle.SetupGet(t => t.Classification).Returns("cycle");
            cycle.SetupGet(t => t.BaseTypes).Returns(new[] { cycle.Object });
            IClassificationType shared = Classification("shared");
            string details = DiagnosticDetails.Classifications(new[]
            {
                Classification("root", Classification("left", shared), Classification("right", shared)),
                cycle.Object,
            });

            StringAssert.Contains(details, "    left" + Environment.NewLine + "      shared");
            StringAssert.Contains(details, "    right" + Environment.NewLine + "      shared");
            StringAssert.Contains(details, "    cycle (cycle)");
        }

        private static IContentType ContentType(string name, params IContentType[] bases)
        {
            var type = new Mock<IContentType>();
            type.SetupGet(t => t.TypeName).Returns(name);
            type.SetupGet(t => t.BaseTypes).Returns(bases);
            return type.Object;
        }

        private static IClassificationType Classification(string name, params IClassificationType[] bases)
        {
            var type = new Mock<IClassificationType>();
            type.SetupGet(t => t.Classification).Returns(name);
            type.SetupGet(t => t.BaseTypes).Returns(bases);
            return type.Object;
        }
    }
}
