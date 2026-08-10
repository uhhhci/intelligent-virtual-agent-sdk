using System.Collections.Generic;

namespace IVH.Core.Observability
{
    /// <summary>
    /// Rolling counters over a single session. Aggregated into a summary file when the session ends.
    /// </summary>
    public class MetricsCollector
    {
        private readonly List<int> _firstTokenLatenciesMs = new List<int>();
        private readonly List<int> _firstAudioLatenciesMs = new List<int>();

        public int TurnCount { get; private set; }
        public int InterruptionCount { get; private set; }
        public int ToolCallCount { get; private set; }
        public int ErrorCount { get; private set; }
        public int ReconnectCount { get; private set; }
        public long TotalTokensIn { get; private set; }
        public long TotalTokensOut { get; private set; }

        public void RecordTurn() => TurnCount++;
        public void RecordInterruption() => InterruptionCount++;
        public void RecordToolCall() => ToolCallCount++;
        public void RecordError() => ErrorCount++;
        public void RecordReconnect() => ReconnectCount++;
        public void RecordTokens(int tokensIn, int tokensOut)
        {
            TotalTokensIn += tokensIn;
            TotalTokensOut += tokensOut;
        }
        public void RecordFirstTokenLatency(int ms) => _firstTokenLatenciesMs.Add(ms);
        public void RecordFirstAudioLatency(int ms) => _firstAudioLatenciesMs.Add(ms);

        /// <summary>Returns the JSON-friendly summary object suitable for writing as {session}.summary.json.</summary>
        public object BuildSummary(string sessionId, System.DateTime startedAtUtc, System.DateTime endedAtUtc)
        {
            return new
            {
                session = sessionId,
                started_at = startedAtUtc.ToString("o"),
                ended_at = endedAtUtc.ToString("o"),
                duration_s = (endedAtUtc - startedAtUtc).TotalSeconds,
                turn_count = TurnCount,
                interruption_count = InterruptionCount,
                tool_call_count = ToolCallCount,
                error_count = ErrorCount,
                reconnect_count = ReconnectCount,
                total_tokens_in = TotalTokensIn,
                total_tokens_out = TotalTokensOut,
                first_token_latency_ms = LatencyStats(_firstTokenLatenciesMs),
                first_audio_latency_ms = LatencyStats(_firstAudioLatenciesMs),
            };
        }

        private static object LatencyStats(List<int> samples)
        {
            if (samples.Count == 0) return new { count = 0 };
            samples.Sort();
            double mean = 0;
            for (int i = 0; i < samples.Count; i++) mean += samples[i];
            mean /= samples.Count;
            return new
            {
                count = samples.Count,
                mean,
                p50 = Percentile(samples, 0.50),
                p95 = Percentile(samples, 0.95),
                p99 = Percentile(samples, 0.99),
            };
        }

        private static int Percentile(List<int> sortedSamples, double p)
        {
            if (sortedSamples.Count == 0) return 0;
            int idx = (int)System.Math.Ceiling(p * sortedSamples.Count) - 1;
            idx = System.Math.Max(0, System.Math.Min(sortedSamples.Count - 1, idx));
            return sortedSamples[idx];
        }
    }
}
