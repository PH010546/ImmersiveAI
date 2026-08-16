using System;

namespace ImmersiveAI.Sentiments
{
    public enum SentimentType
    {
        // Debts of Honor (恩情)
        BattlefieldMercy,   // 戰後釋放敵對領主
        FreedFromCaptivity, // 從敵軍或地牢中解救
        DesperateRescue,    // 絕境戰場馳援
        LivedSharedOrdeal,  // 生死患難同袍

        // Grudges (仇怨)
        KinSlainOrExecuted, // 斬殺或處決家族成員
        FiefRaided,         // 封邑村莊被焚掠
        BrokenVow           // 撕毀誓約或背叛
    }

    /// <summary>
    /// Represents an event-anchored memory of honor, debt of gratitude, or bitter grudge.
    /// </summary>
    public sealed class SentimentEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string HeroId { get; set; } = string.Empty;
        public SentimentType Type { get; set; }
        public bool IsGrudge { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double GameDay { get; set; }
        public string DateText { get; set; } = string.Empty;
        public string PlaceName { get; set; } = string.Empty;
        public int Weight { get; set; } = 1; // 1 to 5 scale
    }
}
