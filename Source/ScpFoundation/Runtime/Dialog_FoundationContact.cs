using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_FoundationContact : Window
    {
        private readonly Map map;
        private Vector2 scroll;
        private FoundationContactStage? shownStage;
        private int shownMessageCount;

        private sealed class Message
        {
            internal string sender;
            internal string when;
            internal string body;
            internal bool outgoing;
            internal bool system;

            internal Message(string sender, string when, string body, bool outgoing = false, bool system = false)
            {
                this.sender = sender;
                this.when = when;
                this.body = body;
                this.outgoing = outgoing;
                this.system = system;
            }
        }

        public override Vector2 InitialSize { get { return new Vector2(820, 650); } }

        public Dialog_FoundationContact(Map map)
        {
            this.map = map;
            doCloseX = true;
            doCloseButton = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            FoundationContactComponent contact = Current.Game.GetComponent<FoundationContactComponent>();
            FoundationContactProgress progress = contact.progress;
            bool online = CompFoundationAccessTerminal.Online(map) != null;
            bool discovered = contact.StatueDiscovered || progress.stage != FoundationContactStage.None;
            Text.Font = GameFont.Small;
            ScpTerminalUi.Panel(rect, ScpTerminalUi.Background);

            Rect header = new Rect(12f, 9f, rect.width - 24f, 53f);
            ScpTerminalUi.Panel(header);
            ScpTerminalUi.Label(new Rect(header.x + 13f, header.y + 4f, header.width - 190f, 25f),
                "SCP_ContactButton".Translate(), ScpTerminalUi.AccentColor, GameFont.Medium);
            ScpTerminalUi.Label(new Rect(header.x + 13f, header.y + 27f, header.width - 190f, 20f),
                "SCP_ContactChannel".Translate(), ScpTerminalUi.MutedText, GameFont.Tiny);
            Widgets.DrawBoxSolid(new Rect(header.xMax - 153f, header.y + 22f, 7f, 7f),
                online && discovered ? ScpTerminalUi.AccentColor : ScpTerminalUi.MutedText);
            ScpTerminalUi.Label(new Rect(header.xMax - 139f, header.y + 11f, 128f, 28f),
                (online && discovered ? "SCP_ContactLinkOnline" : "SCP_ContactLinkOffline").Translate(),
                online && discovered ? ScpTerminalUi.AccentColor : ScpTerminalUi.MutedText,
                GameFont.Small, TextAnchor.MiddleRight);

            if (!discovered)
            {
                Rect locked = new Rect(12f, 70f, rect.width - 24f, rect.height - 135f);
                ScpTerminalUi.Panel(locked);
                ScpTerminalUi.Label(locked.ContractedBy(24f), "SCP_ContactUnavailable".Translate(),
                    ScpTerminalUi.MutedText, GameFont.Medium, TextAnchor.MiddleCenter);
                if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 52f, 140f, 34f),
                    "SCP_TerminalClose".Translate())) Close();
                return;
            }

            string status = ("SCP_ContactStage_" + progress.stage).Translate();
            if (progress.stage == FoundationContactStage.AwaitingReply)
            {
                int remaining = (int)Math.Max(0L, (long)progress.sentTick + FoundationContactProgress.ReplyDelay
                    - Find.TickManager.TicksGame);
                status += "  " + (remaining > 0 ? "SCP_ContactRemaining".Translate((remaining / 2500f).ToString("0.0"))
                    : "SCP_ContactAwaitingLink".Translate());
            }
            if (progress.stage == FoundationContactStage.None && !contact.StatueDiscovered)
                status = "SCP_ContactNeedsStatue".Translate();
            if (!online) status += "  ·  " + "SCP_ContactLinkOffline".Translate();
            Rect statusRect = new Rect(12f, 70f, rect.width - 24f, 45f);
            ScpTerminalUi.Panel(statusRect);
            ScpTerminalUi.Label(statusRect.ContractedBy(10f), status, ScpTerminalUi.MutedText);
            if (!online) TooltipHandler.TipRegion(statusRect, "SCP_ContactOffline".Translate());

            List<Message> messages = Messages(progress, contact, Current.Game.GetComponent<ScpOuroborosArchiveComponent>());
            Rect view = new Rect(12f, 123f, rect.width - 24f, rect.height - 243f);
            ScpTerminalUi.Panel(view);
            float contentWidth = view.width - 16f;
            float bubbleWidth = Mathf.Min(570f, contentWidth - 32f);
            float[] heights = new float[messages.Count];
            float contentHeight = 8f;
            for (int i = 0; i < messages.Count; i++)
            {
                float width = messages[i].system ? contentWidth - 16f : bubbleWidth;
                heights[i] = 38f + Text.CalcHeight(messages[i].body, width - 24f);
                contentHeight += heights[i] + 12f;
            }
            if (shownStage != progress.stage || shownMessageCount != messages.Count)
            {
                scroll.y = Mathf.Max(0f, contentHeight - view.height);
                shownStage = progress.stage;
                shownMessageCount = messages.Count;
            }
            Widgets.BeginScrollView(view, ref scroll,
                new Rect(0f, 0f, contentWidth, Mathf.Max(view.height, contentHeight)));
            float y = 8f;
            for (int i = 0; i < messages.Count; i++)
            {
                Message message = messages[i];
                float width = message.system ? contentWidth - 16f : bubbleWidth;
                float x = message.outgoing ? contentWidth - width - 8f : 8f;
                Rect bubble = new Rect(x, y, width, heights[i]);
                ScpTerminalUi.Panel(bubble, message.system ? ScpTerminalUi.Background
                    : message.outgoing ? new Color(0.11f, 0.21f, 0.23f) : ScpTerminalUi.PanelColor);
                Widgets.DrawBoxSolid(new Rect(message.outgoing ? bubble.xMax - 3f : bubble.x,
                    bubble.y, 3f, bubble.height), ScpTerminalUi.AccentColor);
                ScpTerminalUi.Label(new Rect(x + 12f, y + 5f, width - 24f, 22f),
                    message.sender, ScpTerminalUi.AccentColor);
                if (!string.IsNullOrEmpty(message.when))
                    ScpTerminalUi.Label(new Rect(x + 12f, y + 5f, width - 24f, 22f),
                        message.when, ScpTerminalUi.MutedText, GameFont.Tiny, TextAnchor.MiddleRight);
                ScpTerminalUi.Label(new Rect(x + 12f, y + 31f, width - 24f, heights[i] - 36f),
                    message.body, ScpTerminalUi.MainText, GameFont.Small, TextAnchor.UpperLeft);
                y += heights[i] + 12f;
            }
            Widgets.EndScrollView();

            Rect actions = new Rect(12f, rect.height - 112f, rect.width - 24f, 47f);
            ScpTerminalUi.Panel(actions);
            bool wasEnabled = GUI.enabled;
            if (progress.stage == FoundationContactStage.None)
            {
                GUI.enabled = wasEnabled && online && contact.StatueDiscovered;
                if (ScpTerminalUi.Button(actions.ContractedBy(7f), "SCP_ContactRequest".Translate())) contact.Request(map);
            }
            else if (progress.stage == FoundationContactStage.ReplyReceived)
            {
                GUI.enabled = wasEnabled && online;
                if (ScpTerminalUi.Button(actions.ContractedBy(7f), "SCP_ContactConfirm".Translate())) contact.Confirm(map);
            }
            else if (progress.stage == FoundationContactStage.Established)
            {
                int count = 3;
                float gap = 7f, width = (actions.width - 14f - gap * (count - 1)) / count;
                if (ScpTerminalUi.Button(new Rect(actions.x + 7f, actions.y + 7f, width, 33f),
                    "SCP_AssignmentButton".Translate()))
                    Find.WindowStack.Add(new Dialog_FoundationAssignment(map));
                if (ScpTerminalUi.Button(new Rect(actions.x + 7f + width + gap, actions.y + 7f, width, 33f),
                    "SCP_SupportButton".Translate()))
                    Find.WindowStack.Add(new Dialog_FoundationSupport(map));
                if (ScpTerminalUi.Button(new Rect(actions.x + 7f + (width + gap) * 2f, actions.y + 7f, width, 33f),
                    "SCP_PersonnelButton".Translate()))
                    Find.WindowStack.Add(new Dialog_FoundationPersonnel(map));
            }
            else ScpTerminalUi.Label(actions.ContractedBy(9f), "SCP_ContactStage_AwaitingReply".Translate(),
                ScpTerminalUi.MutedText, GameFont.Small, TextAnchor.MiddleCenter);
            GUI.enabled = wasEnabled;
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 52f, 140f, 34f),
                "SCP_TerminalClose".Translate())) Close();
        }

        private static string When(int tick)
        {
            return tick < 0 ? "" : "SCP_ContactWhen".Translate(tick / 60000 + 1, ((tick / 2500) % 24).ToString("00")).ToString();
        }

        private static List<Message> Messages(FoundationContactProgress progress,
            FoundationContactComponent contact,
            ScpOuroborosArchiveComponent archive)
        {
            var messages = new List<Message>
            {
                new Message("SCP_ContactProtocol".Translate(), "", "SCP_ContactHelp".Translate(), system: true)
            };
            if (progress.stage == FoundationContactStage.None) return messages;
            if (!progress.initiatedByFoundation)
                messages.Add(new Message("SCP_ContactFromColony".Translate(), When(progress.sentTick),
                    "SCP_ContactRequestText".Translate(), outgoing: true));
            if (progress.stage < FoundationContactStage.ReplyReceived) return messages;
            messages.Add(new Message("SCP_ContactReplyTitle".Translate(), When(progress.receivedTick),
                "SCP_ContactReplyText".Translate()));
            if (progress.stage == FoundationContactStage.Established)
            {
                messages.Add(new Message("SCP_ContactFromColony".Translate(), When(progress.confirmedTick),
                    "SCP_ContactConfirmText".Translate(), outgoing: true));
                messages.Add(new Message("SCP_ContactProtocol".Translate(), "",
                    "SCP_ContactEstablishedText".Translate(), system: true));
                if (archive != null && archive.AftermathTick >= 0)
                    messages.Add(new Message("SCP_882AftermathSender".Translate(), When(archive.AftermathTick),
                        (archive.AftermathSecured ? "SCP_882AftermathMessage" : "SCP_882AftermathLostMessage").Translate()));
                if (archive != null && archive.AnalysisTick >= 0)
                    messages.Add(new Message("SCP_882AftermathSender".Translate(), When(archive.AnalysisTick),
                        "SCP_882AnalysisMessage".Translate()));
                if (archive != null && archive.GreenVisitTick >= 0)
                    messages.Add(new Message("SCP_O5GreenSender".Translate(), When(archive.GreenVisitTick),
                        "SCP_O5GreenFollowupMessage".Translate()));
                if (archive != null && archive.FinalReportTick >= 0)
                {
                    messages.Add(new Message("SCP_ContactFromColony".Translate(), When(archive.FinalReportTick),
                        "SCP_882FinalReportOutgoing".Translate(), outgoing: true));
                    messages.Add(new Message("SCP_O5GreenSender".Translate(), When(archive.FinalReportTick),
                        "SCP_882FinalReportReply".Translate()));
                }
                if (archive != null && archive.SignalAnalysisTick >= 0)
                    messages.Add(new Message("SCP_O5GreenSender".Translate(), When(archive.SignalAnalysisTick),
                        "SCP_SignalAnalysisMessage".Translate()));
                if (archive != null && archive.SignalRecoveryTick >= 0)
                    messages.Add(new Message("SCP_ContactFromColony".Translate(), When(archive.SignalRecoveryTick),
                        "SCP_SignalRecoveryMessage".Translate(), outgoing: true));
                if (archive != null && archive.ChurchReportTick >= 0)
                    messages.Add(new Message("SCP_ContactFromColony".Translate(), When(archive.ChurchReportTick),
                        "SCP_ChurchReportOutgoing".Translate(), outgoing: true));
                if (archive != null && archive.ChurchResolutionTick >= 0)
                    messages.Add(new Message("SCP_O5GreenSender".Translate(), When(archive.ChurchResolutionTick),
                        "SCP_ChurchResolutionMessage".Translate()));
                if (archive != null && archive.OuroborosFinalTick >= 0)
                {
                    messages.Add(new Message("SCP_ContactFromColony".Translate(), When(archive.OuroborosFinalTick),
                        "SCP_OuroborosFinalOutgoing".Translate(), outgoing: true));
                    messages.Add(new Message("SCP_O5GreenSender".Translate(), When(archive.OuroborosFinalTick),
                        "SCP_OuroborosFinalReply".Translate()));
                }
                if (contact.Scp079InterviewOfferTick >= 0)
                    messages.Add(new Message("SCP_O5GreenSender".Translate(),
                        When(contact.Scp079InterviewOfferTick), "SCP_079AssignmentOffer".Translate()));
                if (contact.Scp079InterviewAcceptedTick >= 0)
                    messages.Add(new Message("SCP_ContactFromColony".Translate(),
                        When(contact.Scp079InterviewAcceptedTick), "SCP_079AssignmentAccepted".Translate(), outgoing: true));
                CompScp079 interview = Find.Maps.Where(map => map.IsPlayerHome)
                    .Select(map => contact.Scp079AtHome(map)).Where(thing => thing != null)
                    .Select(thing => thing.TryGetComp<CompScp079>()).FirstOrDefault(comp => comp.ProtocolDecisionTick >= 0);
                if (interview != null)
                {
                    messages.Add(new Message("SCP_ContactFromColony".Translate(),
                        When(interview.ProtocolDecisionTick), (interview.DirectLinkChosen
                            ? "SCP_079AssignmentLinkReport" : "SCP_079AssignmentReport").Translate(), outgoing: true));
                    messages.Add(new Message("SCP_O5GreenSender".Translate(),
                        When(interview.ProtocolDecisionTick), (interview.DirectLinkChosen
                            ? "SCP_079AssignmentLinkReply" : "SCP_079AssignmentReply").Translate()));
                    if (interview.DirectLinkWarningTick >= 0)
                        messages.Add(new Message("SCP_O5GreenSender".Translate(),
                            When(interview.DirectLinkWarningTick), "SCP_079WarningBody".Translate()));
                }
            }
            return messages;
        }
    }
}
