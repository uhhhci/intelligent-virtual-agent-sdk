using System;

namespace IVH.Core.Knowledge.Readers
{
    /// <summary>
    /// One coherent slice of a parsed document, typically corresponding to a top-level heading.
    /// Sections preserve the citation metadata that survives the chunking step.
    /// </summary>
    [Serializable]
    public class DocumentSection
    {
        /// <summary>Source file name (without path or extension) for citation display.</summary>
        public string sourceFile;

        /// <summary>One-based section index inside the source document.</summary>
        public int sectionIndex;

        /// <summary>Section heading text, or empty when the source has no headings.</summary>
        public string sectionTitle;

        /// <summary>Plain text content of the section, with formatting noise stripped.</summary>
        public string text;
    }
}
