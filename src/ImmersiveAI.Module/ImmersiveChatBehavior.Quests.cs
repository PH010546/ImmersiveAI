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

        // Precondition flags defined by TaleWorlds.CampaignSystem.Issues.IssueBase.PreconditionFlags
        private const int PreconditionFlagNone = 0;
        private const int PreconditionFlagNotEnoughGold = 1;
        private const int PreconditionFlagNotEnoughHealth = 2;
        private const int PreconditionFlagNotEnoughSkill = 4;
        private const int PreconditionFlagNotEnoughRenown = 8;
        private const int PreconditionFlagNotEnoughTroops = 16;
        private const int PreconditionFlagPartySizeLimitExceeded = 32;
        private const int PreconditionFlagClanIsAtWar = 64;
        private const int PreconditionFlagMainHeroIsPrisoner = 128;
        private const int PreconditionFlagHasOngoingQuest = 256;
        private const int PreconditionFlagWounded = 512;

        private static readonly System.Reflection.MethodInfo? CanPlayerTakeQuestConditionsMethod =
            typeof(IssueBase).GetMethod("CanPlayerTakeQuestConditions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        private string ResolveAcceptQuest(Core.Llm.ToolCall call, Hero npc, QuestTool.Tally? quest)
        {
            var issue = QuestTool.GetAvailableIssue(npc);
            var issueTitle = issue?.Title?.ToString() ?? "Unknown";
            ModLog.Info($"[QuestBridge] LLM called accept_quest for {npc?.Name} (Available issue: '{issueTitle}')");

            if (issue == null || quest == null)
            {
                ModLog.Warn($"[QuestBridge] accept_quest called for {npc?.Name}, but no available issue was found.");
                return "No troubled matter is presently available to give.";
            }

            // Three-tier Precondition Check
            int flagsInt = 0;
            int reqGold = 0;
            try
            {
                if (CanPlayerTakeQuestConditionsMethod != null && Hero.MainHero != null)
                {
                    object[] args = new object[] { Hero.MainHero, null!, null!, null!, 0 };
                    bool canTake = (bool)CanPlayerTakeQuestConditionsMethod.Invoke(issue, args);
                    if (args[1] != null) flagsInt = Convert.ToInt32(args[1]);
                    if (args[4] != null) reqGold = Convert.ToInt32(args[4]);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[QuestBridge] Could not evaluate CanPlayerTakeQuestConditions reflectively: {ex.Message}");
            }

            // 1. Hard Block (physical/economic impossibility)
            if ((flagsInt & PreconditionFlagNotEnoughGold) != 0 || (reqGold > 0 && Hero.MainHero?.Gold < reqGold))
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Not enough gold (Requires {reqGold}, Player has {Hero.MainHero?.Gold})");
                return $"I cannot entrust this task to you: you lack the required {reqGold} gold to cover the cost or deposit. Speak to me again when you have the funds.";
            }
            if ((flagsInt & PreconditionFlagMainHeroIsPrisoner) != 0 || (Hero.MainHero != null && Hero.MainHero.IsPrisoner))
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Player is prisoner.");
                return "You are currently a captive and cannot undertake tasks.";
            }
            if ((flagsInt & PreconditionFlagClanIsAtWar) != 0)
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Faction at war.");
                return "Our realms are at open war. I will not conspire with an enemy.";
            }
            if ((flagsInt & PreconditionFlagPartySizeLimitExceeded) != 0)
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Party limit exceeded.");
                return "Your party is at its absolute limit and cannot take on more men or supplies for this task.";
            }

            // 2. Conflict (competing allegiance/active conflicting quest)
            if ((flagsInt & PreconditionFlagHasOngoingQuest) != 0)
            {
                ModLog.Info($"[QuestBridge] Refusing accept_quest for {npc.Name}: Has conflicting ongoing quest.");
                try
                {
                    TaleWorlds.CampaignSystem.Actions.ChangeRelationAction.ApplyPlayerRelation(npc, -2);
                }
                catch { }
                return "You are already bound by a conflicting task or working for my rivals. I cannot trust you with this business.";
            }

            // 3. Physical Distance Boundary (Remote Letter / Distant Chat requiring physical goods or upfront coin)
            bool isDistant = (Hero.MainHero?.CurrentSettlement == null || npc.CurrentSettlement == null || Hero.MainHero.CurrentSettlement != npc.CurrentSettlement);
            if (isDistant && reqGold > 0)
            {
                ModLog.Info($"[QuestBridge] Refusing remote accept_quest for {npc.Name}: Physical goods/deposit requires in-person transaction.");
                return "This matter involves large sums of coin and heavy goods that cannot be entrusted to a courier. Please come speak with me in person at my settlement so we may settle terms face to face.";
            }

            // 4. Soft Conditions (Alone / Injured / Low Renown / Low Skill) -> Allow for roleplay freedom & solo players!
            quest.Npc = npc;
            quest.AcceptedIssue = issue;
            quest.RequiredGold = reqGold;

            if ((flagsInt & PreconditionFlagNotEnoughTroops) != 0)
            {
                ModLog.Info($"[QuestBridge] Player taking quest with small/solo party ({npc.Name}). Allowing solo player freedom.");
            }

            return "The agreement is struck. The task is officially given into their hands. I speak on in my own words, thanking them, giving parting advice, or noting their courage.";
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
                int reqGold = quest.RequiredGold;

                MainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        var title = issueToStart.Title?.ToString() ?? "Quest";
                        ModLog.Info($"[QuestBridge] Formally starting quest '{title}' for {npc?.Name} via native dialogue pipeline...");

                        bool ok = false;

                        // 1. Generate the quest instance via native issue mechanism
                        if (issueToStart.IssueQuest == null)
                        {
                            issueToStart.StartIssueWithQuest();
                        }

                        var activeQuest = issueToStart.IssueQuest ?? (npc != null ? QuestTool.GetActiveQuest(npc) : null);
                        if (activeQuest == null && Campaign.Current?.IssueManager != null && npc != null)
                        {
                            Campaign.Current.IssueManager.StartIssueQuest(npc);
                            activeQuest = QuestTool.GetActiveQuest(npc);
                        }

                        if (activeQuest != null)
                        {
                            // 2. Invoke the quest's native initialization / acceptance consequence method
                            // (Grants quest items, deducts upfront costs, sets up map tracking, spots hideouts, registers dialog flows)
                            try
                            {
                                var questType = activeQuest.GetType();
                                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                                var acceptMethod = questType.GetMethod("QuestAcceptedConsequences", flags)
                                    ?? questType.GetMethod("OnQuestAccepted", flags)
                                    ?? questType.GetMethod("QuestAcceptedByPlayerConsequences", flags);

                                if (acceptMethod != null)
                                {
                                    acceptMethod.Invoke(activeQuest, null);
                                    ModLog.Info($"[QuestBridge] Executed native quest acceptance consequence: {questType.Name}.{acceptMethod.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                ModLog.Warn($"[QuestBridge] Error invoking quest acceptance consequence: {ex.Message}");
                            }

                            // 3. Fallback: If consequence didn't start or register the quest into QuestManager, ensure it is started
                            if (Campaign.Current?.QuestManager != null && !Campaign.Current.QuestManager.Quests.Contains(activeQuest))
                            {
                                activeQuest.StartQuest();
                            }

                            try
                            {
                                var setDialogsMethod = typeof(QuestBase).GetMethod("SetDialogs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                setDialogsMethod?.Invoke(activeQuest, null);
                            }
                            catch { }

                            if (activeQuest.JournalEntries == null || activeQuest.JournalEntries.Count == 0)
                            {
                                var desc = issueToStart.Description ?? issueToStart.IssueQuestSolutionExplanationByIssueGiver ?? issueToStart.Title;
                                if (desc != null)
                                {
                                    activeQuest.AddLog(desc);
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
                            ModLog.Warn($"[QuestBridge] Failed to start quest '{title}' for {npc?.Name}");
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
