using System.Linq;
using NUnit.Framework;
using IVH.Core.Knowledge.Readers;

namespace IVH.Core.Tests.Knowledge
{
    /// <summary>
    /// Unit tests for <see cref="MarkdownReader"/> — section splitting, noise stripping, and
    /// citation metadata.
    /// </summary>
    public class MarkdownReaderTests
    {
        [Test]
        public void SplitsOnTopLevelHeadings()
        {
            string md = "# Intro\nFirst paragraph.\n\n## Methods\nSecond section body.\n\n## Results\nThird.";
            var sections = new MarkdownReader().Read("paper.md", md).ToList();

            Assert.AreEqual(3, sections.Count);
            Assert.AreEqual("Intro", sections[0].sectionTitle);
            Assert.AreEqual("Methods", sections[1].sectionTitle);
            Assert.AreEqual("Results", sections[2].sectionTitle);
            Assert.AreEqual(1, sections[0].sectionIndex);
            Assert.AreEqual(2, sections[1].sectionIndex);
            Assert.AreEqual(3, sections[2].sectionIndex);
        }

        [Test]
        public void StripsCodeFencesAndInlineCode()
        {
            string md = "# Section\nUse the `foo()` function.\n\n```python\nprint('hi')\n```\n\nDone.";
            var sections = new MarkdownReader().Read("doc.md", md).ToList();

            Assert.AreEqual(1, sections.Count);
            Assert.IsFalse(sections[0].text.Contains("print('hi')"), "fenced code body should be stripped");
            Assert.IsFalse(sections[0].text.Contains("foo()"), "inline code body should be stripped");
            Assert.IsTrue(sections[0].text.Contains("Use the"));
            Assert.IsTrue(sections[0].text.Contains("Done."));
        }

        [Test]
        public void CollapsesLinksToAnchorText()
        {
            string md = "# Refs\nSee [the docs](https://example.com/x) for more.";
            var sections = new MarkdownReader().Read("refs.md", md).ToList();

            Assert.IsTrue(sections[0].text.Contains("the docs"));
            Assert.IsFalse(sections[0].text.Contains("example.com"));
        }

        [Test]
        public void StripsEmphasisMarkers()
        {
            string md = "# E\nThis is **bold** and *italic* and __also_bold__.";
            var sections = new MarkdownReader().Read("e.md", md).ToList();

            Assert.IsTrue(sections[0].text.Contains("bold"));
            Assert.IsTrue(sections[0].text.Contains("italic"));
            Assert.IsFalse(sections[0].text.Contains("**"));
        }

        [Test]
        public void DocumentWithoutHeadings_ProducesSingleSection()
        {
            string md = "Just a paragraph.\n\nAnother paragraph.";
            var sections = new MarkdownReader().Read("plain.md", md).ToList();

            Assert.AreEqual(1, sections.Count);
            Assert.AreEqual("", sections[0].sectionTitle);
            Assert.AreEqual(1, sections[0].sectionIndex);
        }

        [Test]
        public void CanRead_OnlyAcceptsMarkdownExtensions()
        {
            var reader = new MarkdownReader();
            Assert.IsTrue(reader.CanRead("foo.md"));
            Assert.IsTrue(reader.CanRead("FOO.MD"));
            Assert.IsTrue(reader.CanRead("foo.markdown"));
            Assert.IsFalse(reader.CanRead("foo.txt"));
            Assert.IsFalse(reader.CanRead("foo.pdf"));
            Assert.IsFalse(reader.CanRead(""));
            Assert.IsFalse(reader.CanRead(null));
        }

        [Test]
        public void StripsExtensionFromSourceFileMetadata()
        {
            var sections = new MarkdownReader().Read("proposal.md", "# H\nBody.").ToList();
            Assert.AreEqual("proposal", sections[0].sourceFile);
        }
    }
}
