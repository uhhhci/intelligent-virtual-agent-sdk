using System.Threading.Tasks;

namespace IVH.Core.Memory
{
    /// <summary>
    /// Produces a vector embedding for a text chunk. Implementations include
    /// <see cref="Embedders.GeminiEmbedder"/> and any custom provider the user plugs in.
    /// </summary>
    public interface IEmbedder
    {
        /// <summary>Dimension of the produced vectors. Must be constant for a given embedder instance.</summary>
        int Dimension { get; }

        Task<float[]> EmbedAsync(string text);
    }
}
