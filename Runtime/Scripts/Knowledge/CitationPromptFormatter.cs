using System.Collections.Generic;
using System.Text;
using IVH.Core.Memory;

namespace IVH.Core.Knowledge
{
    /// <summary>
    /// Formats retrieved chunks into a system-prompt prefix that instructs the LLM to ground its
    /// answer in the supplied sources and emit citations in <c>[source: file §section]</c> form.
    /// </summary>
    public static class CitationPromptFormatter
    {
        /// <summary>
        /// The default instruction template. Token <c>{sources}</c> is replaced with the formatted
        /// source list. The default keeps the agent's spoken response natural — the reference
        /// markers are visible to the model for context but it is explicitly instructed not to
        /// read them out loud. Override per-<see cref="KnowledgeBase"/> when you want strict
        /// text-style citation (e.g. for a chat-log UI where citations should be visible).
        /// </summary>
        public const string DefaultInstructionTemplate =
            "The following is background knowledge you have on the topics being discussed. " +
            "Treat this as information you already know and answer naturally in your own voice. " +
            "Do NOT read the reference markers (the [source: ...] tags) out loud, and do NOT say things like " +
            "\"according to the documents\", \"as referenced in section X\", \"the sources mention\", " +
            "or quote section numbers in your spoken reply. Just speak as if this is simply what you know. " +
            "If the user asks about something not covered here, say so naturally (e.g. \"I'm not sure about that\") instead of guessing.\n\n" +
            "Reference knowledge:\n{sources}\n";

        /// <summary>
        /// Stricter alternative template that forces explicit text-style citation. Use this when
        /// the agent's output is read (chat log, document Q&amp;A) rather than spoken, and the
        /// user benefits from seeing exactly which source backed each claim.
        /// Assign to <see cref="KnowledgeBase.citationInstructionTemplate"/> to opt in.
        /// </summary>
        public const string StrictCitationInstructionTemplate =
            "Use ONLY the following sources to answer the user. " +
            "If the sources do not contain the answer, say \"I don't have that in my reference materials.\" " +
            "After each factual claim, cite the source as [source: <file> §<section>].\n\n" +
            "Sources:\n{sources}\n";

        /// <summary>
        /// Builds the prompt prefix from a list of retrieved memory items. Each item's
        /// <see cref="MemoryItem.metadataJson"/> is expected to be the JSON form of a
        /// <see cref="ChunkCitation"/> written by the editor-time baker. Items without parseable
        /// metadata are still included but cited as <c>[source: unknown]</c>.
        /// </summary>
        /// <param name="hits">Retrieved items with their similarity scores, in descending order.</param>
        /// <param name="instructionTemplate">Override of <see cref="DefaultInstructionTemplate"/>; null falls back to default.</param>
        /// <param name="maxContextChars">Hard upper bound on the produced prefix length. The source list is truncated greedily.</param>
        /// <returns>The formatted prefix string, or empty when <paramref name="hits"/> is null or empty.</returns>
        public static string Format(
            IList<(MemoryItem item, float similarity)> hits,
            string instructionTemplate,
            int maxContextChars)
        {
            if (hits == null || hits.Count == 0) return "";
            string template = string.IsNullOrEmpty(instructionTemplate) ? DefaultInstructionTemplate : instructionTemplate;

            string sources = BuildSourceList(hits, maxContextChars, template.Length);
            if (sources.Length == 0) return "";
            return template.Replace("{sources}", sources);
        }

        /// <summary>
        /// Builds just the bulleted source list with no surrounding instruction template. Used when
        /// returning retrieved chunks as the result of a <c>search_knowledge</c> function call, where
        /// the model already asked for the data and a "treat this as background knowledge" preamble
        /// would be redundant.
        /// </summary>
        /// <param name="hits">Retrieved items with their similarity scores, in descending order.</param>
        /// <param name="maxContextChars">Hard upper bound on the produced length. The list is truncated greedily.</param>
        /// <returns>The formatted source list, or empty when <paramref name="hits"/> is null or empty.</returns>
        public static string FormatSourcesOnly(
            IList<(MemoryItem item, float similarity)> hits,
            int maxContextChars)
        {
            if (hits == null || hits.Count == 0) return "";
            return BuildSourceList(hits, maxContextChars, 0);
        }

        /// <summary>
        /// Emits each hit as <c>- [source: ...] text</c>, greedily stopping before the combined length
        /// (plus <paramref name="reservedChars"/> reserved for a surrounding template) exceeds the budget.
        /// </summary>
        private static string BuildSourceList(
            IList<(MemoryItem item, float similarity)> hits,
            int maxContextChars,
            int reservedChars)
        {
            var sources = new StringBuilder();
            int budget = maxContextChars > 0 ? maxContextChars : int.MaxValue;

            for (int i = 0; i < hits.Count; i++)
            {
                var (item, _) = hits[i];
                if (item == null || string.IsNullOrEmpty(item.text)) continue;

                ChunkCitation cite = ChunkCitation.TryParse(item.metadataJson);
                string label = cite != null
                    ? $"[source: {cite.sourceFile} §{cite.sectionIndex}{(string.IsNullOrEmpty(cite.sectionTitle) ? "" : $" \"{cite.sectionTitle}\"")}]"
                    : "[source: unknown]";

                string line = $"- {label} {item.text.Trim()}\n";
                if (sources.Length + line.Length + reservedChars > budget) break;
                sources.Append(line);
            }

            return sources.ToString();
        }
    }
}
