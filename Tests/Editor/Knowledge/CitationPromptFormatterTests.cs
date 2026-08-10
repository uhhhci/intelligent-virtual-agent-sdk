using System.Collections.Generic;
using NUnit.Framework;
using IVH.Core.Knowledge;
using IVH.Core.Memory;

namespace IVH.Core.Tests.Knowledge
{
    /// <summary>
    /// Unit tests for <see cref="CitationPromptFormatter"/> — verifies citation rendering, the
    /// max-context-chars budget, and graceful handling of missing metadata.
    /// </summary>
    public class CitationPromptFormatterTests
    {
        private static (MemoryItem item, float similarity) Hit(string text, string source, int section, string title)
        {
            var cite = new ChunkCitation { sourceFile = source, sectionIndex = section, sectionTitle = title };
            return (new MemoryItem { text = text, metadataJson = cite.ToJson() }, 0.9f);
        }

        [Test]
        public void NullOrEmptyHits_ReturnsEmptyString()
        {
            Assert.AreEqual("", CitationPromptFormatter.Format(null, null, 1000));
            Assert.AreEqual("", CitationPromptFormatter.Format(new List<(MemoryItem, float)>(), null, 1000));
        }

        [Test]
        public void DefaultTemplate_IsNaturalSpeechAndIncludesSources()
        {
            var hits = new List<(MemoryItem, float)>
            {
                Hit("Photosynthesis converts light to chemical energy.", "biology", 2, "Cells"),
            };

            string output = CitationPromptFormatter.Format(hits, null, 4000);

            // Default suppresses spoken citation but still hands the model the source list.
            StringAssert.Contains("background knowledge", output);
            StringAssert.Contains("Do NOT read the reference markers", output);
            StringAssert.Contains("[source: biology §2 \"Cells\"]", output);
            StringAssert.Contains("Photosynthesis converts light to chemical energy.", output);
        }

        [Test]
        public void StrictCitationTemplate_ForcesExplicitCitation()
        {
            var hits = new List<(MemoryItem, float)>
            {
                Hit("Photosynthesis converts light to chemical energy.", "biology", 2, "Cells"),
            };

            string output = CitationPromptFormatter.Format(hits, CitationPromptFormatter.StrictCitationInstructionTemplate, 4000);

            StringAssert.Contains("Use ONLY the following sources", output);
            StringAssert.Contains("[source: biology §2 \"Cells\"]", output);
        }

        [Test]
        public void MissingMetadata_FallsBackToUnknownLabel()
        {
            var hits = new List<(MemoryItem, float)>
            {
                (new MemoryItem { text = "Orphan chunk." }, 0.5f),
            };

            string output = CitationPromptFormatter.Format(hits, null, 4000);
            StringAssert.Contains("[source: unknown]", output);
            StringAssert.Contains("Orphan chunk.", output);
        }

        [Test]
        public void MaxContextChars_TruncatesGreedily()
        {
            string longBody = new string('x', 800);
            var hits = new List<(MemoryItem, float)>
            {
                Hit(longBody, "doc", 1, "A"),
                Hit(longBody, "doc", 2, "B"),
                Hit(longBody, "doc", 3, "C"),
                Hit(longBody, "doc", 4, "D"),
            };

            string output = CitationPromptFormatter.Format(hits, null, 1500);

            Assert.Less(output.Length, 1700,
                "output should respect the budget ceiling (some slack for the template overhead estimate)");
            StringAssert.Contains("§1", output);
            // Later sections must be dropped to stay within the budget.
            Assert.IsFalse(output.Contains("§4"), "lowest-priority entry should be dropped under budget pressure");
        }

        [Test]
        public void CustomTemplate_ReplacesSourcesToken()
        {
            string template = "INSTRUCTION: cite always.\nSources:\n{sources}";
            var hits = new List<(MemoryItem, float)>
            {
                Hit("Body.", "src", 1, ""),
            };

            string output = CitationPromptFormatter.Format(hits, template, 1000);
            StringAssert.StartsWith("INSTRUCTION: cite always.", output);
            StringAssert.Contains("[source: src §1]", output);
        }
    }
}
