using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Storytelling;
using ScpFoundation.Interactions;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_FoundationAssignment : Window
    {
        private readonly Map map;
        private Vector2 scroll;
        public override Vector2 InitialSize { get { return new Vector2(880, 680); } }
        public Dialog_FoundationAssignment(Map map)
        {
            this.map = map; doCloseX = true; doCloseButton = false;
        }
        public override void DoWindowContents(Rect rect)
        {
            FoundationAssignmentComponent assignment = Current.Game.GetComponent<FoundationAssignmentComponent>();
            FoundationAssignmentProgress progress = assignment.progress;
            FoundationContactComponent contact = Current.Game.GetComponent<FoundationContactComponent>();
            bool firstCompleted = progress.stage == FoundationAssignmentStage.Completed;
            bool offer079 = firstCompleted && contact.Scp079InterviewOfferTick >= 0;
            Thing computer = offer079 ? contact.Scp079AtHome(map) : null;
            CompScp079 interview = computer == null ? null : computer.TryGetComp<CompScp079>();
            Text.Font = GameFont.Small;
            ScpTerminalUi.Panel(rect, ScpTerminalUi.Background);
            Rect header = new Rect(12f, 9f, rect.width - 24f, 54f);
            ScpTerminalUi.Panel(header);
            ScpTerminalUi.Label(header.ContractedBy(13f), (offer079 ? "SCP_079AssignmentTitle"
                : firstCompleted ? "SCP_AssignmentNoneTitle" : "SCP_AssignmentTitle").Translate(),
                ScpTerminalUi.AccentColor, GameFont.Medium);
            string text;
            if (offer079)
            {
                text = "SCP_079AssignmentBrief".Translate();
                if (contact.Scp079InterviewAcceptedTick >= 0)
                {
                    text += "\n\n" + "SCP_079AssignmentAccepted".Translate();
                    if (interview != null)
                        text += "\n\n" + (interview.ProtocolDecided ? "SCP_079AssignmentComplete"
                            : interview.AllTopicsRecorded ? "SCP_079AssignmentReady"
                            : "SCP_079AssignmentInProgress").Translate();
                }
            }
            else if (firstCompleted)
                text = "SCP_AssignmentNone".Translate();
            else
            {
                text = "SCP_AssignmentInstructions".Translate();
                if (!assignment.ContactEstablished) text += "\n\n" + "SCP_AssignmentNeedsContact".Translate();
                else
                {
                    text += "\n\n" + ("SCP_AssignmentStage_" + progress.stage).Translate();
                    if (progress.stage != FoundationAssignmentStage.Available)
                        text += "\n" + "SCP_AssignmentSubject".Translate(assignment.SubjectLabel);
                    if (progress.stage == FoundationAssignmentStage.Active)
                    {
                        text += "\n" + "SCP_AssignmentProgress".Translate((progress.stableTicks / 2500f).ToString("0.0"));
                        text += "\n" + (assignment.Qualified ? "SCP_AssignmentCounting" : "SCP_AssignmentWaiting").Translate();
                    }
                }
            }
            bool online = CompFoundationAccessTerminal.Online(map) != null;
            if (!online) text += "\n\n" + "SCP_ContactOffline".Translate();
            Rect report = new Rect(12f, 72f, rect.width - 24f, rect.height - 226f);
            ScpTerminalUi.Panel(report);
            Color previousColor = GUI.color;
            GUI.color = ScpTerminalUi.MainText;
            Widgets.LabelScrollable(report.ContractedBy(12f), text, ref scroll);
            GUI.color = previousColor;
            bool previous = GUI.enabled;
            bool show079 = firstCompleted && !offer079
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("079");
            GUI.enabled = previous && online && assignment.ContactEstablished
                && progress.stage == FoundationAssignmentStage.Completed;
            if (show079 && ScpTerminalUi.Button(new Rect(12f, rect.height - 142f, rect.width - 24f, 34f),
                "SCP_079RequestQuest".Translate()))
            {
                QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_079Recovery");
                Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 600f);
                Messages.Message((quest == null ? "SCP_079QuestUnavailable" : "SCP_079QuestOffered").Translate(),
                    quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
            }
            GUI.enabled = previous && online && assignment.ContactEstablished;
            if (progress.stage == FoundationAssignmentStage.Available)
            {
                if (ScpTerminalUi.Button(new Rect(12f, rect.height - 101f, rect.width - 24f, 34f),
                    "SCP_ContactConfirm".Translate()))
                    assignment.Accept(map);
            }
            else if (progress.stage == FoundationAssignmentStage.Ready
                && ScpTerminalUi.Button(new Rect(12f, rect.height - 101f, rect.width - 24f, 34f),
                    "SCP_AssignmentSubmit".Translate()))
                assignment.Submit(map);
            else if (offer079 && contact.Scp079InterviewAcceptedTick < 0)
            {
                GUI.enabled = previous && online && computer != null;
                if (ScpTerminalUi.Button(new Rect(12f, rect.height - 101f, rect.width - 24f, 34f),
                    "SCP_079AcceptAssignment".Translate()))
                    contact.AcceptScp079Interview(map);
            }
            GUI.enabled = previous;
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 51f, 140f, 34f),
                "SCP_TerminalClose".Translate())) Close();
        }
    }
}
