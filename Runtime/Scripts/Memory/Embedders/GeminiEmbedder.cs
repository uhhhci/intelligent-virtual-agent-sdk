using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IVH.Core.Utils.Logging;

namespace IVH.Core.Memory.Embedders
{
    /// <summary>
    /// Embeds text via the Google AI Studio embeddings endpoint (gemini-embedding-001). Output
    /// vectors are 768-dimensional by default.
    /// </summary>
    public class GeminiEmbedder : IEmbedder
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly HttpClient _http;

        public int Dimension => 768;

        public GeminiEmbedder(string apiKey, string model = "gemini-embedding-001")
        {
            _apiKey = apiKey;
            _model = model;
            _http = new HttpClient();
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return new float[Dimension];

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:embedContent?key={_apiKey}";
            var payload = new
            {
                content = new { parts = new[] { new { text } } }
            };

            try
            {
                var body = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync(url, body);
                string json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    IVALogger.Warn("GeminiEmbedder", $"Embedding request failed: {resp.StatusCode} {json}");
                    return new float[Dimension];
                }
                JObject parsed = JObject.Parse(json);
                JArray values = parsed["embedding"]?["values"] as JArray;
                if (values == null) return new float[Dimension];
                float[] result = new float[values.Count];
                for (int i = 0; i < values.Count; i++) result[i] = values[i].Value<float>();
                return result;
            }
            catch (Exception ex)
            {
                IVALogger.Error("GeminiEmbedder", "Embedding error", ex);
                return new float[Dimension];
            }
        }
    }
}
