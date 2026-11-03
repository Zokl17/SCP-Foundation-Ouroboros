using System.Linq;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using RimWorld;
using ScpFoundation.Anomalies;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class FoundationContactComponent : GameComponent
    {
        public readonly FoundationContactProgress progress = new FoundationContactProgress();
        private bool ouroborosOffered;
        private int scp079InterviewOfferTick = -1;
        private int scp079InterviewAcceptedTick = -1;
        public FoundationContactComponent(Game game) { }

        public int Scp079InterviewOfferTick { get { return scp079InterviewOfferTick; } }
        public int Scp079InterviewAcceptedTick { get { return scp079InterviewAcceptedTick; } }

        public Thing Scp079AtHome(Map map)
        {
            ThingDef computer = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_079Computer");
            return map != null && map.IsPlayerHome && computer != null
                ? map.listerThings.ThingsOfDef(computer).FirstOrDefault(thing => thing.Spawned
                    && thing.TryGetComp<CompScp079>() != null) : null;
        }

        public bool AcceptScp079Interview(Map map)
        {
            if (progress.stage != FoundationContactStage.Established || scp079InterviewOfferTick < 0
                || scp079InterviewAcceptedTick >= 0 || Scp079AtHome(map) == null
                || CompFoundationAccessTerminal.Online(map) == null) return false;
            scp079InterviewAcceptedTick = Find.TickManager.TicksGame;
            return true;
        }

        public bool OfferOuroboros(Map map, bool debugRepeat = false)
        {
            if (progress.stage != FoundationContactStage.Established || !debugRepeat && ouroborosOffered
                || map == null || !map.IsPlayerHome || CompFoundationAccessTerminal.Online(map) == null) return false;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_OuroborosOpening");
            if (script == null || Find.QuestManager.QuestsListForReading.Any(quest => quest.root == script
                && (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing))) return false;
            Quest offered = QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            if (offered == null) return false;
            ouroborosOffered = true;
            return true;
        }

        public bool StatueDiscovered
        {
            get
            {
                var definition = ScpInteractionDefOf.SCP_Statue.GetCompProperties<CompProperties_Scp>().anomaly;
                var knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                return knowledge.HasEncountered(definition) || knowledge.LevelFor(definition) > KnowledgeLevel.Unknown;
            }
        }
        public bool Request(Map map)
        {
            return progress.Request(Find.TickManager.TicksGame, CompFoundationAccessTerminal.Online(map) != null, StatueDiscovered);
        }
        public bool Confirm(Map map)
        {
            if (!progress.Confirm(Find.TickManager.TicksGame, CompFoundationAccessTerminal.Online(map) != null)) return false;
            Current.Game.GetComponent<FoundationAssignmentComponent>().Accept(map);
            return true;
        }
        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 0) return;
            Thing terminal = Find.Maps.Select(map => CompFoundationAccessTerminal.Online(map)).FirstOrDefault(thing => thing != null);
            if (progress.Introduce(terminal != null))
                Find.LetterStack.ReceiveLetter("SCP_TerminalHintTitle".Translate(), "SCP_TerminalHintBody".Translate(),
                    LetterDefOf.NeutralEvent, terminal);
            if (progress.CanIntroduceStatue(now, terminal != null)
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("650"))
            {
                IncidentDef arrival = ScpInteractionDefOf.SCP_Discover650;
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, terminal.Map);
                if (arrival != null && arrival.Worker.TryExecute(parms)) return;
            }
            if (progress.stage == FoundationContactStage.Established)
            {
                if (scp079InterviewOfferTick < 0 && Current.Game.GetComponent<FoundationAssignmentComponent>()
                    .progress.stage == FoundationAssignmentStage.Completed)
                {
                    Map home = Find.Maps.FirstOrDefault(candidate => Scp079AtHome(candidate) != null);
                    Thing computer = Scp079AtHome(home);
                    if (computer != null)
                    {
                        scp079InterviewOfferTick = now;
                        Find.LetterStack.ReceiveLetter("SCP_079AssignmentTitle".Translate(),
                            "SCP_079AssignmentOffer".Translate(), LetterDefOf.NeutralEvent, computer);
                    }
                }
                if (!ouroborosOffered)
                    OfferOuroboros(Find.Maps.FirstOrDefault(map => map.IsPlayerHome
                        && CompFoundationAccessTerminal.Online(map) != null));
                return;
            }
            if (progress.stage != FoundationContactStage.None && progress.stage != FoundationContactStage.AwaitingReply) return;
            if (progress.Deliver(now, terminal != null))
                Find.LetterStack.ReceiveLetter("SCP_ContactReplyTitle".Translate(), "SCP_ContactReplyNotice".Translate(),
                    LetterDefOf.NeutralEvent, terminal);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref progress.stage, "scpFoundationContactStage", FoundationContactStage.None);
            Scribe_Values.Look(ref progress.initiatedByFoundation, "scpFoundationContactInitiated", false);
            Scribe_Values.Look(ref progress.terminalHintShown, "scpFoundationTerminalHint", false);
            Scribe_Values.Look(ref progress.sentTick, "scpFoundationContactSent", -1);
            Scribe_Values.Look(ref progress.receivedTick, "scpFoundationContactReceived", -1);
            Scribe_Values.Look(ref progress.confirmedTick, "scpFoundationContactConfirmed", -1);
            Scribe_Values.Look(ref ouroborosOffered, "scpOuroborosOpeningOffered", false);
            Scribe_Values.Look(ref scp079InterviewOfferTick, "scp079InterviewOfferTick", -1);
            Scribe_Values.Look(ref scp079InterviewAcceptedTick, "scp079InterviewAcceptedTick", -1);
            Scribe_Values.Look(ref progress.firstOnlineTick, "scpFoundationFirstOnlineTick", -1);
        }
    }
}
