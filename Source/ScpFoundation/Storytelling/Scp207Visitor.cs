using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using ScpFoundation.Anomalies;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class IncidentWorker_Scp207Visitor : IncidentWorker_GiveQuest
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && map.IsPlayerHome && Current.Game != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("207")
                && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms) || !base.TryExecuteWorker(parms)) return false;
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("207");
            return true;
        }
    }

    public sealed class QuestNode_Scp207Visitor : QuestNode_Root_WandererJoin_WalkIn
    {
        private bool warningSent;
        private string acceptSignal;
        private string rejectSignal;

        public override Pawn GeneratePawn()
        {
            // A pooled world pawn can belong to a hostile faction. This visitor needs a fresh, unaffiliated pawn.
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Villager, null, forceGenerateNewPawn: true));
            HediffDef effect = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_207Overdrive");
            ThingDef bottleDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_207Bottle");
            if (pawn == null || pawn.health == null || effect == null || bottleDef == null) return pawn;
            Hediff_Scp207Overdrive overdrive = pawn.health.AddHediff(effect) as Hediff_Scp207Overdrive;
            if (overdrive != null)
            {
                overdrive.Severity = 0.6f;
                overdrive.MarkVisitorDose();
            }
            Thing bottle = ThingMaker.MakeThing(bottleDef);
            if (pawn.inventory == null || !pawn.inventory.innerContainer.TryAdd(bottle))
                bottle.Destroy(DestroyMode.Vanish);
            return pawn;
        }

        public override void SendLetter_NewTemp(Quest quest, Pawn pawn, Map map)
        {
            ChoiceLetter_Scp207Visitor letter = LetterMaker.MakeLetter(
                "SCP_207VisitorOfferTitle".Translate(),
                "SCP_207VisitorOfferBody".Translate(pawn.LabelShortCap),
                DefDatabase<LetterDef>.GetNamed("SCP_207VisitorOffer"), null, quest) as ChoiceLetter_Scp207Visitor;
            letter.visitor = pawn;
            letter.signalAccept = acceptSignal;
            letter.signalReject = rejectSignal;
            letter.overrideMap = map;
            letter.StartTimeout(GenDate.TicksPerDay);
            Find.LetterStack.ReceiveLetter(letter);
            Warn(pawn);
        }

        protected override void AddSpawnPawnQuestParts(Quest quest, Map map, Pawn pawn)
        {
            acceptSignal = QuestGenUtility.HardcodedSignalWithQuestID("Accept");
            rejectSignal = QuestGenUtility.HardcodedSignalWithQuestID("Reject");

            // As with Anomaly's waiting visitors, the pawn walks in before the offer is answered.
            IntVec3 entry;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out entry, map, CellFinder.EdgeRoadChance_Friendly))
                entry = CellFinder.RandomClosewalkCellNear(map.Center, map, 8);
            GenSpawn.Spawn(pawn, entry, map);
            IntVec3 waiting;
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(pawn, out waiting)) waiting = entry;
            LordMaker.MakeNewLord(pawn.Faction, new LordJob_Scp207Visitor(waiting), map)
                .AddPawn(pawn);
            quest.AddPart(new QuestPart_Scp207Join { pawn = pawn, inSignal = acceptSignal });

            QuestGen_Signal.Signal(quest, acceptSignal, () =>
            {
                QuestGen_End.End(quest, QuestEndOutcome.Success);
            });
            QuestGen_Signal.Signal(quest, rejectSignal, () =>
            {
                QuestGen_Misc.Leave(quest, new[] { pawn }, sendStandardLetter: false);
                QuestGen_End.End(quest, QuestEndOutcome.Fail);
            });
        }

        private void Warn(Pawn pawn)
        {
            if (warningSent) return;
            warningSent = true;
            Find.LetterStack.ReceiveLetter("SCP_207VisitorWarningTitle".Translate(),
                "SCP_207VisitorWarningBody".Translate(pawn.LabelShortCap),
                LetterDefOf.NegativeEvent, pawn);
        }
    }

    public sealed class LordJob_Scp207Visitor : LordJob_WaitForDurationThenExit
    {
        private IntVec3 waiting;

        public LordJob_Scp207Visitor() { }

        public LordJob_Scp207Visitor(IntVec3 waiting) : base(waiting, GenDate.TicksPerDay)
        {
            this.waiting = waiting;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref waiting, "scp207Waiting");
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = base.CreateGraph();
            LordToil wander = graph.StartingToil;
            LordToil travel = new LordToil_Scp207Travel(waiting);
            graph.AddToil(travel);
            graph.StartingToil = travel;
            Transition arrival = new Transition(travel, wander);
            arrival.AddTrigger(new Trigger_Memo("TravelArrived"));
            graph.AddTransition(arrival);
            return graph;
        }
    }

    public sealed class LordToil_Scp207Travel : LordToil_Travel
    {
        public LordToil_Scp207Travel(IntVec3 waiting) : base(waiting) { }

        public override void UpdateAllDuties()
        {
            base.UpdateAllDuties();
            foreach (Pawn pawn in lord.ownedPawns)
            {
                PawnDuty duty = pawn.mindState.duty;
                if (duty == null) continue;
                duty.def = DutyDefOf.TravelOrWait;
                duty.locomotion = LocomotionUrgency.Sprint;
            }
        }
    }

    public sealed class QuestPart_Scp207Join : QuestPart
    {
        public Pawn pawn;
        public string inSignal;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "scp207JoinPawn");
            Scribe_Values.Look(ref inSignal, "scp207JoinSignal");
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal || pawn == null || pawn.Destroyed || pawn.Dead) return;
            JoinPawn(pawn);
        }

        public static void JoinPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Faction == Faction.OfPlayer) return;
            Lord previous = pawn.GetLord();
            if (previous != null) previous.RemovePawn(pawn);
            pawn.SetFaction(Faction.OfPlayer);
            if (pawn.jobs != null && pawn.CurJob != null)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }

    public sealed class ChoiceLetter_Scp207Visitor : ChoiceLetter_AcceptJoiner
    {
        public Pawn visitor;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                bool first = true;
                foreach (DiaOption option in base.Choices)
                {
                    if (first && !ArchivedOnly && option.action != null)
                    {
                        Action accept = option.action;
                        option.action = () =>
                        {
                            QuestPart_Scp207Join.JoinPawn(visitor);
                            accept();
                            if (quest != null && (quest.State == QuestState.Ongoing
                                || quest.State == QuestState.NotYetAccepted))
                                quest.End(QuestEndOutcome.Success, false, false);
                        };
                    }
                    first = false;
                    yield return option;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref visitor, "scp207Visitor");
        }
    }
}
