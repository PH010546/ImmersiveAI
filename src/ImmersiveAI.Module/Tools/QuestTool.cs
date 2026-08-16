using System;
using System.Linq;
using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace ImmersiveAI.Tools
{
    /// <summary>
    /// Connects conversational dialogue directly to native Bannerlord Issues and Quests.
    /// When an NPC (Village Notable, Town Artisan, Merchant, or Lord) has an issue or active quest,
    /// this tool allows the NPC to formally hand over the quest or accept its completion in dialogue.
    /// </summary>
    public static class QuestTool
    {
        public const string AcceptQuest = "accept_quest";
        public const string ReportQuest = "report_quest";

        public sealed class Tally
        {
            public Hero? Npc;
            public IssueBase? AcceptedIssue;
            public QuestBase? ReportedQuest;
            public System.Reflection.MethodInfo? CompletionMethod;
            public int RequiredGold;
        }

        public static readonly ToolDefinition AcceptTool = new ToolDefinition(AcceptQuest,
            "Formally hand over my spoken issue or task to the traveler only after they have clearly and explicitly committed in words to take it upon themselves (e.g. 'I will do it', 'Leave it to me'). " +
            "Do NOT call this when they are merely inquiring, discussing possibilities, or stating their skills.",
            new[]
            {
                new ToolParameter("confirmation", "A brief phrase confirming the task agreed upon.", required: false)
            });

        public static readonly ToolDefinition ReportTool = new ToolDefinition(ReportQuest,
            "Acknowledge the handover and completion of a physical delivery-type quest when the traveler has brought and delivered the required items from inventory. " +
            "(On-map combat deeds such as destroying bandits or clearing hideouts are concluded automatically when the battle is won on the map; do not call this tool for combat tasks).",
            new[]
            {
                new ToolParameter("result", "Confirmation of the quest result.", required: false)
            });

        public static IssueBase? GetAvailableIssue(Hero npc)
        {
            try
            {
                if (npc == null || Campaign.Current?.IssueManager == null) return null;
                if (Campaign.Current.IssueManager.Issues.TryGetValue(npc, out var issue) && issue != null)
                {
                    if (!issue.IsSolvingWithQuest && !issue.IsSolvingWithAlternative && !issue.IsSolvingWithLordSolution)
                        return issue;
                }
                return null;
            }
            catch { return null; }
        }

        public static QuestBase? GetActiveQuest(Hero npc)
        {
            try
            {
                if (npc == null || Campaign.Current?.QuestManager == null) return null;
                return Campaign.Current.QuestManager.Quests
                    .FirstOrDefault(q => (q.QuestGiver == npc || IsQuestTargetHero(q, npc)) && !q.IsFinalized);
            }
            catch { return null; }
        }

        public static bool IsQuestTargetHero(QuestBase? q, Hero? npc)
        {
            if (q == null || npc == null) return false;
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var targetHero = (q.GetType().GetField("_targetHero", flags) ?? q.GetType().GetField("_destinationHero", flags) ?? q.GetType().GetField("_recipientHero", flags))?.GetValue(q) as Hero;
                return targetHero == npc;
            }
            catch { return false; }
        }
    }
}
