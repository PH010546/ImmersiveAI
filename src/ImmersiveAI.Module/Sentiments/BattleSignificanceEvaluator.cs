using System;
using System.Linq;
using ImmersiveAI.Core.Battles;
using TaleWorlds.CampaignSystem;

namespace ImmersiveAI.Sentiments
{
    public struct BattleBondResult
    {
        public bool IsEpic;
        public int BonusShift;
        public string Reason;
    }

    /// <summary>
    /// Evaluates the significance, odds, and heroics of a battle to determine
    /// if it qualifies for an Epic Bond breakthrough beyond daily dialogue caps.
    /// </summary>
    public static class BattleSignificanceEvaluator
    {
        public static BattleBondResult EvaluateCompanionBond(
            BattleRecord record,
            Hero companion,
            ModConfig? config)
        {
            var result = new BattleBondResult { IsEpic = false, BonusShift = 0, Reason = string.Empty };
            if (record == null || companion == null || record.Outcome != BattleRecord.Outcomes.Victory)
                return result;

            float oddsRatio = config?.EpicBattleOddsRatio ?? 2.0f;
            int ourTotal = Math.Max(1, record.Ours.Total);
            int theirTotal = record.Theirs.Total;
            float actualOdds = (float)theirTotal / ourTotal;

            var part = record.ParticipantById(companion.StringId);
            bool companionFell = part != null && (part.State == BattleParticipant.States.Fell || part.State == BattleParticipant.States.Wounded);
            bool playerFell = record.Player != null && (record.Player.State == BattleParticipant.States.Fell || record.Player.State == BattleParticipant.States.Wounded);
            bool hadLosses = record.Ours.Fallen > 0 || record.Ours.Wounded > 0;

            // Tier 1: Personal Lifesaver Rescue (Companion knocked down in dangerous/outnumbered struggle and saved)
            if (companionFell && (ourTotal <= 4 || actualOdds >= oddsRatio))
            {
                result.IsEpic = true;
                result.BonusShift = 7;
                result.Reason = $"fell wounded in the desperate fighting near {record.PlaceName} and was saved from death and captivity";
                return result;
            }

            // Tier 2: Captain Downed / Desperate Bloodshed (Player fell, or heavy bloodshed against 2.0x odds)
            if (playerFell || (actualOdds >= oddsRatio && hadLosses))
            {
                result.IsEpic = true;
                result.BonusShift = 5;
                result.Reason = playerFell
                    ? $"stood firm when the captain fell and fought through to victory near {record.PlaceName}"
                    : $"shared a bloody victory against overwhelming odds ({theirTotal} against our {ourTotal}) near {record.PlaceName}";
                return result;
            }

            // Tier 3: Major Lord Army or Siege Clash (Sieges or large field clashes)
            bool isSiege = record.Kind == BattleRecord.Kinds.Siege || record.Kind == BattleRecord.Kinds.Sally;
            bool isMajorArmy = theirTotal >= 40 && (record.EnemyName.IndexOf("lord", StringComparison.OrdinalIgnoreCase) >= 0 || record.EnemyName.IndexOf("clan", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isSiege || isMajorArmy)
            {
                result.IsEpic = true;
                result.BonusShift = 4;
                result.Reason = isSiege
                    ? $"fought side by side at the storming of {record.PlaceName}"
                    : $"shared the triumph over {record.EnemyName} near {record.PlaceName}";
                return result;
            }

            // Tier 4: Routine Skirmish / Steamroll (Looters or small bands) -> standard dialogue track
            return result;
        }
    }
}
