using System.Collections.Generic;
using IVH.Core.Knowledge.Readers;

namespace IVH.Core.Knowledge.Chunkers
{
    /// <summary>
    /// Character-based sliding-window chunker with overlap. Splits at paragraph boundaries when
    /// possible, falling back to sentence boundaries, then to a hard character cut. No tokenizer
    /// dependency — char counts are an embedding-token approximation that works across providers.
    /// </summary>
    public class SlidingWindowChunker : IChunker
    {
        private readonly int _chunkCharSize;
        private readonly int _chunkCharOverlap;

        /// <summary>
        /// Creates a chunker.
        /// </summary>
        /// <param name="chunkCharSize">Target chunk size in characters. Clamped to a minimum of 100.</param>
        /// <param name="chunkCharOverlap">Overlap between consecutive chunks in characters. Clamped below half of <paramref name="chunkCharSize"/>.</param>
        public SlidingWindowChunker(int chunkCharSize, int chunkCharOverlap)
        {
            _chunkCharSize = chunkCharSize < 100 ? 100 : chunkCharSize;
            int maxOverlap = _chunkCharSize / 2;
            _chunkCharOverlap = chunkCharOverlap < 0 ? 0 : (chunkCharOverlap > maxOverlap ? maxOverlap : chunkCharOverlap);
        }

        /// <inheritdoc/>
        public IEnumerable<TextChunk> Chunk(DocumentSection section)
        {
            if (section == null || string.IsNullOrEmpty(section.text)) yield break;

            string text = section.text;
            int length = text.Length;
            int chunkIndex = 0;
            int start = 0;

            while (start < length)
            {
                int end = start + _chunkCharSize;
                if (end >= length)
                {
                    yield return BuildChunk(section, text.Substring(start), chunkIndex);
                    yield break;
                }

                // Prefer to break on a paragraph (\n\n), then sentence, then whitespace.
                int breakPoint = FindBreakPoint(text, start, end);
                int actualEnd = breakPoint > start ? breakPoint : end;
                string body = text.Substring(start, actualEnd - start);
                yield return BuildChunk(section, body, chunkIndex);

                chunkIndex++;
                int next = actualEnd - _chunkCharOverlap;
                start = next > start ? next : actualEnd;
            }
        }

        private static int FindBreakPoint(string text, int start, int end)
        {
            int searchFloor = start + (end - start) / 2;

            int paragraph = text.LastIndexOf("\n\n", end - 1, end - searchFloor);
            if (paragraph >= searchFloor) return paragraph + 2;

            int sentence = LastIndexOfAny(text, new[] { ". ", "? ", "! " }, end - 1, end - searchFloor);
            if (sentence >= searchFloor) return sentence + 2;

            int space = text.LastIndexOf(' ', end - 1, end - searchFloor);
            if (space >= searchFloor) return space + 1;

            return end;
        }

        private static int LastIndexOfAny(string text, string[] needles, int startIndex, int count)
        {
            int best = -1;
            foreach (var n in needles)
            {
                int found = text.LastIndexOf(n, startIndex, count);
                if (found > best) best = found;
            }
            return best;
        }

        private static TextChunk BuildChunk(DocumentSection section, string body, int chunkIndex)
        {
            return new TextChunk
            {
                text = body.Trim(),
                sourceFile = section.sourceFile,
                sectionIndex = section.sectionIndex,
                sectionTitle = section.sectionTitle,
                chunkIndex = chunkIndex,
            };
        }
    }
}
