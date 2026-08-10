using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IVH.Core.Knowledge.Chunkers;
using IVH.Core.Knowledge.Readers;

namespace IVH.Core.Tests.Knowledge
{
    /// <summary>
    /// Unit tests for <see cref="SlidingWindowChunker"/> — pure-function chunking with no Unity
    /// runtime required.
    /// </summary>
    public class SlidingWindowChunkerTests
    {
        private static DocumentSection MakeSection(string body)
        {
            return new DocumentSection
            {
                sourceFile = "test",
                sectionIndex = 1,
                sectionTitle = "Demo",
                text = body,
            };
        }

        [Test]
        public void SingleShortSection_ProducesOneChunk()
        {
            var chunker = new SlidingWindowChunker(1000, 100);
            var chunks = chunker.Chunk(MakeSection("This is a short section.")).ToList();

            Assert.AreEqual(1, chunks.Count);
            Assert.AreEqual("This is a short section.", chunks[0].text);
            Assert.AreEqual(0, chunks[0].chunkIndex);
            Assert.AreEqual("test", chunks[0].sourceFile);
            Assert.AreEqual(1, chunks[0].sectionIndex);
        }

        [Test]
        public void LongSection_SplitsIntoMultipleChunks()
        {
            string body = string.Concat(Enumerable.Repeat("alpha beta gamma. ", 500));
            var chunker = new SlidingWindowChunker(500, 50);
            var chunks = chunker.Chunk(MakeSection(body)).ToList();

            Assert.Greater(chunks.Count, 1, "expected more than one chunk for an 8500+ char section");
            for (int i = 0; i < chunks.Count; i++)
            {
                Assert.AreEqual(i, chunks[i].chunkIndex);
                Assert.LessOrEqual(chunks[i].text.Length, 500);
            }
        }

        [Test]
        public void Overlap_PreservesBoundaryContent()
        {
            // Build a body where each marker is unique so we can verify overlap.
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 200; i++) sb.Append($"word{i} ");
            var chunker = new SlidingWindowChunker(300, 100);
            var chunks = chunker.Chunk(MakeSection(sb.ToString())).ToList();

            Assert.Greater(chunks.Count, 1);
            // The tail of chunk 0 and head of chunk 1 must share at least one word marker.
            string tail0 = chunks[0].text.Substring(System.Math.Max(0, chunks[0].text.Length - 80));
            string head1 = chunks[1].text.Substring(0, System.Math.Min(80, chunks[1].text.Length));
            bool sharesAtLeastOneMarker = false;
            for (int i = 0; i < 200; i++)
            {
                string marker = $"word{i}";
                if (tail0.Contains(marker) && head1.Contains(marker)) { sharesAtLeastOneMarker = true; break; }
            }
            Assert.IsTrue(sharesAtLeastOneMarker, "overlap should re-emit at least one boundary marker");
        }

        [Test]
        public void EmptySection_ProducesNoChunks()
        {
            var chunker = new SlidingWindowChunker(500, 50);
            Assert.IsEmpty(chunker.Chunk(MakeSection("")).ToList());
            Assert.IsEmpty(chunker.Chunk(MakeSection(null)).ToList());
            Assert.IsEmpty(chunker.Chunk(null).ToList());
        }

        [Test]
        public void OverlapClampedBelowHalfChunkSize()
        {
            // Pass an overlap larger than half the chunk size; chunker must clamp without spinning.
            var chunker = new SlidingWindowChunker(200, 1000);
            string body = string.Concat(Enumerable.Repeat("x", 1000));
            var chunks = chunker.Chunk(MakeSection(body)).ToList();
            Assert.Greater(chunks.Count, 0);
            Assert.LessOrEqual(chunks.Count, 50, "should not produce a runaway chunk count");
        }
    }
}
