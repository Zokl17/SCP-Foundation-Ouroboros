using System;
using System.Collections.Generic;
using System.Linq;
using ScpFoundation.Anomalies;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_Scp079Interview : Window
    {
        private readonly Thing computer;
        private readonly Map map;
        private static readonly Color Screen = new Color(0.025f, 0.055f, 0.035f);
        private static readonly Color Panel = new Color(0.045f, 0.095f, 0.06f);
        private static readonly Color Green = new Color(0.34f, 0.88f, 0.46f);
        private static readonly Color Muted = new Color(0.52f, 0.69f, 0.55f);

        public Dialog_Scp079Interview(Thing computer, Map map)
        {
            this.computer = computer;
            this.map = map;
            doCloseX = true;
            doCloseButton = false;
        }

        public override Vector2 InitialSize { get { return new Vector2(720f, 650f); } }

        public override void DoWindowContents(Rect rect)
        {
            CompScp079 interview = computer == null ? null : computer.TryGetComp<CompScp079>();
            CompScp scp = computer == null ? null : computer.TryGetComp<CompScp>();
            bool studied = scp != null && Current.Game.GetComponent<ScpKnowledgeComponent>()
                .LevelFor(scp.Definition) >= KnowledgeLevel.PartiallyDocumented;
            bool accepted = Current.Game.GetComponent<FoundationContactComponent>().Scp079InterviewAcceptedTick >= 0;
            bool qualified = map != null && map.mapPawns.FreeColonistsSpawned.Any(pawn =>
                Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(pawn) >= 2);
            bool ready = interview != null && scp != null && computer.Spawned && computer.Map == map
                && scp.CanResearchNow && CompFoundationAccessTerminal.Online(map) != null;
            ScpTerminalUi.Panel(rect, Screen);
            GreenPanel(new Rect(12f, 10f, rect.width - 24f, 48f));
            ScpTerminalUi.Label(new Rect(25f, 14f, rect.width - 50f, 38f),
                "SCP_079InterviewTitle".Translate(), Green, GameFont.Medium);
            GreenPanel(new Rect(12f, 68f, rect.width - 24f, 64f));
            ScpTerminalUi.Label(new Rect(24f, 73f, rect.width - 48f, 54f),
                "SCP_079InterviewSafety".Translate(), ScpTerminalUi.MainText,
                GameFont.Small, TextAnchor.UpperLeft);
            GreenPanel(new Rect(12f, 142f, rect.width - 24f, 178f));
            ScpTerminalUi.Label(new Rect(24f, 149f, rect.width - 48f, 25f),
                "SCP_079TranscriptHeading".Translate(), Green);
            int recorded = 0;
            if (interview != null)
                for (int i = 0; i < 3; i++)
                    if (interview.HasTopic(i))
                    {
                        if (GreenButton(new Rect(24f, 180f + recorded * 42f, rect.width - 48f, 34f),
                            ("SCP_079Document" + i + "Title").Translate()))
                            Find.WindowStack.Add(new Dialog_Scp079Document(i));
                        recorded++;
                    }
            if (recorded == 0)
                ScpTerminalUi.Label(new Rect(24f, 183f, rect.width - 48f, 28f),
                    "SCP_079NoTranscript".Translate(), Muted);

            string status = interview != null && interview.ProtocolDecided
                ? "SCP_079AssignmentComplete".Translate().ToString()
                : !accepted ? "SCP_079NeedsAssignment".Translate().ToString()
                : !studied ? "SCP_079NeedsResearch".Translate().ToString()
                : !qualified ? "SCP_079NoInterviewer".Translate().ToString()
                : !ready ? "SCP_079NotReady".Translate().ToString()
                : interview.AllTopicsRecorded ? "SCP_079AssignmentReady".Translate().ToString()
                : interview.RemainingCooldown > 0
                    ? "SCP_079Cooldown".Translate((interview.RemainingCooldown / 2500f).ToString("0.0")).ToString()
                    : "SCP_079Ready".Translate().ToString();
            GreenPanel(new Rect(12f, 330f, rect.width - 24f, 57f));
            ScpTerminalUi.Label(new Rect(24f, 336f, rect.width - 48f, 45f), status, Muted,
                GameFont.Small, TextAnchor.UpperLeft);
            bool previous = GUI.enabled;
            GUI.enabled = previous && accepted && studied && qualified && ready
                && interview != null && !interview.ProtocolDecided && interview.RemainingCooldown == 0;
            int available = 0;
            for (int i = 0; i < 3; i++)
            {
                if (interview != null && interview.HasTopic(i)) continue;
                int topic = i;
                if (GreenButton(new Rect(12f, 397f + available * 40f, rect.width - 24f, 33f),
                    ("SCP_079Topic" + i).Translate()))
                    ChooseInterviewer(topic);
                available++;
            }
            GUI.enabled = previous;
            if (interview != null && interview.AllTopicsRecorded && !interview.ProtocolDecided)
            {
                GUI.enabled = previous && accepted && studied && ready;
                if (GreenButton(new Rect(12f, 397f, rect.width - 24f, 34f),
                    "SCP_079SendReport".Translate())) DecideProtocol(false, interview);
                if (DangerButton(new Rect(12f, 439f, rect.width - 24f, 38f),
                    "SCP_079DirectLink".Translate()))
                    Find.WindowStack.Add(new Dialog_Scp079LinkConfirm(() => DecideProtocol(true, interview)));
                GUI.enabled = previous;
            }
            if (GreenButton(new Rect((rect.width - 140f) / 2f, rect.height - 41f, 140f, 32f),
                "SCP_TerminalClose".Translate())) Close();
        }

        private static void GreenPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, Panel);
            Color original = GUI.color;
            GUI.color = Green;
            Widgets.DrawBox(rect);
            GUI.color = original;
        }

        private static bool GreenButton(Rect rect, string label)
        {
            Widgets.DrawBoxSolid(rect, GUI.enabled && Mouse.IsOver(rect)
                ? new Color(0.08f, 0.21f, 0.11f) : Panel);
            Color original = GUI.color;
            GUI.color = GUI.enabled ? Green : Muted;
            Widgets.DrawBox(rect);
            GUI.color = original;
            ScpTerminalUi.Label(rect.ContractedBy(5f), label, GUI.enabled ? Green : Muted,
                GameFont.Small, TextAnchor.MiddleCenter);
            return Widgets.ButtonInvisible(rect);
        }

        internal static bool DangerButton(Rect rect, string label)
        {
            Widgets.DrawBoxSolid(rect, GUI.enabled && Mouse.IsOver(rect)
                ? new Color(0.24f, 0.045f, 0.045f) : new Color(0.10f, 0.015f, 0.02f));
            Color original = GUI.color;
            GUI.color = GUI.enabled ? new Color(1f, 0.30f, 0.28f) : Muted;
            Widgets.DrawBox(rect);
            GUI.color = original;
            ScpTerminalUi.Label(rect.ContractedBy(5f), label,
                GUI.enabled ? new Color(1f, 0.45f, 0.43f) : Muted,
                GameFont.Small, TextAnchor.MiddleCenter);
            return Widgets.ButtonInvisible(rect);
        }

        private void DecideProtocol(bool directLink, CompScp079 interview)
        {
            Thing terminal = CompFoundationAccessTerminal.Online(map);
            if (!interview.DecideProtocol(directLink, terminal)) return;
            Find.LetterStack.ReceiveLetter((directLink ? "SCP_079LinkTitle" : "SCP_079ReportTitle").Translate(),
                (directLink ? "SCP_079LinkResult" : "SCP_079ReportResult").Translate(),
                directLink ? RimWorld.LetterDefOf.NegativeEvent : RimWorld.LetterDefOf.PositiveEvent,
                computer);
        }

        private void ChooseInterviewer(int topic)
        {
            Thing terminal = CompFoundationAccessTerminal.Online(map);
            if (terminal == null) return;
            JobDef interviewJob = DefDatabase<JobDef>.GetNamedSilentFail("SCP_079Interview");
            if (interviewJob == null) return;
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            FoundationPersonnelComponent personnel = Current.Game.GetComponent<FoundationPersonnelComponent>();
            foreach (Pawn candidate in map.mapPawns.FreeColonistsSpawned)
            {
                Pawn pawn = candidate;
                if (pawn.Dead || pawn.Downed || pawn.Drafted || pawn.jobs == null
                    || personnel.LevelFor(pawn) < 2
                    || !pawn.CanReserveAndReach(computer, PathEndMode.Touch, Danger.Some)
                    || !pawn.CanReserveAndReach(terminal, PathEndMode.Touch, Danger.Some)) continue;
                options.Add(new FloatMenuOption(pawn.LabelShortCap, () => {
                    Job job = JobMaker.MakeJob(interviewJob, computer, terminal);
                    job.count = topic;
                    job.playerForced = true;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    Close();
                }));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("SCP_079NoInterviewer".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    public sealed class Dialog_Scp079Document : Window
    {
        private readonly int topic;
        private Vector2 scroll;
        public Dialog_Scp079Document(int topic) { this.topic = topic; doCloseX = true; doCloseButton = false; }
        public override Vector2 InitialSize { get { return new Vector2(660f, 500f); } }

        public override void DoWindowContents(Rect rect)
        {
            ScpTerminalUi.Panel(rect, new Color(0.025f, 0.055f, 0.035f));
            Rect header = new Rect(12f, 10f, rect.width - 24f, 54f);
            ScpTerminalUi.Panel(header, new Color(0.045f, 0.095f, 0.06f));
            ScpTerminalUi.Label(header.ContractedBy(12f),
                ("SCP_079Document" + topic + "Title").Translate(),
                new Color(0.34f, 0.88f, 0.46f), GameFont.Medium);
            Rect body = new Rect(12f, 75f, rect.width - 24f, rect.height - 130f);
            ScpTerminalUi.Panel(body, new Color(0.045f, 0.095f, 0.06f));
            Color original = GUI.color;
            GUI.color = new Color(0.83f, 0.92f, 0.84f);
            Widgets.LabelScrollable(body.ContractedBy(18f),
                ("SCP_079Document" + topic + "Body").Translate(), ref scroll);
            GUI.color = original;
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 44f, 140f, 32f),
                "SCP_TerminalClose".Translate())) Close();
        }
    }

    public sealed class Dialog_Scp079LinkConfirm : Window
    {
        private readonly Action confirmed;
        public Dialog_Scp079LinkConfirm(Action confirmed)
        {
            this.confirmed = confirmed;
            doCloseX = true;
            doCloseButton = false;
            forcePause = true;
        }
        public override Vector2 InitialSize { get { return new Vector2(640f, 280f); } }

        public override void DoWindowContents(Rect rect)
        {
            ScpTerminalUi.Panel(rect, new Color(0.06f, 0.015f, 0.02f));
            Rect header = new Rect(12f, 12f, rect.width - 24f, 52f);
            Widgets.DrawBoxSolid(header, new Color(0.17f, 0.025f, 0.035f));
            ScpTerminalUi.Label(header.ContractedBy(12f), "SCP_079DirectLinkTitle".Translate(),
                new Color(1f, 0.38f, 0.35f), GameFont.Medium);
            ScpTerminalUi.Label(new Rect(26f, 78f, rect.width - 52f, 100f),
                "SCP_079DirectLinkConfirm".Translate(), ScpTerminalUi.MainText,
                GameFont.Small, TextAnchor.UpperLeft);
            if (ScpTerminalUi.Button(new Rect(24f, rect.height - 56f, 200f, 36f),
                "SCP_079GoBack".Translate())) Close();
            if (Dialog_Scp079Interview.DangerButton(new Rect(rect.width - 224f, rect.height - 56f, 200f, 36f),
                "SCP_079Connect".Translate()))
            {
                confirmed();
                Close();
            }
        }
    }
}
