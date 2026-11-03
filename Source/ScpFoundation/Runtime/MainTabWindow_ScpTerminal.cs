using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Containment;
using ScpFoundation.Definitions;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Runtime
{
    public sealed class MainTabWindow_ScpTerminal : MainTabWindow
    {
        private static readonly Color ScreenColor = new Color(0.055f, 0.085f, 0.105f);
        private static readonly Color PanelColor = new Color(0.075f, 0.115f, 0.135f);
        private static readonly Color BorderColor = new Color(0.18f, 0.31f, 0.34f);
        private static readonly Color AccentColor = new Color(0.31f, 0.82f, 0.78f);
        private static readonly Color MainTextColor = new Color(0.88f, 0.94f, 0.92f);
        private static readonly Color DimTextColor = new Color(0.56f, 0.68f, 0.69f);
        private bool postsTab;
        private Thing selected;
        private ScpDef selectedArticle;
        private Map snapshotMap;
        private float nextRefresh;
        private readonly List<Thing> objects = new List<Thing>();
        private readonly List<ScpDef> articles = new List<ScpDef>();
        private readonly List<Thing> posts = new List<Thing>();
        private Vector2 listScroll, detailScroll;
        private string details = "";
        public override Vector2 RequestedTabSize { get { return new Vector2(
            Mathf.Min(1100f, UI.screenWidth - 64f), Mathf.Min(650f, UI.screenHeight - 80f)); } }

        public static void Open(CompScp anomaly)
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamed("SCPTerminal");
            Find.MainTabsRoot.SetCurrentTab(button, false);
            MainTabWindow_ScpTerminal window = (MainTabWindow_ScpTerminal)button.TabWindow;
            window.postsTab = false;
            window.selected = anomaly.parent;
            window.nextRefresh = 0;
            window.detailScroll = Vector2.zero;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            nextRefresh = 0;
        }

        private void Refresh(Map map)
        {
            if (snapshotMap != map) { selected = null; selectedArticle = null; listScroll = detailScroll = Vector2.zero; }
            snapshotMap = map;
            nextRefresh = Time.realtimeSinceStartup + 1f;
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            objects.Clear();
            objects.AddRange(map.GetComponent<ScpStoryStateComponent>().Anomalies
                .Where(comp => comp.parent.Spawned && !comp.Definition.source.originalFixture
                    && !comp.parent.Position.Fogged(map)).Select(comp => (Thing)comp.parent));
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried != null && carried.TryGetComp<CompScp>() != null && !objects.Contains(carried)) objects.Add(carried);
            }
            objects.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            articles.Clear();
            foreach (ScpDef article in ScpArticleCatalog.Articles)
            {
                if (ScpArticleCatalog.Representative(map, article) != null) knowledge.RecordEncounter(article);
                if (knowledge.HasEncountered(article)) articles.Add(article);
            }
            posts.Clear();
            posts.AddRange(map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCP_ContainmentAnchor).Where(post => post.Faction == Faction.OfPlayer));
            List<Thing> current = postsTab ? posts : objects;
            if (selected != null && !current.Contains(selected)) selected = null;
            if (postsTab || !articles.Contains(selectedArticle)) selectedArticle = null;
            if (selected == null && selectedArticle == null)
            {
                selected = current.FirstOrDefault();
                if (selected == null && !postsTab) selectedArticle = articles.FirstOrDefault();
            }
            details = selectedArticle != null ? ScpArticleCatalog.Dossier(selectedArticle, knowledge)
                : selected == null ? "" : postsTab ? PostDetails(selected, knowledge) : ObjectDetails(selected);
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Small;
            Widgets.DrawBoxSolid(rect, ScreenColor);
            DrawBorder(rect, BorderColor);
            Map map = Find.CurrentMap;
            if (map == null)
            {
                DrawLabel(rect.ContractedBy(24f), "SCP_TerminalNoMap".Translate(), MainTextColor);
                return;
            }
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            if (Time.realtimeSinceStartup >= nextRefresh || snapshotMap != map) Refresh(map);
            Rect header = new Rect(12f, 10f, rect.width - 24f, 54f);
            Widgets.DrawBoxSolid(header, PanelColor);
            DrawBorder(header, BorderColor);
            Widgets.DrawBoxSolid(new Rect(header.x + 12f, header.y + 11f, 35f, 32f), AccentColor);
            DrawLabel(new Rect(header.x + 13f, header.y + 11f, 33f, 32f), "SCP", ScreenColor,
                GameFont.Tiny, TextAnchor.MiddleCenter);
            DrawLabel(new Rect(header.x + 58f, header.y + 5f, header.width - 225f, 24f),
                "SCP_TerminalSystemName".Translate(), AccentColor, GameFont.Medium);
            DrawLabel(new Rect(header.x + 58f, header.y + 28f, header.width - 225f, 20f),
                "SCP_TerminalHeading".Translate(map.Parent.LabelCap), DimTextColor);
            bool contactAvailable = Current.Game.GetComponent<FoundationContactComponent>().StatueDiscovered;
            Widgets.DrawBoxSolid(new Rect(header.xMax - 165f, header.y + 23f, 7f, 7f),
                contactAvailable ? AccentColor : DimTextColor);
            DrawLabel(new Rect(header.xMax - 150f, header.y + 13f, 137f, 28f),
                (contactAvailable ? "SCP_TerminalOnline" : "SCP_ContactLinkOffline").Translate(),
                contactAvailable ? AccentColor : DimTextColor, GameFont.Small, TextAnchor.MiddleRight);

            float navY = 72f;
            float navWidth = (rect.width - 54f) / 4f;
            if (TerminalButton(new Rect(12f, navY, navWidth, 37f),
                "SCP_TerminalObjects".Translate(objects.Count + articles.Count), !postsTab)) SwitchTab(false);
            if (TerminalButton(new Rect(22f + navWidth, navY, navWidth, 37f),
                "SCP_TerminalPosts".Translate(posts.Count), postsTab)) SwitchTab(true);
            if (TerminalButton(new Rect(32f + navWidth * 2f, navY, navWidth, 37f),
                "SCP_ContactButton".Translate()))
                Find.WindowStack.Add(new Dialog_FoundationContact(map));
            ScpOuroborosArchiveComponent archive = Current.Game.GetComponent<ScpOuroborosArchiveComponent>();
            string journalLabel = "SCP_JournalButton".Translate();
            if (archive != null && archive.UnreadCount > 0) journalLabel += "  ● " + archive.UnreadCount;
            if (TerminalButton(new Rect(42f + navWidth * 3f, navY, navWidth, 37f), journalLabel))
                Find.WindowStack.Add(new Dialog_ScpEventJournal());
            DrawLabel(new Rect(16f, 113f, rect.width - 32f, 43f),
                postsTab ? "SCP_TerminalRoomHint".Translate() : "SCP_TerminalWorkHint".Translate(), DimTextColor);

            float bodyY = 162f;
            float bodyHeight = rect.height - bodyY - 32f;
            float listWidth = Mathf.Clamp(rect.width * 0.28f, 230f, 292f);
            Rect listPanel = new Rect(12f, bodyY, listWidth, bodyHeight);
            Rect detailPanel = new Rect(listPanel.xMax + 10f, bodyY,
                rect.width - listPanel.xMax - 22f, bodyHeight);
            DrawPanel(listPanel);
            DrawPanel(detailPanel);
            DrawLabel(new Rect(listPanel.x + 13f, listPanel.y + 6f, listPanel.width - 26f, 28f),
                "SCP_TerminalRegistry".Translate(), AccentColor);
            Widgets.DrawBoxSolid(new Rect(listPanel.x + 10f, listPanel.y + 36f,
                listPanel.width - 20f, 1f), BorderColor);
            List<Thing> current = postsTab ? posts : objects;
            Rect list = new Rect(listPanel.x + 10f, listPanel.y + 44f,
                listPanel.width - 20f, listPanel.height - 54f);
            int recordCount = current.Count + (postsTab ? 0 : articles.Count);
            Widgets.BeginScrollView(list, ref listScroll,
                new Rect(0f, 0f, list.width - 16f, Mathf.Max(list.height, recordCount * 72f)));
            for (int i = 0; i < current.Count; i++)
            {
                Thing thing = current[i];
                CompScp scp = thing.TryGetComp<CompScp>();
                string label = scp == null ? thing.LabelCap.ToString() : scp.PublicLabel;
                string status = scp != null ? !thing.Spawned ? "SCP_TerminalCarried".Translate().ToString()
                    : scp.IsInCustody ? scp.StatusLabel : "SCP_TerminalUncaptured".Translate().ToString() : thing.Position.ToString();
                if (RecordButton(new Rect(0f, i * 72f, list.width - 16f, 65f),
                    label, status, thing == selected))
                { selected = thing; selectedArticle = null; detailScroll = Vector2.zero; nextRefresh = 0; }
            }
            if (!postsTab)
                for (int i = 0; i < articles.Count; i++)
                {
                    ScpDef article = articles[i];
                    if (RecordButton(new Rect(0f, (current.Count + i) * 72f, list.width - 16f, 65f),
                        ArticleLabel(article), "SCP_ArchiveEntry".Translate(), article == selectedArticle))
                    { selected = null; selectedArticle = article; detailScroll = Vector2.zero; nextRefresh = 0; }
                }
            if (recordCount == 0) DrawLabel(new Rect(8f, 4f, list.width - 28f, 74f),
                "SCP_TerminalEmpty".Translate(), DimTextColor);
            Widgets.EndScrollView();

            DrawLabel(new Rect(detailPanel.x + 14f, detailPanel.y + 6f, detailPanel.width - 205f, 28f),
                "SCP_TerminalDossier".Translate(), AccentColor);
            Widgets.DrawBoxSolid(new Rect(detailPanel.x + 10f, detailPanel.y + 36f,
                detailPanel.width - 20f, 1f), BorderColor);
            if (selected == null && selectedArticle == null)
            {
                DrawLabel(new Rect(detailPanel.x + 16f, detailPanel.y + 56f,
                    detailPanel.width - 32f, 52f), "SCP_TerminalEmpty".Translate(), DimTextColor);
                return;
            }
            float x = detailPanel.x + 14f, width = detailPanel.width - 28f;
            Thing located = selectedArticle == null ? selected : ScpArticleCatalog.Representative(map, selectedArticle);
            if (TerminalButton(new Rect(detailPanel.xMax - 183f, detailPanel.y + 6f, 169f, 27f),
                "SCP_TerminalLocate".Translate()))
            {
                Thing target = located == null ? null : located.Spawned ? located : map.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(pawn => pawn.carryTracker.CarriedThing == located
                        || pawn.inventory != null && pawn.inventory.innerContainer.Contains(located));
                if (target != null) CameraJumper.TryJumpAndSelect(target);
            }
            CompScp displayScp = selected == null ? null : selected.TryGetComp<CompScp>();
            DrawLabel(new Rect(x, detailPanel.y + 42f, width, 30f),
                selectedArticle != null ? ArticleLabel(selectedArticle)
                    : displayScp != null ? displayScp.PublicLabel : selected.LabelCap.ToString(), MainTextColor,
                GameFont.Medium);
            float top = detailPanel.y + 78f;
            if (selectedArticle != null && (knowledge.LevelFor(selectedArticle) < KnowledgeLevel.Documented
                || !knowledge.ExperimentsComplete(selectedArticle)))
            {
                KnowledgeLevel level = knowledge.LevelFor(selectedArticle);
                string actionKey = level == KnowledgeLevel.Unknown ? "SCP_ArchiveStudy"
                    : level == KnowledgeLevel.Observed ? "SCP_ArchiveExperiment"
                    : level == KnowledgeLevel.PartiallyDocumented ? "SCP_ArchiveDocument" : "SCP_RunExperiments";
                if (TerminalButton(new Rect(x, top, width, 28f), actionKey.Translate()))
                    ChooseArchiveResearcher(map, selectedArticle);
                top += 34f;
            }
            CompScpPost dutyPost = selected == null ? null : postsTab ? selected.TryGetComp<CompScpPost>() : CompScpPost.ForObject(selected);
            if (dutyPost != null)
            {
                if (TerminalButton(new Rect(x, top, width, 28f), "SCP_ShiftsButton".Translate()))
                    Find.WindowStack.Add(new Dialog_ScpShifts(dutyPost));
                top += 34f;
            }
            CompScpInteraction interaction = selected == null ? null : selected.TryGetComp<CompScpInteraction>();
            if (!postsTab && interaction != null && interaction.CanWorkAtPost)
            {
                Widgets.CheckboxLabeled(new Rect(x, top, width, 27f), "SCP_ObserveToggle".Translate(), ref interaction.observationEnabled);
                Widgets.CheckboxLabeled(new Rect(x, top + 29f, width, 27f), "SCP_ResearchToggle".Translate(), ref interaction.researchEnabled);
                top += 63f;
            }
            CompScp079 computer = selected == null ? null : selected.TryGetComp<CompScp079>();
            if (!postsTab && computer != null)
            {
                bool enabled = GUI.enabled;
                bool studied = displayScp != null && knowledge.LevelFor(displayScp.Definition)
                    >= KnowledgeLevel.PartiallyDocumented;
                bool accepted = Current.Game.GetComponent<FoundationContactComponent>()
                    .Scp079InterviewAcceptedTick >= 0;
                GUI.enabled = enabled && studied && accepted;
                if (TerminalButton(new Rect(x, top, width, 30f), "SCP_079InterviewButton".Translate()))
                    Find.WindowStack.Add(new Dialog_Scp079Interview(selected, map));
                GUI.enabled = enabled;
                top += 36f;
                if (!studied || !accepted)
                {
                    DrawLabel(new Rect(x, top, width, 22f),
                        (!studied ? "SCP_079NeedsResearch" : "SCP_079NeedsAssignment").Translate(),
                        DimTextColor, GameFont.Small);
                    top += 24f;
                }
            }
            string appendixId = selectedArticle != null ? selectedArticle.scpId : null;
            if (!postsTab && appendixId == null && selected != null)
            {
                CompScp selectedScp = selected.TryGetComp<CompScp>();
                if (selectedScp != null && selectedScp.Definition != null) appendixId = selectedScp.Definition.scpId;
            }
            float appendixHeight = appendixId == null ? 0f : 40f;
            Rect dossier = new Rect(x, top + 4f, width,
                Mathf.Max(50f, detailPanel.yMax - top - appendixHeight - 18f));
            Widgets.DrawBoxSolid(dossier, ScreenColor);
            DrawBorder(dossier, BorderColor);
            Color previousColor = GUI.color;
            GUI.color = MainTextColor;
            Widgets.LabelScrollable(dossier.ContractedBy(10f), details, ref detailScroll);
            GUI.color = previousColor;
            if (appendixId != null && TerminalButton(new Rect(x, detailPanel.yMax - 39f, width, 30f),
                "SCP_JournalAppendix".Translate(appendixId)))
                Find.WindowStack.Add(new Dialog_ScpEventJournal(appendixId));
            DrawLabel(new Rect(16f, rect.height - 27f, rect.width - 32f, 21f),
                "SCP_TerminalFooter".Translate(), DimTextColor, GameFont.Tiny);
        }

        private static void DrawPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, PanelColor);
            DrawBorder(rect, BorderColor);
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            Widgets.DrawBox(rect);
            GUI.color = previous;
        }

        private static void DrawLabel(Rect rect, string label, Color color,
            GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Color previousColor = GUI.color;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            GUI.color = color;
            Text.Font = font;
            Text.Anchor = anchor;
            Widgets.Label(rect, label);
            GUI.color = previousColor;
            Text.Font = previousFont;
            Text.Anchor = previousAnchor;
        }

        private static bool TerminalButton(Rect rect, string label, bool active = false)
        {
            bool enabled = GUI.enabled;
            bool hovered = enabled && Mouse.IsOver(rect);
            Widgets.DrawBoxSolid(rect, active ? new Color(0.12f, 0.23f, 0.25f)
                : hovered ? new Color(0.12f, 0.18f, 0.2f) : PanelColor);
            DrawBorder(rect, !enabled ? DimTextColor : active || hovered ? AccentColor : BorderColor);
            if (active) Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), AccentColor);
            DrawLabel(new Rect(rect.x + 7f, rect.y + 1f, rect.width - 14f, rect.height - 2f),
                label, !enabled ? DimTextColor : active || hovered ? AccentColor : MainTextColor,
                GameFont.Small, TextAnchor.MiddleCenter);
            return enabled && Widgets.ButtonInvisible(rect);
        }

        private static bool RecordButton(Rect rect, string label, string status, bool active)
        {
            bool hovered = Mouse.IsOver(rect);
            Widgets.DrawBoxSolid(rect, active ? new Color(0.11f, 0.21f, 0.23f)
                : hovered ? new Color(0.11f, 0.16f, 0.18f) : ScreenColor);
            if (active) Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 3f, rect.height), AccentColor);
            DrawBorder(rect, active ? AccentColor : BorderColor);
            DrawLabel(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 22f, 26f),
                label, active ? AccentColor : MainTextColor);
            DrawLabel(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 22f, 23f),
                status, DimTextColor, GameFont.Tiny);
            return Widgets.ButtonInvisible(rect);
        }

        private void SwitchTab(bool postsMode)
        {
            if (postsMode == postsTab) return;
            postsTab = postsMode;
            selected = null;
            selectedArticle = null;
            listScroll = detailScroll = Vector2.zero;
            Refresh(Find.CurrentMap);
        }

        private static void ChooseArchiveResearcher(Map map, ScpDef article)
        {
            Thing terminal = CompFoundationAccessTerminal.Online(map);
            if (terminal == null)
            {
                Messages.Message("SCP_ArchiveNeedsTerminal".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var options = new List<FloatMenuOption>();
            FoundationPersonnelComponent personnel = Current.Game.GetComponent<FoundationPersonnelComponent>();
            foreach (Pawn candidate in map.mapPawns.FreeColonistsSpawned)
            {
                Pawn pawn = candidate;
                if (pawn.Dead || pawn.Downed || pawn.Drafted || pawn.jobs == null
                    || personnel.LevelFor(pawn) < 1
                    || !pawn.CanReserveAndReach(terminal, PathEndMode.Touch, Danger.Some)) continue;
                options.Add(new FloatMenuOption(pawn.LabelShortCap, () =>
                {
                    JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("SCP_ArchiveResearch");
                    if (jobDef == null || CompFoundationAccessTerminal.Online(map) != terminal) return;
                    Job job = JobMaker.MakeJob(jobDef, terminal);
                    job.count = int.Parse(article.scpId.Substring(4));
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("SCP_ArchiveNoResearcher".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string ObjectDetails(Thing thing)
        {
            CompScp scp = thing.TryGetComp<CompScp>();
            Building_Scp294 machine = thing as Building_Scp294;
            if (machine != null) return machine.Dossier();
            if (scp.Clearance == 0)
            {
                CompScpInteraction restrictedInteraction = thing.TryGetComp<CompScpInteraction>();
                return scp.BuildRequirementsReport() + (scp.CanResearchNow && restrictedInteraction != null
                    ? "\n\n" + restrictedInteraction.Dossier() : "");
            }
            var text = new StringBuilder();
            text.AppendLine("SCP_Clearance".Translate(scp.Clearance));
            text.AppendLine(scp.PublicLabel + " • " + scp.Definition.scpId + " • " + scp.Definition.objectClass);
            CompScpInteraction interaction = thing.TryGetComp<CompScpInteraction>();
            if (interaction != null) text.AppendLine(interaction.Dossier());
            CompScp079 interview = thing.TryGetComp<CompScp079>();
            if (interview != null) text.AppendLine(interview.Transcript());
            text.AppendLine();
            if (thing.Spawned)
            {
                text.AppendLine("SCP_TerminalStaff".Translate());
                foreach (Pawn pawn in thing.Map.mapPawns.FreeColonistsSpawned)
                    if (pawn.CurJob != null && pawn.CurJob.targetA.Thing == thing
                        && (pawn.CurJob.def == ScpInteractionDefOf.SCP_Observe || pawn.CurJob.def == ScpInteractionDefOf.SCP_Study))
                        text.AppendLine(pawn.LabelShortCap + " — " + pawn.jobs.curDriver.GetReport());
                text.AppendLine();
            }
            text.AppendLine(scp.BuildRequirementsReport());
            return text.ToString();
        }

        private string PostDetails(Thing post, ScpKnowledgeComponent knowledge)
        {
            var text = new StringBuilder();
            var context = new ContainmentContext((ThingWithComps)post);
            text.AppendLine(post.LabelCap + " • " + post.Position);
            CompScpPost dutyPost = post.TryGetComp<CompScpPost>();
            if (dutyPost != null) text.AppendLine(dutyPost.Summary());
            List<Thing> occupants = objects.Where(thing => thing.Spawned && thing.TryGetComp<CompScp>().IsInCustody
                && (thing.Position == post.Position || context.room != null && !context.room.TouchesMapEdge
                    && thing.Position.GetRoom(post.Map) == context.room)).ToList();
            text.AppendLine("SCP_TerminalOccupants".Translate(occupants.Count == 0 ? "SCP_TerminalNone".Translate().ToString()
                : string.Join(", ", occupants.Select(thing => thing.TryGetComp<CompScp>().PublicLabel).ToArray())));
            text.AppendLine();
            foreach (ScpDef definition in DefDatabase<ScpDef>.AllDefs.Where(definition => !definition.source.originalFixture
                && objects.Any(thing => thing.TryGetComp<CompScp>().Definition == definition && thing.TryGetComp<CompScp>().Clearance > 0)))
            {
                var requirements = definition.requirements.Where(requirement => IsRoomRequirement(requirement)
                    && knowledge.LevelFor(definition) >= requirement.visibleFrom).ToList();
                bool ready = requirements.Count > 0 && requirements.All(requirement => requirement.IsSatisfied(context));
                text.AppendLine(definition.scpId + " — " + (ready ? "SCP_TerminalRoomReady" : "SCP_TerminalRoomNotReady").Translate());
                foreach (ContainmentRequirement requirement in requirements)
                    text.AppendLine("SCP_RequirementLine".Translate((requirement.IsSatisfied(context) ? "SCP_CheckMet" : "SCP_CheckUnmet").Translate(), requirement.Description()));
                if (definition.requirements.Any(requirement => knowledge.LevelFor(definition) < requirement.visibleFrom)) text.AppendLine("SCP_HiddenRequirements".Translate());
                text.AppendLine();
            }
            text.AppendLine("SCP_TerminalRoomHint".Translate());
            return text.ToString();
        }

        private static bool IsRoomRequirement(ContainmentRequirement requirement)
        {
            return requirement is RequirementRoom || requirement is RequirementWallMaterial
                || requirement is RequirementLight || requirement is RequirementTemperature;
        }

        private static string ArticleLabel(ScpDef article)
        {
            return article.scpId == "SCP-650" ? "SCP_650DisplayName".Translate().ToString()
                : article.scpId == "SCP-999" ? "SCP_999DisplayName".Translate().ToString() : article.label;
        }
    }
}
