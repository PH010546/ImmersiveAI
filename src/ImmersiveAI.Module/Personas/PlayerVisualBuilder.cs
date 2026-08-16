using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Builds the visual appearance and equipped gear of the main player character,
    /// combining custom user description (player_description.txt) with dynamic game engine
    /// body properties (physique) and currently equipped armor/weapons.
    /// </summary>
    public static class PlayerVisualBuilder
    {
        public static string Build(Hero player)
        {
            if (player == null) return string.Empty;

            var sb = new StringBuilder();

            // 1. Custom Player Description (bearing, tokens, scars, accents from player_description.txt)
            try
            {
                string custom = PromptFiles.LoadPlayerDescription()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(custom))
                {
                    sb.Append(custom);
                    if (!custom.EndsWith(".") && !custom.EndsWith("!") && !custom.EndsWith("?"))
                        sb.Append(".");
                }
            }
            catch { }

            // 2. Physical build from BodyProperties (physique, musculature)
            try
            {
                var bp = player.BodyProperties;
                float build = bp.Build;
                float weight = bp.Weight;
                string? buildWord = null;
                if (build > 0.65f && weight > 0.65f) buildWord = "a powerfully built, broad-shouldered frame";
                else if (build > 0.65f) buildWord = "an athletic, muscular frame";
                else if (weight > 0.70f) buildWord = "a heavy, stout build";
                else if (build < 0.35f && weight < 0.35f) buildWord = "a lean, slender build";
                else if (build < 0.35f) buildWord = "a slight, lithe build";

                if (buildWord != null)
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append($"Has {buildWord}.");
                }
            }
            catch { }

            // 3. Dynamic Equipment currently worn
            try
            {
                var eq = (Mission.Current != null && Mission.Current.DoesMissionRequireCivilianEquipment)
                    ? player.CivilianEquipment
                    : (player.BattleEquipment ?? player.CivilianEquipment);

                if (eq != null)
                {
                    string? ItemAt(EquipmentIndex i) { try { return eq[i].Item?.Name?.ToString(); } catch { return null; } }

                    var armorPieces = new List<string>();
                    var head = ItemAt(EquipmentIndex.Head);
                    var body = ItemAt(EquipmentIndex.Body);
                    var cape = ItemAt(EquipmentIndex.Cape);

                    if (!string.IsNullOrWhiteSpace(head)) armorPieces.Add(head);
                    if (!string.IsNullOrWhiteSpace(body)) armorPieces.Add(body);
                    if (!string.IsNullOrWhiteSpace(cape)) armorPieces.Add(cape);

                    var arms = new[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 }
                        .Select(ItemAt).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

                    var horse = ItemAt(EquipmentIndex.Horse);

                    var gearParts = new List<string>();
                    if (armorPieces.Count > 0)
                        gearParts.Add($"wearing {JoinAnd(armorPieces)}");
                    if (arms.Count > 0)
                        gearParts.Add($"armed with {JoinAnd(arms)}");
                    if (!string.IsNullOrWhiteSpace(horse) && Mission.Current == null)
                        gearParts.Add($"mounted on {A(horse!)} {horse}");

                    if (gearParts.Count > 0)
                    {
                        if (sb.Length > 0) sb.Append(" ");
                        sb.Append("Presently " + string.Join(", ", gearParts) + ".");
                    }
                }
            }
            catch { }

            return sb.ToString().Trim();
        }

        private static string A(string word)
        {
            if (string.IsNullOrEmpty(word)) return "a";
            return "aeiou".IndexOf(char.ToLowerInvariant(word[0])) >= 0 ? "an" : "a";
        }

        private static string JoinAnd(List<string> items)
        {
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return items[0] + " and " + items[1];
            return string.Join(", ", items.Take(items.Count - 1)) + ", and " + items[items.Count - 1];
        }
    }
}
