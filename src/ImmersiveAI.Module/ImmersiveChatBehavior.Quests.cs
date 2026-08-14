using System;
using ImmersiveAI.Core;
using ImmersiveAI.Tools;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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
            var issueTitle = issue?.Title?.ToString() ?? "Unknown";
            ModLog.Info($"[QuestBridge] LLM called accept_quest for {npc?.Name} (Available issue: '{issueTitle}')");

            if (issue != null && quest != null)
            {
                quest.Npc = npc;
                quest.AcceptedIssue = issue;
                return "The agreement is struck. The task is officially given into their hands. I speak on in my own words, thanking them or giving parting advice.";
            }
            ModLog.Warn($"[QuestBridge] accept_quest called for {npc?.Name}, but no available issue was found.");
            return "No troubled matter is presently available to give.";
        }

        private string ResolveReportQuest(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? quest)
        {
            var activeQuest = QuestTool.GetActiveQuest(npc);
            var questTitle = activeQuest?.Title?.ToString() ?? "Unknown";
            ModLog.Info($"[QuestBridge] LLM called report_quest for {npc?.Name} (Active quest: '{questTitle}')");

            if (activeQuest != null && quest != null)
            {
                quest.Npc = npc;
                quest.ReportedQuest = activeQuest;
                return "I acknowledge the completion of the deed with gratitude. I speak on in my own words, offering our thanks and rewards.";
            }
            ModLog.Warn($"[QuestBridge] report_quest called for {npc?.Name}, but no active quest was found.");
            return "No ongoing task was found.";
        }

        private void DispatchQuestOutcomes(QuestTool.Tally? quest)
        {
            if (quest == null) return;

            if (quest.AcceptedIssue != null)
            {
                var issueToStart = quest.AcceptedIssue;
                var npc = quest.Npc ?? issueToStart.IssueOwner;
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var title = issueToStart.Title?.ToString() ?? "Quest";
                        ModLog.Info($"[QuestBridge] Starting quest '{title}' for {npc?.Name} via IssueManager...");

                        bool ok = false;
                        if (issueToStart.IssueQuest == null)
                        {
                            ok = issueToStart.StartIssueWithQuest();
                        }

                        var quest = issueToStart.IssueQuest ?? (npc != null ? QuestTool.GetActiveQuest(npc) : null);
                        if (quest == null && Campaign.Current?.IssueManager != null && npc != null)
                        {
                            ok = Campaign.Current.IssueManager.StartIssueQuest(npc);
                            quest = QuestTool.GetActiveQuest(npc);
                        }

                        if (quest != null)
                        {
                            if (Campaign.Current?.QuestManager != null && !Campaign.Current.QuestManager.Quests.Contains(quest))
                            {
                                quest.StartQuest();
                            }

                            try
                            {
                                var setDialogsMethod = typeof(QuestBase).GetMethod("SetDialogs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                setDialogsMethod?.Invoke(quest, null);
                            }
                            catch { }

                            if (quest.JournalEntries == null || quest.JournalEntries.Count == 0)
                            {
                                var desc = issueToStart.Description ?? issueToStart.IssueQuestSolutionExplanationByIssueGiver ?? issueToStart.Title;
                                if (desc != null)
                                {
                                    quest.AddLog(desc);
                                }
                            }

                            ok = true;
                        }

                        if (ok)
                        {
                            ModLog.Info($"[QuestBridge] Successfully started quest: '{title}' for {npc?.Name}");
                            InformationManager.DisplayMessage(
                                new InformationMessage($"Quest Started: {title}", new Color(0.4f, 0.9f, 0.4f, 1f)));
                        }
                        else
                        {
                            ModLog.Warn($"[QuestBridge] Failed to start quest '{title}' for {npc?.Name} (StartIssueQuest returned false)");
                            InformationManager.DisplayMessage(
                                new InformationMessage($"Could not start quest: {title}", new Color(0.9f, 0.4f, 0.4f, 1f)));
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("starting quest via dialogue", ex);
                        InformationManager.DisplayMessage(
                            new InformationMessage($"Quest Error: {ex.Message}", new Color(0.9f, 0.3f, 0.3f, 1f)));
                    }
                });
            }

            if (quest.ReportedQuest != null)
            {
                var questToReport = quest.ReportedQuest;
                var npc = quest.Npc ?? questToReport.QuestGiver;
                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        if (questToReport != null && !questToReport.IsFinalized)
                        {
                            var title = questToReport.Title?.ToString() ?? "Quest";
                            ModLog.Info($"[QuestBridge] Completing quest with success: '{title}' for {npc?.Name}");
                            questToReport.CompleteQuestWithSuccess();
                            InformationManager.DisplayMessage(
                                new InformationMessage($"Quest Completed: {title}", new Color(0.95f, 0.85f, 0.35f, 1f)));
                        }
                        else
                        {
                            ModLog.Warn($"[QuestBridge] Cannot complete quest for {npc?.Name}: quest is null or already finalized.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("completing quest via dialogue", ex);
                        InformationManager.DisplayMessage(
                            new InformationMessage($"Quest Error: {ex.Message}", new Color(0.9f, 0.3f, 0.3f, 1f)));
                    }
                });
            }
        }
    }
}
