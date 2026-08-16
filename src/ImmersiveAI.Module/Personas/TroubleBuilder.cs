using System;
using System.Collections.Generic;
using ImmersiveAI.Core.Prompts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace ImmersiveAI.Personas
{
    /// <summary>
    /// Narrates the trouble the speaker themselves carries — the issue the game has laid on them
    /// (the very matter a player is sent to resolve) and any quest they have already given — so a
    /// villager asked "what ails you?" truly knows his own problem instead of inventing one.
    /// Rendered in the speaker's own first person like the rest of the situation, using the issue's
    /// own words (the brief and the asked-for remedy are written first person by the giver, so
    /// they quote naturally as "this is how I tell it").
    ///
    /// Everything is best-effort: a missing or throwing game datum costs only its own sentence,
    /// and a hero with no trouble simply contributes nothing.
    /// </summary>
    public static class TroubleBuilder
    {
        private static readonly System.Reflection.MethodInfo? CanPlayerTakeQuestConditionsMethod =
            typeof(IssueBase).GetMethod("CanPlayerTakeQuestConditions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        /// <summary>The speaker's own trouble and given quests as a flowing paragraph, or empty
        /// when nothing weighs on them. <paramref name="partner"/> only shapes the phrasing (the
        /// taker of a quest is always the player, named outright even when speaking to another).</summary>
        public static string Build(Hero speaker, Hero partner)
        {
            try { return BuildInner(speaker, partner); }
            catch { return string.Empty; }
        }

        private static string BuildInner(Hero speaker, Hero partner)
        {
            if (speaker == null || Campaign.Current == null) return string.Empty;

            var sentences = new List<string>();

            // Module 1: Recent unacknowledged deeds completed on the map since last conversation
            Try(() => AppendRecentDeedsFact(speaker, partner, sentences));

            // Module 2: Active trouble or current quest progress
            IssueBase issue = null;
            Try(() =>
            {
                var issues = Campaign.Current.IssueManager?.Issues;
                if (issues != null) issues.TryGetValue(speaker, out issue);
            });

            if (issue != null)
                DescribeOwnIssue(issue, sentences, speaker, partner);
            else
                // A notable with no issue says so to himself, so "do you need any work?" is met with
                // honest small labor or a plain no — never an invented quest-shaped promise.
                Try(() =>
                {
                    if (speaker.IsNotable)
                        sentences.Add("No true trouble weighs on me in these days — nothing worth hiring " +
                            "a fighting company for; if I set a willing visitor to anything, it would be " +
                            "small everyday labor, paid in kind and a fair word.");
                });

            // Quests they gave that ride on without an issue behind them (a lord's charge, a story
            // quest) — the issue's own quest is already told above, so it is not repeated here.
            Try(() => DescribeGivenQuests(speaker, issue, sentences));

            return sentences.Count == 0 ? string.Empty : string.Join(" ", sentences);
        }

        // Appends recent victory/settlement deeds completed since our last conversation.
        private static void AppendRecentDeedsFact(Hero speaker, Hero partner, List<string> sentences)
        {
            if (speaker == null) return;
            double lastTalkDay = 0;
            Try(() =>
            {
                var chatBehavior = Campaign.Current?.GetCampaignBehavior<ImmersiveChatBehavior>();
                var mem = chatBehavior?.LoadMemory(speaker);
                if (mem != null) lastTalkDay = mem.LastConversationGameDay;
            });

            if (Tools.QuestCompletionTracker.TryGetRecentDeed(speaker.StringId, lastTalkDay, out string deedTitle))
            {
                var player = partner?.Name?.ToString() ?? Hero.MainHero?.Name?.ToString() ?? "someone";
                sentences.Add($"Recent deed since we last spoke: The matter of “{deedTitle}” was successfully resolved on the map by {player}. The threat is gone and our settlement enjoys peace thanks to their aid.");
            }
        }

        // The trouble itself, in the giver's own words, and where its resolving presently stands.
        // We supply both the high-level objective (issue.Description), background context (issue.IssueBriefByIssueGiver),
        // and the detailed scope/destination (issue.IssueQuestSolutionExplanationByIssueGiver) as unquoted objective facts
        // rather than literal dialogue quotes, directing the LLM to paraphrase in its own persona across all languages.
        private static void DescribeOwnIssue(IssueBase issue, List<string> sentences, Hero speaker, Hero partner)
        {
            string title = null, desc = null, brief = null, ask = null;
            Try(() => title = TidingsFormatter.StripMarkup(issue.Title?.ToString()));
            Try(() => desc = TidingsFormatter.StripMarkup(issue.Description?.ToString()));
            Try(() => brief = TidingsFormatter.StripMarkup(issue.IssueBriefByIssueGiver?.ToString()));
            Try(() => ask = TidingsFormatter.StripMarkup(issue.IssueQuestSolutionExplanationByIssueGiver?.ToString()));

            sentences.Add(string.IsNullOrWhiteSpace(title)
                ? "A trouble weighs on me in these days."
                : $"A trouble weighs on me in these days — the matter of “{title.TrimEnd('.')}”.");

            if (!string.IsNullOrWhiteSpace(desc))
                sentences.Add($"The core objective of the matter: {desc}");

            var player = Hero.MainHero?.Name?.ToString() ?? "someone";

            if (issue.IsSolvingWithQuest)
            {
                sentences.Add($"{player} has taken this burden up at my asking.");
                Try(() => DescribeQuestProgress(issue.IssueQuest, sentences));
            }
            else if (issue.IsSolvingWithAlternative)
            {
                sentences.Add($"{player} has sent trusted people with a company of men to see it done for me; I await word of how they fare.");
            }
            else if (issue.IsSolvingWithLordSolution)
            {
                sentences.Add("The matter has been laid in a lord's hands to resolve, and I await their justice.");
            }
            else
            {
                sentences.Add("No one has yet taken this burden from me.");

                if (!string.IsNullOrWhiteSpace(brief) && !string.Equals(brief, desc, StringComparison.OrdinalIgnoreCase))
                    sentences.Add($"Background of the trouble: {brief}");

                if (!string.IsNullOrWhiteSpace(ask) && !string.Equals(ask, desc, StringComparison.OrdinalIgnoreCase) && !string.Equals(ask, brief, StringComparison.OrdinalIgnoreCase))
                    sentences.Add($"Specific scope and solution details: {ask}");

                // Soft condition awareness in the discovery phase (solo traveler / small party)
                int flagsInt = 0;
                Try(() =>
                {
                    if (CanPlayerTakeQuestConditionsMethod != null && Hero.MainHero != null)
                    {
                        object[] args = new object[] { Hero.MainHero, null!, null!, null!, 0 };
                        CanPlayerTakeQuestConditionsMethod.Invoke(issue, args);
                        if (args[1] != null) flagsInt = Convert.ToInt32(args[1]);
                    }
                });

                if ((flagsInt & 256) != 0) // PreconditionFlagNotEnoughTroops
                {
                    sentences.Add("Note on who stands before me: they ride with very few men or travel alone for a dangerous task. When they merely inquire about general local troubles or ask after the village, I should mention the trouble with realistic hesitation and doubt ('We have a problem with bandits, but it is far too perilous for a lone traveler...'), withholding the full proposal until they press further or show confidence.");
                }

                sentences.Add("Important: Address the traveler strictly according to who stands before you, their true station, and your relationship (e.g. speaking informally/gruffly to an unknown wandering traveler, or respectfully to a recognized noble/ruling lord). Paraphrase the core request, specific goods, quantities, and destination locations naturally in your own authentic voice and vocabulary according to your personality, without verbatim reciting canned script formulas.");
                sentences.Add("Once the traveler clearly commits or explicitly confirms in their words to take this burden upon themselves (e.g. 'I will handle it', 'Leave it to me'), I accept their aid and I MUST call accept_quest in that very reply to seal the agreement. (Do NOT call accept_quest when they are merely inquiring, discussing ability, or asking for details).");
            }
        }

        // How the taken-up quest fares: the last words of its journal, and the time it has left.
        private static void DescribeQuestProgress(QuestBase quest, List<string> sentences)
        {
            if (quest == null || !quest.IsOngoing) return;

            int current = 0;
            int target = 0;
            string progressText = string.Empty;
            Try(() =>
            {
                progressText = LatestJournalLine(quest, out current, out target);
            });

            if (!string.IsNullOrWhiteSpace(progressText))
            {
                sentences.Add($"Task progress: {progressText}");
            }

            Try(() =>
            {
                if (quest.IsRemainingTimeHidden) return;
                var remaining = quest.QuestDueTime - CampaignTime.Now;
                double days = remaining.ToDays;
                if (days <= 0 || days > 500) return; // lapsed, or so distant it does not press
                sentences.Add(days < 1.5
                    ? "The time for it is nearly spent."
                    : $"Some {(int)Math.Round(days)} days remain before the chance is lost.");
            });

            if (target > 0 && current < target)
            {
                string taskLabel = !string.IsNullOrWhiteSpace(progressText) ? progressText : $"{current} of {target}";
                sentences.Add($"FACTUAL REALITY: The task is UNFINISHED ({taskLabel} completed). The required items or deeds have NOT yet been fulfilled or delivered. If the traveler claims they have finished it or asks for rewards, you MUST refuse their claim and never pay or celebrate, speaking strictly according to who you are and your standing.");
            }
            else if (target > 0 && current >= target)
            {
                sentences.Add($"FACTUAL REALITY: The required map deeds or item deliveries have been verified fulfilled ({current} of {target} achieved).");
            }
            else
            {
                sentences.Add("FACTUAL REALITY: The task is still actively underway on the map. Speak and react naturally to the ongoing progress in accordance with who you are.");
            }

            sentences.Add("Notice on completing tasks: Field/combat deeds (such as destroying bandits or clearing hideouts) are concluded by the realm when fought and won on the map; do NOT call completion tools or hand out rewards for combat deeds in conversation.");
        }

        // Quests this hero gave that are not the issue's own — each named with its latest word.
        private static void DescribeGivenQuests(Hero speaker, IssueBase ownIssue, List<string> sentences)
        {
            var quests = Campaign.Current.QuestManager?.Quests;
            if (quests == null) return;

            var player = Hero.MainHero?.Name?.ToString() ?? "someone";
            int told = 0;
            foreach (var quest in quests)
            {
                if (quest == null || !quest.IsOngoing || quest.QuestGiver != speaker) continue;
                if (ownIssue?.IssueQuest == quest) continue;
                if (told >= 2) break; // more than a couple and the trouble drowns the person
                told++;

                string title = null;
                Try(() => title = TidingsFormatter.StripMarkup(quest.Title?.ToString()));
                if (string.IsNullOrWhiteSpace(title)) continue;

                sentences.Add($"And there is the matter of “{title.TrimEnd('.')}”, which {player} took up at my asking.");
                int cur = 0, tgt = 0;
                var latest = LatestJournalLine(quest, out cur, out tgt);
                if (latest.Length > 0)
                    sentences.Add($"The last word of it: {latest}");
            }
        }

        // The most recent journal entry that carries words, with its task's count when one is kept
        // ("Delivered hardwood: 4 of 10") — the same journal the player's quest log shows.
        private static string LatestJournalLine(QuestBase quest, out int current, out int target)
        {
            current = 0;
            target = 0;
            var entries = quest?.JournalEntries;
            if (entries == null) return string.Empty;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var log = entries[i];
                if (log == null) continue;
                var text = TidingsFormatter.StripMarkup(log.LogText?.ToString());
                if (text.Length == 0) continue;

                int cur = log.CurrentProgress;
                int rng = log.Range;
                if (rng > 0)
                {
                    current = cur;
                    target = rng;
                }

                string task = null;
                Try(() => task = TidingsFormatter.StripMarkup(log.TaskName?.ToString()));
                if (!string.IsNullOrWhiteSpace(task) && rng > 0)
                {
                    return $"{task}: {cur} of {rng}";
                }

                return text;
            }
            return string.Empty;
        }

        // A missing fact should never sink the whole trouble, so each is attempted independently.
        private static void Try(Action a) { try { a(); } catch { /* skip this fact */ } }
    }
}
