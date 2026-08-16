using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Sentiments
{
    /// <summary>
    /// Manages event-anchored sentiments (debts of honor and grudges) and enforces
    /// the daily dialogue relation cap along a natural Sigmoid (S-curve) progression.
    /// </summary>
    public sealed class SentimentLedger
    {
        private sealed class DailyGainRecord
        {
            public double LastDay;
            public int AccumulatedGain;
        }

        private readonly object _lock = new object();
        private readonly Dictionary<string, DailyGainRecord> _dailyGains = new Dictionary<string, DailyGainRecord>(StringComparer.Ordinal);
        private readonly List<SentimentEvent> _events = new List<SentimentEvent>();

        private const string SentimentsFileName = "_sentiments.json";

        public static SentimentLedger Load(string campaignRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(campaignRoot)) return new SentimentLedger();
                string path = Path.Combine(campaignRoot, SentimentsFileName);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var list = JsonConvert.DeserializeObject<List<SentimentEvent>>(json);
                    var ledger = new SentimentLedger();
                    if (list != null) ledger._events.AddRange(list);
                    return ledger;
                }
            }
            catch { /* best-effort persistence */ }
            return new SentimentLedger();
        }

        public void Save(string campaignRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(campaignRoot)) return;
                Directory.CreateDirectory(campaignRoot);
                string path = Path.Combine(campaignRoot, SentimentsFileName);
                lock (_lock)
                {
                    string json = JsonConvert.SerializeObject(_events, Formatting.Indented);
                    File.WriteAllText(path, json);
                }
            }
            catch { /* best-effort */ }
        }

        public void RecordEvent(SentimentEvent evt, string campaignRoot)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.HeroId)) return;
            lock (_lock)
            {
                // Avoid duplicate identical events within 1 day
                var existing = _events.FirstOrDefault(e =>
                    e.HeroId == evt.HeroId &&
                    e.Type == evt.Type &&
                    Math.Abs(e.GameDay - evt.GameDay) < 1.0);

                if (existing == null)
                {
                    _events.Add(evt);
                    // Keep most recent 50 events across the campaign
                    if (_events.Count > 50) _events.RemoveAt(0);
                }
            }
            Save(campaignRoot);
        }

        public IReadOnlyList<SentimentEvent> GetEventsFor(Hero npc)
        {
            if (npc == null) return Array.Empty<SentimentEvent>();
            lock (_lock)
            {
                return _events
                    .Where(e => e.HeroId == npc.StringId)
                    .OrderByDescending(e => e.GameDay)
                    .Take(4)
                    .ToList();
            }
        }

        /// <summary>
        /// Calculates the effective relation shift applying the Sigmoid (S-curve) and daily cap.
        /// Epic events bypass the daily cap.
        /// </summary>
        public int GetEffectiveShift(Hero npc, int requestedShift, double currentDay, ModConfig? config, bool isEpicEvent = false)
        {
            if (npc == null || requestedShift == 0) return 0;
            if (config != null && !config.EnableRelationshipChanges) return 0;

            // Negative shifts (wounds/offenses) are never capped — pain is felt immediately
            if (requestedShift < 0) return requestedShift;

            // Epic events (e.g. lifesaver rescue, major battle) bypass daily casual cap
            if (isEpicEvent) return requestedShift;

            // If daily cap is disabled in settings, allow full shift
            if (config != null && !config.EnableDailyRelationCap) return requestedShift;

            int currentRelation = npc.GetRelation(Hero.MainHero);
            int dailyCap = GetDailyCapForRelation(currentRelation, config);

            lock (_lock)
            {
                if (!_dailyGains.TryGetValue(npc.StringId, out var record))
                {
                    record = new DailyGainRecord { LastDay = currentDay, AccumulatedGain = 0 };
                    _dailyGains[npc.StringId] = record;
                }

                // Reset cap if a new game day has passed
                if (currentDay - record.LastDay >= 1.0)
                {
                    record.LastDay = currentDay;
                    record.AccumulatedGain = 0;
                }

                int remainingAllowance = Math.Max(0, dailyCap - record.AccumulatedGain);
                if (remainingAllowance <= 0) return 0;

                int effective = Math.Min(requestedShift, remainingAllowance);
                record.AccumulatedGain += effective;
                return effective;
            }
        }

        /// <summary>
        /// S-Curve Tiered Daily Caps:
        /// - Negative / Distrust (-100 to 0): Max +1/day (thawing frost)
        /// - Icebreaking / Neutral (0 to +10): Max +2/day (polite reserve)
        /// - Accelerated Acquaintance (+10 to +35): Max Config Cap (default +3)/day (smooth bonding)
        /// - Deep Confidant (+35 to +60): Max +1/day (diminishing returns from chat alone)
        /// - Devoted Friends (+60 to +100): Max +1 every 3 days (plateau saturation)
        /// </summary>
        private static int GetDailyCapForRelation(int relation, ModConfig? config)
        {
            int baseCap = config?.DailyDialogueRelationCap ?? 3;

            if (relation < 0) return 1;
            if (relation <= 10) return Math.Min(2, baseCap);
            if (relation <= 35) return baseCap;
            if (relation <= 60) return 1;
            return 1; // 60+ is highly saturated
        }

        /// <summary>
        /// Builds a concise, natural description of active debts of honor or grudges for the prompt.
        /// </summary>
        public string DescribeSentimentsFor(Hero npc)
        {
            if (npc == null) return string.Empty;
            var events = GetEventsFor(npc);
            if (events.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("What stands between our souls (Deeds & Grievances):");
            foreach (var e in events)
            {
                string tag = e.IsGrudge ? "A bitter grudge" : "A debt of honor";
                string when = string.IsNullOrWhiteSpace(e.DateText) ? string.Empty : $" on {e.DateText}";
                string where = string.IsNullOrWhiteSpace(e.PlaceName) ? string.Empty : $" near {e.PlaceName}";
                sb.AppendLine($"- {tag}:{when}{where}, {e.Description.Trim()}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
