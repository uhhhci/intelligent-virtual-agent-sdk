using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace IVH.Core.Knowledge.Readers
{
    /// <summary>
    /// Parses a Markdown document into sections split on top-level headings (<c># </c> or
    /// <c>## </c>). Strips fenced code blocks, inline code, link/image syntax, and emphasis
    /// markers — preserves heading text, list bullets, and paragraph breaks. Documents with no
    /// headings collapse to a single section.
    /// </summary>
    public class MarkdownReader : IDocumentReader
    {
        private static readonly Regex s_codeFence = new Regex(@"```[\s\S]*?```", RegexOptions.Compiled);
        private static readonly Regex s_inlineCode = new Regex(@"`[^`]*`", RegexOptions.Compiled);
        private static readonly Regex s_image = new Regex(@"!\[[^\]]*\]\([^\)]*\)", RegexOptions.Compiled);
        private static readonly Regex s_link = new Regex(@"\[([^\]]+)\]\([^\)]*\)", RegexOptions.Compiled);
        private static readonly Regex s_emphasis = new Regex(@"(\*\*|__|\*|_)(.*?)\1", RegexOptions.Compiled);
        private static readonly Regex s_headingLine = new Regex(@"^(#{1,2})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);

        /// <inheritdoc/>
        public bool CanRead(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return fileName.EndsWith(".md", System.StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".markdown", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public IEnumerable<DocumentSection> Read(string fileName, string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) yield break;

            string normalized = NormalizeWhitespace(StripNoise(rawText));
            string[] lines = normalized.Split('\n');

            string currentTitle = "";
            var buffer = new StringBuilder();
            int sectionIndex = 0;

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                Match h = s_headingLine.Match(line);
                if (h.Success)
                {
                    if (buffer.Length > 0)
                    {
                        sectionIndex++;
                        yield return BuildSection(fileName, sectionIndex, currentTitle, buffer.ToString());
                        buffer.Clear();
                    }
                    currentTitle = h.Groups[2].Value.Trim();
                    continue;
                }

                buffer.AppendLine(line);
            }

            if (buffer.Length > 0)
            {
                sectionIndex++;
                yield return BuildSection(fileName, sectionIndex, currentTitle, buffer.ToString());
            }
        }

        private static string StripNoise(string text)
        {
            text = s_codeFence.Replace(text, " ");
            text = s_inlineCode.Replace(text, " ");
            text = s_image.Replace(text, " ");
            text = s_link.Replace(text, m => m.Groups[1].Value);
            text = s_emphasis.Replace(text, m => m.Groups[2].Value);
            return text;
        }

        private static string NormalizeWhitespace(string text)
        {
            // Collapse Windows / Mac line endings; keep paragraph breaks meaningful.
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            // Collapse runs of 3+ blank lines to 2.
            return Regex.Replace(text, @"\n{3,}", "\n\n");
        }

        private static DocumentSection BuildSection(string fileName, int index, string title, string body)
        {
            return new DocumentSection
            {
                sourceFile = StripExtension(fileName),
                sectionIndex = index,
                sectionTitle = title ?? "",
                text = body.Trim(),
            };
        }

        private static string StripExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            int dot = fileName.LastIndexOf('.');
            return dot > 0 ? fileName.Substring(0, dot) : fileName;
        }
    }
}
