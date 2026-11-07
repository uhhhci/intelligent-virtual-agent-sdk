using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace IVH.Core.ServiceConnector.LocalVoice
{
    /// <summary>
    /// The server's REST endpoints a Unity client needs: <c>/v1/health</c> and the voice list
    /// (docs/API.md of the LocalVoiceServer repository). Plain <see cref="HttpClient"/>, so it works outside
    /// Unity too; answers are returned as parsed JSON because every field of them is optional by contract.
    /// </summary>
    public sealed class LocalVoiceRest : IDisposable
    {
        private readonly HttpClient _http;
        public Uri BaseUri { get; }

        /// <param name="baseUrl">e.g. <c>http://127.0.0.1:8123</c>. A <c>ws://…/v1/realtime</c> URL is accepted and converted.</param>
        public LocalVoiceRest(string baseUrl, TimeSpan? timeout = null)
        {
            BaseUri = ToHttpBase(baseUrl);
            _http = new HttpClient { BaseAddress = BaseUri, Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        }

        /// <summary><c>ws://h:p/v1/realtime</c> → <c>http://h:p/</c>.</summary>
        public static Uri ToHttpBase(string url)
        {
            var u = new Uri(url);
            string scheme = u.Scheme == "wss" ? "https" : u.Scheme == "ws" ? "http" : u.Scheme;
            return new Uri($"{scheme}://{u.Host}:{u.Port}/");
        }

        public async Task<JObject> GetHealthAsync(CancellationToken ct = default) =>
            JObject.Parse(await _http.GetStringAsync("v1/health").ConfigureAwait(false));

        /// <summary><c>GET /v1/voices</c>: <c>{voices: [...], default_voice, cloning_available}</c>.</summary>
        public async Task<JObject> GetVoicesAsync(CancellationToken ct = default) =>
            JObject.Parse(await _http.GetStringAsync("v1/voices").ConfigureAwait(false));

        /// <summary>The ready voice whose id or name matches <paramref name="idOrName"/>, else null.</summary>
        public async Task<JObject> FindVoiceAsync(string idOrName, CancellationToken ct = default)
        {
            var list = await GetVoicesAsync(ct).ConfigureAwait(false);
            foreach (var v in (JArray)list["voices"] ?? new JArray())
            {
                if (string.Equals((string)v["id"], idOrName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((string)v["name"], idOrName, StringComparison.OrdinalIgnoreCase))
                    return (JObject)v;
            }
            return null;
        }

        public void Dispose() => _http.Dispose();
    }
}
