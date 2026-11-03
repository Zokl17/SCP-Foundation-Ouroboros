using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Storytelling
{
    public sealed class Scp650ReturnComponent : GameComponent
    {
        public bool returned;
        private int nextOfferTick;
        public Scp650ReturnComponent(Game game) { }

        public bool ResearchComplete
        {
            get
            {
                var definition = ScpInteractionDefOf.SCP_Statue.GetCompProperties<CompProperties_Scp>().anomaly;
                ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                return Scp650ReturnPolicy.CanOffer(Current.Game.GetComponent<FoundationAssignmentComponent>()
                    .progress.stage == FoundationAssignmentStage.Completed,
                    knowledge.LevelFor(definition) >= KnowledgeLevel.Documented,
                    knowledge.ExperimentsComplete(definition), returned);
            }
        }

        public static Thing SubjectOn(Map map)
        {
            return map == null || !map.IsPlayerHome ? null
                : map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCP_Statue).FirstOrDefault(thing =>
                    thing.TryGetComp<CompScp>().IsInCustody && CompScpPost.IsObjectAtPost(thing));
        }

        public Map EligibleMap()
        {
            if (!ModsConfig.RoyaltyActive || !ResearchComplete) return null;
            Faction faction = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            if (faction == null || faction.HostileTo(Faction.OfPlayer)) return null;
            return Find.Maps.FirstOrDefault(map => SubjectOn(map) != null
                && CompFoundationAccessTerminal.Online(map) != null);
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (returned || now % 2500 != 0 || now < nextOfferTick) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_650Return");
            if (script == null || Find.QuestManager.QuestsListForReading.Any(quest => quest.root == script
                && (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing))) return;
            if (EligibleMap() == null) return;
            if (QuestUtility.GenerateQuestAndMakeAvailable(script, 0f) != null)
                nextOfferTick = now + 60000;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref returned, "scp650ReturnedToFoundation", false);
            Scribe_Values.Look(ref nextOfferTick, "scp650ReturnNextOfferTick", 0);
        }
    }

    public sealed class QuestNode_Scp650Return : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return Current.Game.GetComponent<Scp650ReturnComponent>().EligibleMap() != null;
        }

        protected override void RunInt()
        {
            Map map = Current.Game.GetComponent<Scp650ReturnComponent>().EligibleMap();
            QuestGen.slate.Set("map", map);
            QuestGen.slate.Set("scp650ReturnFaction", Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction);
            QuestGen.slate.Set("scp650ReturnItems", new List<ThingDefCount> { new ThingDefCount(ScpInteractionDefOf.SCP_Statue, 1) });
            QuestGen.quest.AddPart(new QuestPart_Scp650Return {
                map = map, subject = Scp650ReturnComponent.SubjectOn(map),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                completedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp650Returned"),
                failedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp650ReturnFailed")
            });
        }
    }

    public sealed class QuestNode_BindScp650ReturnShuttle : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }
        protected override void RunInt()
        {
            QuestPart_Scp650Return part = QuestGen.quest.PartsListForReading.OfType<QuestPart_Scp650Return>().Single();
            part.ship = QuestGen.slate.Get<TransportShip>("scp650ReturnShip");
            string tag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("Scp650ReturnShuttle");
            if (part.ship.shipThing.questTags == null) part.ship.shipThing.questTags = new List<string>();
            part.ship.shipThing.questTags.Add(tag);
            part.sentSatisfiedSignal = QuestGenUtility.QuestTagSignal(tag, "SentSatisfied");
            part.sentUnsatisfiedSignal = QuestGenUtility.QuestTagSignal(tag, "SentUnsatisfied");
        }
    }

    public sealed class QuestPart_Scp650Return : QuestPartActivable
    {
        public Map map;
        public Thing subject;
        public TransportShip ship;
        public string completedSignal, failedSignal, sentSatisfiedSignal, sentUnsatisfiedSignal;
        private bool finished, arrivalAnnounced;

        public static QuestPart_Scp650Return For(Thing thing)
        {
            return thing == null || Find.QuestManager == null ? null
                : Find.QuestManager.QuestsListForReading.Where(quest => quest.State == QuestState.Ongoing)
                    .SelectMany(quest => quest.PartsListForReading.OfType<QuestPart_Scp650Return>())
                    .FirstOrDefault(part => part.subject == thing && !part.finished && part.State == QuestPartState.Enabled);
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (finished || State != QuestPartState.Enabled) return;
            if (signal.tag == sentSatisfiedSignal)
            {
                Scp650ReturnComponent state = Current.Game.GetComponent<Scp650ReturnComponent>();
                bool correctCargo = ship != null && ship.TransporterComp.innerContainer.Contains(subject);
                if (!Scp650ReturnPolicy.CanReward(quest.State == QuestState.Ongoing, true, correctCargo, state.returned))
                { Fail(); return; }
                finished = true;
                state.returned = true;
                // The first encounter and the completed dossier survive the physical transfer.
                Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("650");
                foreach (Map home in Find.Maps)
                foreach (CompScpPost post in CompScpPost.All(home).Where(post => post.BoundObject == subject))
                { post.Bind(null); post.useShifts = false; }
                FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
                support.progress.trust = System.Math.Min(FoundationSupportProgress.MaximumTrust,
                    support.progress.trust + Scp650ReturnPolicy.TrustReward);
                Find.SignalManager.SendSignal(new Signal(completedSignal));
            }
            else if (signal.tag == sentUnsatisfiedSignal) Fail();
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 250 != 0) return;
            if (map == null || !Find.Maps.Contains(map) || subject == null || subject.Destroyed
                || ship == null || ship.Disposed || ship.shipThing.Destroyed)
            { Fail(); return; }
            if (!arrivalAnnounced && ship.ShipExistsAndIsSpawned)
            {
                arrivalAnnounced = true;
                Find.LetterStack.ReceiveLetter("SCP_650ReturnArrivedTitle".Translate(),
                    "SCP_650ReturnArrivedBody".Translate(), LetterDefOf.NeutralEvent, ship.shipThing);
            }
        }

        private void Fail()
        {
            if (finished) return;
            finished = true;
            Find.SignalManager.SendSignal(new Signal(failedSignal));
        }

        private void ReturnUnsentCargo()
        {
            // A cancelled or failed pickup must not take the player's cargo away.
            if (!Current.Game.GetComponent<Scp650ReturnComponent>().returned && map != null
                && Find.Maps.Contains(map) && ship != null && ship.ShipExistsAndIsSpawned)
                ship.TransporterComp.innerContainer.TryDropAll(ship.shipThing.Position, map, ThingPlaceMode.Near);
        }

        public override void Notify_PreCleanup()
        {
            ReturnUnsentCargo();
            base.Notify_PreCleanup();
        }

        public override void Cleanup()
        {
            ReturnUnsentCargo();
            base.Cleanup();
        }

        public FloatMenuOption LoadOption(Pawn pawn)
        {
            if (ship == null || !ship.ShipExistsAndIsSpawned) return null;
            string label = "SCP_650ReturnLoad".Translate();
            if (pawn == null || !pawn.IsColonistPlayerControlled || pawn.Drafted || pawn.Downed
                || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                || !pawn.CanReserveAndReach(subject, PathEndMode.Touch, Danger.Some, ShiftPolicy.ReservationSlots, 1)
                || !pawn.CanReach(ship.shipThing, PathEndMode.Touch, Danger.Some))
                return new FloatMenuOption(label + ": " + "SCP_650ReturnCannotLoad".Translate(), null);
            return new FloatMenuOption(label, () => {
                Job order = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("SCP_Load650ForReturn"), subject, ship.shipThing);
                order.count = 1;
                order.ignoreForbidden = true;
                pawn.jobs.TryTakeOrderedJob(order, JobTag.Misc);
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scp650ReturnMap");
            Scribe_References.Look(ref subject, "scp650ReturnSubject");
            Scribe_References.Look(ref ship, "scp650ReturnShip");
            Scribe_Values.Look(ref completedSignal, "scp650ReturnCompletedSignal");
            Scribe_Values.Look(ref failedSignal, "scp650ReturnFailedSignal");
            Scribe_Values.Look(ref sentSatisfiedSignal, "scp650ReturnSentSatisfiedSignal");
            Scribe_Values.Look(ref sentUnsatisfiedSignal, "scp650ReturnSentUnsatisfiedSignal");
            Scribe_Values.Look(ref finished, "scp650ReturnFinished", false);
            Scribe_Values.Look(ref arrivalAnnounced, "scp650ReturnArrivalAnnounced", false);
        }
    }
}
