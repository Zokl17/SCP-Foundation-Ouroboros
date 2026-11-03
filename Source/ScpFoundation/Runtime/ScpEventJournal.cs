using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Runtime
{
    public sealed class ScpEventRecord : IExposable
    {
        public string scpId;
        public int number;
        public int tick;
        public string details;
        public Thing source;

        public string Title { get { return "SCP_JournalRecordTitle".Translate(scpId, number); } }

        public void ExposeData()
        {
            Scribe_Values.Look(ref scpId, "scpId");
            Scribe_Values.Look(ref number, "number", 0);
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref details, "details");
            Scribe_References.Look(ref source, "source");
        }
    }

    public sealed class ScpEventJournal : GameComponent
    {
        private List<ScpEventRecord> records = new List<ScpEventRecord>();

        public ScpEventJournal(Game game) { }

        public IEnumerable<ScpEventRecord> Records(string scpId = null)
        {
            return records.Where(record => scpId == null || record.scpId == scpId)
                .Reverse();
        }

        public static ScpEventRecord Add(string scpId, Thing source, string details)
        {
            if (Current.Game == null || string.IsNullOrEmpty(scpId) || string.IsNullOrEmpty(details)) return null;
            ScpEventJournal journal = Current.Game.GetComponent<ScpEventJournal>();
            if (journal == null) return null;
            int number = journal.records.Where(record => record.scpId == scpId)
                .Select(record => record.number).DefaultIfEmpty(0).Max() + 1;
            ScpEventRecord entry = new ScpEventRecord
            {
                scpId = scpId,
                number = number,
                tick = Find.TickManager.TicksGame,
                details = details,
                source = source
            };
            journal.records.Add(entry);
            return entry;
        }

        public static string Notice(ScpEventRecord entry, string text)
        {
            // The numbered protocol belongs in the journal, not in map notifications.
            return text;
        }

        public ScpEventRecord Latest(string scpId)
        {
            return Records(scpId).FirstOrDefault();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref records, "scpEventJournal", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (records == null) records = new List<ScpEventRecord>();
                records.RemoveAll(record => record == null || string.IsNullOrEmpty(record.scpId));
            }
        }
    }

    public sealed class Dialog_ScpEventJournal : Window
    {
        private readonly string scpId;
        private ScpEventRecord selected;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private bool archiveTab;
        private string selectedArchiveId;
        private Vector2 archiveListScroll;
        private Vector2 archiveDetailScroll;

        public override Vector2 InitialSize { get { return new Vector2(
            Mathf.Min(1000f, UI.screenWidth - 48f), Mathf.Min(600f, UI.screenHeight - 48f)); } }

        public Dialog_ScpEventJournal(string scpId = null, bool openArchive = false)
        {
            this.scpId = scpId;
            archiveTab = scpId == null && openArchive;
            doCloseX = true;
            doCloseButton = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Small;
            ScpTerminalUi.Panel(rect, ScpTerminalUi.Background);
            Rect header = new Rect(12f, 9f, rect.width - 24f, 54f);
            ScpTerminalUi.Panel(header);
            ScpTerminalUi.Label(header.ContractedBy(13f), scpId == null
                ? (archiveTab ? "SCP_FoundationArchiveTab" : "SCP_JournalButton").Translate()
                : "SCP_JournalAppendix".Translate(scpId),
                ScpTerminalUi.AccentColor, GameFont.Medium);
            float bodyY = 72f;
            if (scpId == null)
            {
                ScpOuroborosArchiveComponent archive = Current.Game.GetComponent<ScpOuroborosArchiveComponent>();
                float tabWidth = (rect.width - 32f) / 2f;
                if (ScpTerminalUi.Button(new Rect(12f, 71f, tabWidth, 33f),
                    "SCP_JournalButton".Translate(), !archiveTab)) archiveTab = false;
                string archiveLabel = "SCP_FoundationArchiveTab".Translate();
                if (archive != null && archive.UnreadCount > 0) archiveLabel += "  ● " + archive.UnreadCount;
                if (ScpTerminalUi.Button(new Rect(20f + tabWidth, 71f, tabWidth, 33f),
                    archiveLabel, archiveTab)) archiveTab = true;
                bodyY = 112f;
                if (archiveTab)
                {
                    DrawArchive(rect, bodyY, archive);
                    return;
                }
            }
            ScpEventJournal journal = Current.Game.GetComponent<ScpEventJournal>();
            List<ScpEventRecord> entries = journal == null ? new List<ScpEventRecord>() : journal.Records(scpId).ToList();
            Rect list = new Rect(12f, bodyY, 331f, rect.height - bodyY - 60f);
            Rect detail = new Rect(353f, bodyY, rect.width - 365f, rect.height - bodyY - 60f);
            ScpTerminalUi.Panel(list);
            ScpTerminalUi.Panel(detail);
            if (entries.Count == 0)
            {
                ScpTerminalUi.Label(detail.ContractedBy(16f), "SCP_JournalEmpty".Translate(),
                    ScpTerminalUi.MutedText, GameFont.Small, TextAnchor.UpperLeft);
                if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 50f, 140f, 33f),
                    "SCP_TerminalClose".Translate())) Close();
                return;
            }
            if (selected == null || !entries.Contains(selected)) selected = entries[0];
            Rect listView = list.ContractedBy(9f);
            Widgets.BeginScrollView(listView, ref listScroll,
                new Rect(0f, 0f, listView.width - 16f, Mathf.Max(listView.height, entries.Count * 66f)));
            for (int i = 0; i < entries.Count; i++)
            {
                ScpEventRecord record = entries[i];
                string label = record.Title + "\n"
                    + "SCP_JournalDay".Translate(record.tick / 60000 + 1);
                if (ScpTerminalUi.Button(new Rect(0f, i * 66f, listView.width - 16f, 60f),
                    label, record == selected))
                {
                    selected = record;
                    detailScroll = Vector2.zero;
                }
            }
            Widgets.EndScrollView();
            float x = detail.x + 14f, width = detail.width - 28f;
            ScpTerminalUi.Label(new Rect(x, detail.y + 10f, width, 38f),
                selected.Title, ScpTerminalUi.MainText, GameFont.Medium);
            ScpTerminalUi.Label(new Rect(x, detail.y + 48f, width, 24f),
                "SCP_JournalDay".Translate(selected.tick / 60000 + 1), ScpTerminalUi.MutedText);
            if (selected.source != null && selected.source.Spawned
                && ScpTerminalUi.Button(new Rect(x, detail.y + 79f, width, 30f), "SCP_TerminalLocate".Translate()))
                CameraJumper.TryJumpAndSelect(selected.source);
            Rect report = new Rect(x, detail.y + 119f, width, detail.height - 132f);
            ScpTerminalUi.Panel(report, ScpTerminalUi.Background);
            Color previous = GUI.color;
            GUI.color = ScpTerminalUi.MainText;
            Widgets.LabelScrollable(report.ContractedBy(9f), selected.details ?? "", ref detailScroll);
            GUI.color = previous;
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 50f, 140f, 33f),
                "SCP_TerminalClose".Translate())) Close();
        }

        private void DrawArchive(Rect rect, float bodyY, ScpOuroborosArchiveComponent archive)
        {
            Rect list = new Rect(12f, bodyY, 331f, rect.height - bodyY - 60f);
            Rect detail = new Rect(353f, bodyY, rect.width - 365f, rect.height - bodyY - 60f);
            ScpTerminalUi.Panel(list);
            ScpTerminalUi.Panel(detail);
            List<ScpOuroborosArchiveEntry> entries = archive == null
                ? new List<ScpOuroborosArchiveEntry>() : archive.Entries();
            if (entries.Count == 0)
            {
                ScpTerminalUi.Label(detail.ContractedBy(16f), "SCP_FoundationArchiveEmpty".Translate(),
                    ScpTerminalUi.MutedText, GameFont.Small, TextAnchor.UpperLeft);
            }
            else
            {
                ScpOuroborosArchiveEntry selectedEntry = entries.FirstOrDefault(entry => entry.id == selectedArchiveId);
                if (selectedEntry == null)
                {
                    selectedEntry = entries.FirstOrDefault(entry => !entry.read) ?? entries[0];
                    selectedArchiveId = selectedEntry.id;
                    archiveListScroll = Vector2.zero;
                }
                Rect listView = list.ContractedBy(9f);
                Widgets.BeginScrollView(listView, ref archiveListScroll,
                    new Rect(0f, 0f, listView.width - 16f, Mathf.Max(listView.height, entries.Count * 66f)));
                for (int i = 0; i < entries.Count; i++)
                {
                    ScpOuroborosArchiveEntry entry = entries[i];
                    string label = (entry.read ? "" : "● ") + entry.title + "\n"
                        + "SCP_JournalDay".Translate(entry.tick / 60000 + 1);
                    if (ScpTerminalUi.Button(new Rect(0f, i * 66f, listView.width - 16f, 60f),
                        label, entry.id == selectedArchiveId))
                    {
                        selectedArchiveId = entry.id;
                        selectedEntry = entry;
                        archiveDetailScroll = Vector2.zero;
                    }
                }
                Widgets.EndScrollView();
                archive.MarkRead(selectedEntry.id);
                float x = detail.x + 14f, width = detail.width - 28f;
                ScpTerminalUi.Label(new Rect(x, detail.y + 10f, width, 38f),
                    selectedEntry.title, ScpTerminalUi.MainText, GameFont.Medium);
                ScpTerminalUi.Label(new Rect(x, detail.y + 48f, width, 24f),
                    "SCP_JournalDay".Translate(selectedEntry.tick / 60000 + 1), ScpTerminalUi.MutedText);
                Rect report = new Rect(x, detail.y + 82f, width, detail.height - 95f);
                ScpTerminalUi.Panel(report, ScpTerminalUi.Background);
                Color previous = GUI.color;
                GUI.color = ScpTerminalUi.MainText;
                Widgets.LabelScrollable(report.ContractedBy(11f), selectedEntry.body, ref archiveDetailScroll);
                GUI.color = previous;
                if (archive != null && archive.FinalReportAvailable && selectedEntry.id == "greenVisit")
                {
                    Map map = Find.CurrentMap;
                    bool canSubmit = archive.CanSubmitFinalReport(map);
                    string reportLabel = map == null || CompFoundationAccessTerminal.Online(map) == null
                        ? "SCP_882FinalReportNeedTerminal".Translate()
                        : !ScpOuroborosArchiveComponent.HasContained882(map)
                            ? "SCP_882FinalReportNeedContainment".Translate()
                            : "SCP_882FinalReportSubmit".Translate();
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = wasEnabled && canSubmit;
                    if (ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f, rect.height - 50f, 280f, 33f),
                        reportLabel) && archive.SubmitFinalReport(map))
                    {
                        selectedArchiveId = "finalReport";
                        archiveDetailScroll = Vector2.zero;
                    }
                    GUI.enabled = wasEnabled;
                    if (ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f + 290f, rect.height - 50f, 140f, 33f),
                        "SCP_TerminalClose".Translate())) Close();
                    return;
                }
                if (archive != null && selectedEntry.id == "churchCache" && archive.ChurchReportTick < 0)
                {
                    Map map = Find.CurrentMap;
                    bool canSend = archive.CanSubmitChurchReport(map);
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = wasEnabled && canSend;
                    if (ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f, rect.height - 50f, 280f, 33f),
                        canSend ? "SCP_ChurchReportSubmit".Translate() : "SCP_ChurchReportNeedTerminal".Translate())
                        && archive.SubmitChurchReport(map)) archiveDetailScroll = Vector2.zero;
                    GUI.enabled = wasEnabled;
                    if (ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f + 290f, rect.height - 50f, 140f, 33f),
                        "SCP_TerminalClose".Translate())) Close();
                    return;
                }
                if (archive != null && selectedEntry.id == "routingLog"
                    && archive.OuroborosFinalTick < 0)
                {
                    Map map = Find.CurrentMap;
                    bool canClose = archive.CanCloseOuroboros(map);
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = wasEnabled && canClose;
                    if (ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f, rect.height - 50f, 280f, 33f),
                        canClose ? "SCP_OuroborosFinalSubmit".Translate()
                            : "SCP_OuroborosFinalNeedTerminal".Translate())
                        && archive.CloseOuroboros(map))
                    {
                        selectedArchiveId = "ouroborosFinal";
                        archiveDetailScroll = Vector2.zero;
                    }
                    GUI.enabled = wasEnabled;
                    if (ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f + 290f,
                        rect.height - 50f, 140f, 33f), "SCP_TerminalClose".Translate())) Close();
                    return;
                }
                if (archive != null && selectedEntry.id == "finalReport"
                    && archive.SignalAnalysisTick < 0)
                {
                    Map map = Find.CurrentMap;
                    QuestPart_ScpSignalResearch part = QuestPart_ScpSignalResearch.ActiveOn(map);
                    if (part != null && ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f,
                        rect.height - 50f, 280f, 33f), "SCP_SignalResearchAssign".Translate()))
                        ChooseSignalResearcher(map);
                    if (part != null && ScpTerminalUi.Button(new Rect((rect.width - 430f) / 2f + 290f,
                        rect.height - 50f, 140f, 33f), "SCP_TerminalClose".Translate())) Close();
                    if (part != null) return;
                }
            }
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 50f, 140f, 33f),
                "SCP_TerminalClose".Translate())) Close();
        }

        private static void ChooseSignalResearcher(Map map)
        {
            Thing terminal = CompFoundationAccessTerminal.Online(map);
            if (terminal == null)
            {
                Messages.Message("SCP_ArchiveNeedsTerminal".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            FoundationPersonnelComponent personnel = Current.Game.GetComponent<FoundationPersonnelComponent>();
            var options = new List<FloatMenuOption>();
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead || pawn.Downed || pawn.Drafted || pawn.jobs == null
                    || personnel.LevelFor(pawn) < 4
                    || pawn.skills.GetSkill(SkillDefOf.Intellectual).Level < 12
                    || !pawn.CanReserveAndReach(terminal, PathEndMode.Touch, Danger.Some)) continue;
                Pawn researcher = pawn;
                options.Add(new FloatMenuOption(researcher.LabelShortCap, () =>
                {
                    if (QuestPart_ScpSignalResearch.ActiveOn(map) == null
                        || CompFoundationAccessTerminal.Online(map) != terminal) return;
                    JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("SCP_OuroborosSignalResearch");
                    if (jobDef != null) researcher.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(jobDef, terminal), JobTag.Misc);
                }));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("SCP_SignalResearchNoResearcher".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
