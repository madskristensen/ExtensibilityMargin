using System;
using ExtensibilityMargin;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Utilities;
using Moq;

namespace ExtensibilityMargin.Tests
{
    [TestClass]
    public class ContentTypeSummaryTests
    {
        [TestMethod]
        public void SummaryGroupsUniqueBaseTypesAndRolesWithoutRedundantContext()
        {
            IContentType text = Type("text");
            IContentType code = Type("code", text);
            IContentType csharp = Type("CSharp", Type("Roslyn Languages", code), code);

            string summary = DiagnosticDetails.ContentTypeSummary(csharp, "CSharp", "CSharp",
                new[] { "ZOOMABLE", "DOCUMENT", "DOCUMENT" });

            string expected = string.Join(Environment.NewLine, new[]
            {
                "Content type: CSharp",
                "Identifies which editor extensions apply here.",
                "",
                "Also matches (base types)",
                "code, Roslyn Languages, text",
                "",
                "Text view roles",
                "Capabilities used by role-based editor extensions.",
                "DOCUMENT, ZOOMABLE",
            });
            Assert.AreEqual(expected, summary);
        }

        [TestMethod]
        public void ProjectionContextIsShownWhenTypesDiffer()
        {
            string summary = DiagnosticDetails.ContentTypeSummary(Type("CSharp"), "projection", "Razor", Array.Empty<string>());

            StringAssert.Contains(summary, "Buffer context" + Environment.NewLine + "View: projection" + Environment.NewLine + "Document: Razor");
            Assert.DoesNotContain("Also matches", summary);
            Assert.DoesNotContain("Text view roles", summary);
        }

        [TestMethod]
        public void CyclesDoNotRepeatTheSelectedTypeOrItsBases()
        {
            var root = new Mock<IContentType>();
            root.SetupGet(t => t.TypeName).Returns("CSharp");
            IContentType code = Type("code", root.Object);
            root.SetupGet(t => t.BaseTypes).Returns(new[] { code, code });

            string summary = DiagnosticDetails.ContentTypeSummary(root.Object, "csharp", "CSHARP", Array.Empty<string>());

            Assert.AreEqual("Content type: CSharp" + Environment.NewLine
                + "Identifies which editor extensions apply here." + Environment.NewLine + Environment.NewLine
                + "Also matches (base types)" + Environment.NewLine + "code", summary);
        }

        [TestMethod]
        public void FullReportStillPreservesInheritanceAndContext()
        {
            string report = DiagnosticDetails.ContentType(Type("CSharp", Type("code", Type("text"))),
                "CSharp", "CSharp", new[] { "DOCUMENT" });

            StringAssert.Contains(report, "CSharp" + Environment.NewLine + "  code" + Environment.NewLine + "    text");
            StringAssert.Contains(report, "View content type: CSharp");
            StringAssert.Contains(report, "Document content type: CSharp");
            StringAssert.Contains(report, "Text view roles:" + Environment.NewLine + "  DOCUMENT");
        }

        private static IContentType Type(string name, params IContentType[] bases)
        {
            var type = new Mock<IContentType>();
            type.SetupGet(t => t.TypeName).Returns(name);
            type.SetupGet(t => t.BaseTypes).Returns(bases);
            return type.Object;
        }
    }
}
