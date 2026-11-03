using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_FoundationPersonnel : Window
    {
        private readonly Map map;
        private Vector2 scroll;
        public override Vector2 InitialSize { get { return new Vector2(880f, 700f); } }

        public Dialog_FoundationPersonnel(Map map)
        {
            this.map = map;
            doCloseX = true;
            doCloseButton = false;
            forcePause = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            FoundationPersonnelComponent personnel = Current.Game.GetComponent<FoundationPersonnelComponent>();
            bool online = CompFoundationAccessTerminal.Online(map) != null;
            Text.Font = GameFont.Small;
            ScpTerminalUi.Panel(rect, ScpTerminalUi.Background);
            Rect header = new Rect(12f, 9f, rect.width - 24f, 54f);
            ScpTerminalUi.Panel(header);
            ScpTerminalUi.Label(header.ContractedBy(13f), "SCP_PersonnelTitle".Translate(),
                ScpTerminalUi.AccentColor, GameFont.Medium);
            Rect intro = new Rect(12f, 72f, rect.width - 24f, 60f);
            ScpTerminalUi.Panel(intro);
            ScpTerminalUi.Label(intro.ContractedBy(11f), "SCP_PersonnelDesc".Translate(),
                ScpTerminalUi.MainText, GameFont.Small, TextAnchor.UpperLeft);
            Rect statusPanel = new Rect(12f, 140f, rect.width - 24f, 31f);
            ScpTerminalUi.Panel(statusPanel);
            ScpTerminalUi.Label(new Rect(statusPanel.x + 11f, statusPanel.y + 2f, 240f, 26f),
                "SCP_PersonnelTrust".Translate(support.progress.trust), ScpTerminalUi.AccentColor);
            if (!online) ScpTerminalUi.Label(new Rect(statusPanel.x + 252f, statusPanel.y + 2f,
                statusPanel.width - 263f, 26f), "SCP_PersonnelOffline".Translate(), ScpTerminalUi.MutedText);

            var colonists = map.mapPawns.FreeColonistsSpawned.OrderBy(pawn => pawn.LabelShort).ToList();
            Rect view = new Rect(12f, 179f, rect.width - 24f, rect.height - 243f);
            ScpTerminalUi.Panel(view);
            float[] rowHeights = new float[colonists.Count];
            float totalHeight = 0f;
            for (int i = 0; i < colonists.Count; i++)
            {
                rowHeights[i] = RowHeight(view.width - 16f, colonists[i], personnel, support);
                totalHeight += rowHeights[i];
            }
            Rect content = new Rect(0f, 0f, view.width - 16f, Mathf.Max(view.height, totalHeight));
            Widgets.BeginScrollView(view, ref scroll, content);
            float rowY = 0f;
            for (int i = 0; i < colonists.Count; i++)
            {
                DrawPawn(new Rect(0f, rowY, content.width, rowHeights[i] - 8f), colonists[i], personnel, support, online);
                rowY += rowHeights[i];
            }
            Widgets.EndScrollView();
            if (colonists.Count == 0) ScpTerminalUi.Label(view.ContractedBy(16f),
                "SCP_PersonnelEmpty".Translate(), ScpTerminalUi.MutedText);
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 51f, 140f, 34f),
                "SCP_TerminalClose".Translate())) Close();
        }

        private static float RowHeight(float width, Pawn pawn, FoundationPersonnelComponent personnel,
            FoundationSupportComponent support)
        {
            int level = personnel.LevelFor(pawn);
            float leftWidth = width - 245f;
            float titleHeight = TextHeight(pawn.LabelShortCap + " — " + RankFor(pawn, personnel), leftWidth, GameFont.Medium);
            float rightsHeight = TextHeight(("SCP_PersonnelRights_" + level).Translate(), leftWidth, GameFont.Small);
            float statusHeight = TextHeight(Status(personnel.PromotionStatus(pawn, support.progress), level,
                support.progress.trust), 200f, GameFont.Small);
            return Mathf.Max(104f, 15f + titleHeight + rightsHeight + 12f,
                56f + statusHeight + 12f) + 8f;
        }

        private static float TextHeight(string value, float width, GameFont font)
        {
            GameFont previous = Text.Font;
            Text.Font = font;
            float height = Text.CalcHeight(value, width);
            Text.Font = previous;
            return height;
        }

        private static void DrawPawn(Rect rect, Pawn pawn, FoundationPersonnelComponent personnel,
            FoundationSupportComponent support, bool online)
        {
            ScpTerminalUi.Panel(rect);
            int level = personnel.LevelFor(pawn);
            string title = pawn.LabelShortCap + " — " + RankFor(pawn, personnel);
            float titleHeight = TextHeight(title, rect.width - 245f, GameFont.Medium);
            ScpTerminalUi.Label(new Rect(rect.x + 13f, rect.y + 8f, rect.width - 245f, titleHeight),
                title, ScpTerminalUi.MainText, GameFont.Medium, TextAnchor.UpperLeft);
            ScpTerminalUi.Label(new Rect(rect.x + 13f, rect.y + 15f + titleHeight,
                rect.width - 245f, rect.height - 27f - titleHeight),
                ("SCP_PersonnelRights_" + level).Translate(), ScpTerminalUi.MutedText,
                GameFont.Small, TextAnchor.UpperLeft);

            FoundationPromotionResult status = personnel.PromotionStatus(pawn, support.progress);
            Rect button = new Rect(rect.xMax - 214f, rect.y + 16f, 200f, 34f);
            bool previous = GUI.enabled;
            GUI.enabled = previous && online && status == FoundationPromotionResult.Allowed;
            int cost = FoundationAuthorityPolicy.PromotionCost(level);
            string label = status == FoundationPromotionResult.StoryOnly || status == FoundationPromotionResult.MaximumLevel
                ? "SCP_PersonnelStoryOnly".Translate().ToString()
                : "SCP_PersonnelPromote".Translate(cost).ToString();
            if (ScpTerminalUi.Button(button, label))
            {
                if (level == 3)
                {
                    var choices = new List<FloatMenuOption>();
                    foreach (FoundationRole role in new[] { FoundationRole.MtfCaptain, FoundationRole.Scientist })
                    {
                        FoundationRole chosen = role;
                        bool eligible = FoundationAuthorityPolicy.QualifiesForLevelFour(role,
                            pawn.skills.GetSkill(SkillDefOf.Intellectual).Level,
                            pawn.skills.GetSkill(SkillDefOf.Shooting).Level);
                        string key = role == FoundationRole.MtfCaptain ? "SCP_PersonnelCaptainChoice" : "SCP_PersonnelScientistChoice";
                        choices.Add(new FloatMenuOption(key.Translate(), eligible
                            ? (Action)(() => Promote(pawn, personnel, support, chosen)) : null));
                    }
                    Find.WindowStack.Add(new FloatMenu(choices));
                }
                else Promote(pawn, personnel, support, FoundationRole.None);
            }
            GUI.enabled = previous;
            ScpTerminalUi.Label(new Rect(rect.xMax - 214f, rect.y + 56f, 200f, rect.height - 68f),
                Status(status, level, support.progress.trust), ScpTerminalUi.MutedText,
                GameFont.Small, TextAnchor.UpperLeft);
        }

        public static string Rank(int level)
        {
            return ("SCP_PersonnelRank_" + Mathf.Clamp(level, 0, FoundationAuthorityPolicy.StoryLevel)).Translate();
        }

        public static string RankFor(Pawn pawn, FoundationPersonnelComponent personnel)
        {
            if (personnel.LevelFor(pawn) == 4)
            {
                FoundationRole role = personnel.RoleFor(pawn);
                if (role == FoundationRole.MtfCaptain) return "SCP_PersonnelRank_4Captain".Translate();
                if (role == FoundationRole.Scientist) return "SCP_PersonnelRank_4Scientist".Translate();
            }
            return Rank(personnel.LevelFor(pawn));
        }

        private static void Promote(Pawn pawn, FoundationPersonnelComponent personnel,
            FoundationSupportComponent support, FoundationRole role)
        {
            int level = personnel.LevelFor(pawn);
            FoundationPromotionResult result = personnel.TryPromote(pawn, support.progress, role);
            if (result == FoundationPromotionResult.Allowed)
                Messages.Message("SCP_PersonnelPromoted".Translate(pawn.LabelShortCap, RankFor(pawn, personnel)), pawn,
                    MessageTypeDefOf.PositiveEvent, false);
            else Messages.Message(Status(result, level, support.progress.trust), pawn,
                MessageTypeDefOf.RejectInput, false);
        }

        private static string Status(FoundationPromotionResult result, int level, int trust)
        {
            switch (result)
            {
                case FoundationPromotionResult.Allowed: return "SCP_PersonnelReady".Translate();
                case FoundationPromotionResult.InsufficientTrust:
                    return "SCP_PersonnelNeedTrust".Translate(FoundationAuthorityPolicy.PromotionCost(level), trust);
                case FoundationPromotionResult.TooYoung: return "SCP_PersonnelNeedAdult".Translate();
                case FoundationPromotionResult.ResearchDisabled: return "SCP_PersonnelResearchDisabled".Translate();
                case FoundationPromotionResult.InsufficientResearchSkill: return "SCP_PersonnelNeedResearchTwo".Translate();
                case FoundationPromotionResult.InsufficientSpecialistSkill: return "SCP_PersonnelNeedSpecialist".Translate();
                case FoundationPromotionResult.InsufficientDirectorSkills: return "SCP_PersonnelNeedDirector".Translate();
                case FoundationPromotionResult.StoryOnly: return "SCP_PersonnelLevelFiveHint".Translate();
                default: return "SCP_PersonnelMaximum".Translate();
            }
        }
    }
}
