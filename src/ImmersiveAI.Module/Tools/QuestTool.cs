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
            public int RequiredGold;
        }

        public static readonly ToolDefinition AcceptTool = new ToolDefinition(AcceptQuest,
            "Formally hand over my spoken issue or task to the traveler who has agreed in words to take it upon themselves. " +
            "You MUST call this tool whenever the traveler offers or agrees to help or take the task, to seal the quest into the game engine.",
            new[]
            {
                new ToolParameter("confirmation", "A brief phrase confirming the task agreed upon.", required: false)
            });

        public static readonly ToolDefinition ReportTool = new ToolDefinition(ReportQuest,
            "Acknowledge the completion of the ongoing quest that the traveler rode upon for me, " +
            "granting them our gratitude and rewards in the game engine.",
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
                    .FirstOrDefault(q => q.QuestGiver == npc && !q.IsFinalized);
            }
            catch { return null; }
        }
    }
}
