using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImmersiveAI.Core.Memory;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Tracks the player's party movements, battles, and travels on the Calradia map.
    /// Computes spatial vector displacement (distance & cardinal direction), elapsed time,
    /// and intermediate milestones between conversations so NPCs understand the passage of time and journey.
    /// </summary>
    public static class PartyJourneyTracker
    {
        public enum JourneyEventType
        {
            SettlementVisited,
            BattleFought,
            SiegeFought
        }

        public sealed class JourneyEvent
        {
            public double GameDay { get; set; }
            public JourneyEventType Type { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
            public Vec2 Position { get; set; }
        }

        private static readonly object _lock = new object();
        private const int MaxEvents = 30;
        private static readonly List<JourneyEvent> _events = new List<JourneyEvent>();

        public static void Clear()
        {
            lock (_lock) { _events.Clear(); }
        }

        public static void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                if (party == null || party != MobileParty.MainParty || settlement == null) return;

                lock (_lock)
                {
                    double now = CampaignTime.Now.ToDays;
                    var last = _events.LastOrDefault(e => e.Type == JourneyEventType.SettlementVisited);
                    if (last != null && last.Name == settlement.Name?.ToString() && (now - last.GameDay) < 0.25)
                        return; // avoid rapid re-entry spam

                    _events.Add(new JourneyEvent
                    {
                        GameDay = now,
                        Type = JourneyEventType.SettlementVisited,
                        Name = settlement.Name?.ToString() ?? settlement.StringId,
                        Details = settlement.Culture?.Name?.ToString() ?? string.Empty,
                        Position = settlement.Position2D
                    });

                    if (_events.Count > MaxEvents)
                        _events.RemoveAt(0);
                }
            }
            catch { /* best-effort tracking */ }
        }

        public static void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null || !mapEvent.InvolvedParties.Contains(PartyBase.MainParty)) return;

                lock (_lock)
                {
                    double now = CampaignTime.Now.ToDays;
                    bool playerWon = mapEvent.WinningSide == mapEvent.PlayerSide;
                    var opponentSide = mapEvent.PlayerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
                    var opponentParty = mapEvent.PartiesOnSide(opponentSide).FirstOrDefault()?.Party;
                    string enemyName = opponentParty?.Name?.ToString() ?? "an enemy force";

                    string result = playerWon ? $"defeated {enemyName}" : $"fought a hard battle against {enemyName}";

                    _events.Add(new JourneyEvent
                    {
                        GameDay = now,
                        Type = JourneyEventType.BattleFought,
                        Name = enemyName,
                        Details = result,
                        Position = mapEvent.Position
                    });

                    if (_events.Count > MaxEvents)
                        _events.RemoveAt(0);
                }
            }
            catch { /* best-effort tracking */ }
        }

        public static void OnSiegeEventEnded(SiegeEvent siegeEvent)
        {
            try
            {
                if (siegeEvent == null || siegeEvent.BesiegedSettlement == null) return;
                bool involved = false;
                try
                {
                    involved = siegeEvent.BesiegerCamp?.LeaderParty == MobileParty.MainParty
                        || (MobileParty.MainParty?.CurrentSettlement == siegeEvent.BesiegedSettlement);
                }
                catch { }

                if (!involved) return;

                lock (_lock)
                {
                    double now = CampaignTime.Now.ToDays;
                    string target = siegeEvent.BesiegedSettlement.Name?.ToString() ?? "a stronghold";
                    _events.Add(new JourneyEvent
                    {
                        GameDay = now,
                        Type = JourneyEventType.SiegeFought,
                        Name = target,
                        Details = $"participated in the siege of {target}",
                        Position = siegeEvent.BesiegedSettlement.Position2D
                    });

                    if (_events.Count > MaxEvents)
                        _events.RemoveAt(0);
                }
            }
            catch { /* best-effort tracking */ }
        }

        /// <summary>
        /// Computes the cardinal direction (North, South, North-East, etc.) from origin to target.
        /// In Bannerlord map coordinates: +Y is North, -Y is South, +X is East, -X is West.
        /// </summary>
        public static string GetCardinalDirection(Vec2 from, Vec2 to)
        {
            Vec2 delta = to - from;
            float dx = delta.X;
            float dy = delta.Y;

            if (Math.Abs(dx) < 5f && Math.Abs(dy) < 5f)
                return "nearby";

            // If mostly North-South
            if (Math.Abs(dy) > Math.Abs(dx) * 2.2f)
                return dy > 0 ? "north" : "south";

            // If mostly East-West
            if (Math.Abs(dx) > Math.Abs(dy) * 2.2f)
                return dx > 0 ? "east" : "west";

            // Diagonal combinations
            string ns = dy > 0 ? "north" : "south";
            string ew = dx > 0 ? "east" : "west";
            return $"{ns}-{ew}";
        }

        /// <summary>
        /// Estimates travel distance in leagues/miles from 2D coordinates.
        /// </summary>
        public static int EstimateMiles(Vec2 from, Vec2 to)
        {
            float len = (to - from).Length;
            // 1 map coordinate unit is roughly ~3.5-4 in-game miles in Calradia scale
            return Math.Max(5, (int)(len * 3.8f));
        }

        /// <summary>
        /// Builds a rich, natural narrative of the journey and elapsed time since the last conversation turn.
        /// </summary>
        public static string DescribeJourneySince(Hero speaker, NpcMemory memory, Settlement? currentSettlement)
        {
            if (speaker == null || memory == null) return string.Empty;

            var lastTurn = memory.RecentTurns.LastOrDefault();
            double now = CampaignTime.Now.ToDays;
            if (lastTurn == null)
            {
                // No past dialogue recorded yet; if traveling with party, mention general party road status
                if (speaker.PartyBelongedTo == MobileParty.MainParty && MobileParty.MainParty != null)
                {
                    var recentVisited = GetRecentVisitedNames(now - 3.0, now);
                    if (recentVisited.Count > 0)
                        return $"Our party rides together upon the road; we have recently passed through {string.Join(", ", recentVisited)}.";
                }
                return string.Empty;
            }

            double elapsedDays = now - lastTurn.GameDay;
            // If spoken within the same breath/hour (less than ~0.25 days), no time/space leap occurred
            if (elapsedDays < 0.25)
                return string.Empty;

            var sb = new StringBuilder();
            string lastPlace = string.IsNullOrWhiteSpace(lastTurn.Place) ? "our previous stop" : lastTurn.Place.Trim();
            string currentPlace = currentSettlement?.Name?.ToString() ?? (MobileParty.MainParty != null ? "the road" : "here");

            // Look up origin and destination coordinates
            Vec2? fromPos = FindSettlementPosition(lastPlace);
            Vec2 toPos = currentSettlement != null ? currentSettlement.Position2D
                : (MobileParty.MainParty?.Position2D ?? Vec2.Zero);

            int days = Math.Max(1, (int)Math.Round(elapsedDays));
            string timeStr = days == 1 ? "1 day" : $"{days} days";

            if (fromPos.HasValue && toPos != Vec2.Zero && fromPos.Value != toPos)
            {
                string dir = GetCardinalDirection(fromPos.Value, toPos);
                int miles = EstimateMiles(fromPos.Value, toPos);

                if (dir != "nearby" && miles > 25)
                {
                    sb.Append($"It has been {timeStr} since we last spoke in {lastPlace}. Over that time, our company has journeyed {dir} across some {miles} miles to reach {currentPlace}.");
                }
                else
                {
                    sb.Append($"It has been {timeStr} since we last spoke in {lastPlace}, and we now stand in {currentPlace}.");
                }
            }
            else
            {
                sb.Append($"It has been {timeStr} since we last spoke in {lastPlace}; time and travel have passed, and we now stand in {currentPlace}.");
            }

            // Gather intermediate settlements visited and battles fought during the interval
            lock (_lock)
            {
                var intermediate = _events
                    .Where(e => e.GameDay >= lastTurn.GameDay - 0.05 && e.GameDay <= now)
                    .ToList();

                var passedCities = intermediate
                    .Where(e => e.Type == JourneyEventType.SettlementVisited && e.Name != lastPlace && e.Name != currentPlace)
                    .Select(e => e.Name)
                    .Distinct()
                    .Take(4)
                    .ToList();

                if (passedCities.Count > 0)
                {
                    sb.Append($" Along the way, our road took us through {string.Join(", ", passedCities)}.");
                }

                var battles = intermediate
                    .Where(e => e.Type == JourneyEventType.BattleFought)
                    .Select(e => e.Details)
                    .Take(2)
                    .ToList();

                if (battles.Count > 0)
                {
                    sb.Append($" On the road, our company {string.Join(" and ", battles)}.");
                }
            }

            return sb.ToString().Trim();
        }

        private static List<string> GetRecentVisitedNames(double startDay, double endDay)
        {
            lock (_lock)
            {
                return _events
                    .Where(e => e.Type == JourneyEventType.SettlementVisited && e.GameDay >= startDay && e.GameDay <= endDay)
                    .Select(e => e.Name)
                    .Distinct()
                    .Take(3)
                    .ToList();
            }
        }

        private static Vec2? FindSettlementPosition(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try
            {
                var s = Settlement.All?.FirstOrDefault(x =>
                    string.Equals(x.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.StringId, name, StringComparison.OrdinalIgnoreCase));
                return s?.Position2D;
            }
            catch { return null; }
        }
    }
}
