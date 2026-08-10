using System.Collections.Generic;

namespace IVH.Core.Knowledge.Readers
{
    /// <summary>
    /// Parses a raw document source into a sequence of <see cref="DocumentSection"/> values that
    /// downstream chunkers and embedders consume. Implementations are responsible for stripping
    /// formatting noise and preserving section boundaries that aid retrieval.
    /// </summary>
    public interface IDocumentReader
    {
        /// <summary>True if this reader can handle the given file name (extension match).</summary>
        bool CanRead(string fileName);

        /// <summary>
        /// Parses the raw text of a document into one or more sections. The reader receives the
        /// full file contents already loaded in memory; it does not perform IO itself.
        /// </summary>
        /// <param name="fileName">Source file name (without path) used for citation metadata.</param>
        /// <param name="rawText">Full file contents as a string.</param>
        IEnumerable<DocumentSection> Read(string fileName, string rawText);
    }
}
