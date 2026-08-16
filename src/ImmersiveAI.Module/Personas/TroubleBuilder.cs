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

        // The trouble itself, in the giver's own words, and where its resolving presently stands.
        private static void DescribeOwnIssue(IssueBase issue, List<string> sentences, Hero speaker, Hero partner)
        {
            string title = null, desc = null;
            Try(() => title = TidingsFormatter.StripMarkup(issue.Title?.ToString()));
            Try(() => desc = TidingsFormatter.StripMarkup(issue.Description?.ToString()));

            if (string.IsNullOrWhiteSpace(desc))
            {
                string ask = null;
                Try(() => ask = TidingsFormatter.StripMarkup(issue.IssueQuestSolutionExplanationByIssueGiver?.ToString()));
                if (!string.IsNullOrWhiteSpace(ask))
                    desc = CleanScriptedDialogue(ask);
            }
            else
            {
                desc = CleanScriptedDialogue(desc);
            }

            sentences.Add(string.IsNullOrWhiteSpace(title)
                ? "A trouble weighs on me in these days."
                : $"A trouble weighs on me in these days — the matter of “{title.TrimEnd('.')}”.");

            if (!string.IsNullOrWhiteSpace(desc))
                sentences.Add($"The core truth of the matter: {desc}");

            var player = Hero.MainHero?.Name?.ToString() ?? "someone";

            if (issue.IsSolvingWithQuest)
            {
                sentences.Add($"{player} has taken this burden up at my asking.");
                if (!string.IsNullOrWhiteSpace(desc))
                    sentences.Add($"What was asked of them: {desc}");
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
                if (!string.IsNullOrWhiteSpace(desc))
                    sentences.Add($"What is needed to resolve it: {desc}");

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

                sentences.Add("Important: Address the traveler strictly according to who stands before you, their true station, and your relationship (e.g. speaking informally/gruffly to an unknown wandering traveler, or respectfully to a recognized noble/ruling lord). Explain the trouble in your own authentic voice and vocabulary without copying canned script lines or fixed formulas.");
                sentences.Add("Once the traveler clearly commits or explicitly confirms in their words to take this burden upon themselves (e.g. 'I will handle it', 'Leave it to me'), I accept their aid and I MUST call accept_quest in that very reply to seal the agreement. (Do NOT call accept_quest when they are merely inquiring, discussing ability, or asking for details).");
            }
        }

        // How the taken-up quest fares: the last words of its journal, and the time it has left.
        private static void DescribeQuestProgress(QuestBase quest, List<string> sentences)
        {
            if (quest == null || !quest.IsOngoing) return;

            int current = 0;
            int target = 0;
            Try(() =>
            {
                var latest = LatestJournalLine(quest, out current, out target);
                if (latest.Length > 0)
                    sentences.Add($"The last word of how it fares: {latest}");
            });

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

            if (target > 0)
            {
                if (current < target)
                {
                    sentences.Add($"Ground truth known to me and local scouts: The task is NOT yet completed ({current} of {target} achieved). The enemies or troubles are STILL actively present out there. If the traveler claims they have already finished it, I know they are mistaken, boasting, or lying, and I react accordingly in character.");
                }
                else
                {
                    sentences.Add($"Ground truth: The required deed has been verified fulfilled on the map ({current} of {target} achieved).");
                }
            }

            sentences.Add("Important: Speak and react to the ongoing progress naturally in accordance with who you are and your standing with the traveler.");
            sentences.Add("Notice on completing tasks: Field/combat deeds (such as destroying bandits or clearing hideouts) are concluded by the realm when fought and won on the map; do NOT call completion tools for combat deeds in conversation. Only when the traveler actually hands over physical goods or items from inventory for a delivery task should report_quest be called.");
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

                Try(() =>
                {
                    var task = TidingsFormatter.StripMarkup(log.TaskName?.ToString());
                    if (task.Length > 0 && log.Range > 0)
                        text = $"{text} ({task}: {log.CurrentProgress} of {log.Range})";
                });
                return text;
            }
            return string.Empty;
        }

        private static string CleanScriptedDialogue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var cleaned = text.Trim();
            string[] openings = new[] { "這樣說吧，先生，", "這樣說吧，先生", "這樣說吧，大人，", "這樣說吧，大人", "先生，", "大人，", "Well sir, ", "Well, my Lord, ", "My Lord, ", "Sir, " };
            foreach (var op in openings)
            {
                if (cleaned.StartsWith(op, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(op.Length).Trim();
                    break;
                }
            }
            string[] closings = new[] { "您願意幫助我們嗎？", "您願意幫忙嗎？", "你願意幫助我們嗎？", "Will you help us?", "Will you help me?" };
            foreach (var cl in closings)
            {
                if (cleaned.EndsWith(cl, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - cl.Length).Trim();
                    break;
                }
            }
            return cleaned;
        }

        // A missing fact should never sink the whole trouble, so each is attempted independently.
        private static void Try(Action a) { try { a(); } catch { /* skip this fact */ } }
    }
}
