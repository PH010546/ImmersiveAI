using System;
using ImmersiveAI.Core;
using ImmersiveAI.Tools;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    public partial class ImmersiveChatBehavior
    {
        private bool CanBridgeQuests(Hero npc)
        {
            if (!_config.EnableQuestDialogueBridge) return false;
            if (npc == null || !npc.IsAlive) return false;
            return QuestTool.GetAvailableIssue(npc) != null || QuestTool.GetActiveQuest(npc) != null;
        }

        private string ResolveAcceptQuest(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? quest)
        {
            var issue = QuestTool.GetAvailableIssue(npc);
            if (issue != null && quest != null)
            {
                quest.AcceptedIssue = issue;
                return "The agreement is struck. The task is officially given into their hands. I speak on in my own words, thanking them or giving parting advice.";
            }
            return "No troubled matter is presently available to give.";
        }

        private string ResolveReportQuest(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? quest)
        {
            var activeQuest = QuestTool.GetActiveQuest(npc);
            if (activeQuest != null && quest != null)
            {
                quest.ReportedQuest = activeQuest;
                return "I acknowledge the completion of the deed with gratitude. I speak on in my own words, offering our thanks and rewards.";
            }
            return "No ongoing task was found.";
        }

        private void DispatchQuestOutcomes(QuestTool.Tally? quest)
        {
            if (quest == null) return;

            if (quest.AcceptedIssue != null)
            {
                var issueToStart = quest.AcceptedIssue;
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        if (issueToStart != null && issueToStart.IsInitialized)
                        {
                            bool ok = issueToStart.StartIssueWithQuest();
                            var title = issueToStart.Title?.ToString() ?? "Quest";
                            if (ok)
                            {
                                InformationManager.DisplayMessage(
                                    new InformationMessage($"Quest Started: {title}", new Color(0.4f, 0.9f, 0.4f, 1f)));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("starting quest via dialogue", ex);
                    }
                });
            }

            if (quest.ReportedQuest != null)
            {
                var questToReport = quest.ReportedQuest;
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        if (questToReport != null && !questToReport.IsFinalized)
                        {
                            questToReport.CompleteQuestWithSuccess();
                            var title = questToReport.Title?.ToString() ?? "Quest";
                            InformationManager.DisplayMessage(
                                new InformationMessage($"Quest Completed: {title}", new Color(0.95f, 0.85f, 0.35f, 1f)));
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("completing quest via dialogue", ex);
                    }
                });
            }
        }
    }
}
