using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    // Uses the same shuttle and Epsilon-11 pawn setup as the SCP-294 transfer.
    public sealed class QuestNode_ScpCreatureTransfer : QuestNode
    {
        public string species;

        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            if (map == null || !map.IsPlayerHome || Current.Game == null || QuestPart_ScpCreatureTransfer.ActiveFor(map, species))
                return false;
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            Faction faction = support.FoundationFaction;
            var anomaly = DefDatabase<ScpFoundation.Definitions.ScpDef>.GetNamedSilentFail("SCP_Anomaly" + species);
            if (!support.progress.unlocked || !Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                || faction == null || faction.HostileTo(Faction.OfPlayer)
                || anomaly == null || Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(anomaly)
                || Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived(species)) return false;
            return species == "682" ? map.mapPawns.FreeColonistsSpawnedCount >= 4
                : species == "939" && map.mapPawns.FreeColonistsSpawnedCount >= 2;
        }

        protected override void RunInt()
        {
            Map map = QuestGen.slate.Get<Map>("map");
            Faction faction = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            PawnKindDef specialist = DefDatabase<PawnKindDef>.GetNamed("SCP_MTF_Epsilon11");
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("SCP_MTF_Epsilon11Rifleman");
            ThingDef rifle = DefDatabase<ThingDef>.GetNamed("SCP_Nu7HKCarbine");
            ThingDef emp = DefDatabase<ThingDef>.GetNamed("SCP_FoundationEMPRifle");
            var squad = new List<Pawn>();
            for (int i = 0; i < 5; i++)
            {
                PawnKindDef role = i == 0 ? specialist : kind;
                squad.Add(FoundationSupportComponent.GenerateEquippedPawn(role, faction, i == 0 ? emp : rifle,
                    QuestGen_Pawns.GeneratePawn(QuestGen.quest, role, faction)));
            }
            var specimens = new List<Thing>();
            specimens.Add(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(species == "682" ? "SCP_682Object" : "SCP_939Object")));
            if (species == "939") specimens.Add(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_939ObjectThree")));
            QuestGen.slate.Set("scpCreatureFaction", faction);
            QuestGen.slate.Set("scpCreaturePawns", squad);
            QuestGen.slate.Set("scpCreatureContents", squad.Cast<Thing>().Concat(specimens).ToList());
            QuestGen.quest.AddPart(new QuestPart_ScpCreatureTransfer {
                map = map, species = species, squad = squad, specimens = specimens,
                inSignalEnable = QuestGen.quest.InitiateSignal,
                securedSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpCreatureSecured"),
                documentedSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpCreatureDocumented"),
                failedSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpCreatureFailed")
            });
        }
    }

    public sealed class QuestPart_ScpCreatureTransfer : QuestPartActivable
    {
        public Map map;
        public string species;
        public List<Pawn> squad;
        public List<Thing> specimens;
        public string securedSignal, documentedSignal, failedSignal;
        private bool delivered, secured, finished;
        private int lastGuardOrderTick = -1000;
        private IntVec3 guardedCell = IntVec3.Invalid;

        public static bool ActiveFor(Map target, string kind)
        {
            return target != null && Find.QuestManager != null
                && Find.QuestManager.QuestsListForReading.Any(quest =>
                    (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing)
                    && quest.PartsListForReading.OfType<QuestPart_ScpCreatureTransfer>()
                        .Any(part => part.map == target && part.species == kind));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scpCreatureMap");
            Scribe_Values.Look(ref species, "scpCreatureSpecies");
            Scribe_Collections.Look(ref squad, "scpCreatureSquad", LookMode.Reference);
            Scribe_Collections.Look(ref specimens, "scpCreatureSpecimens", LookMode.Reference);
            Scribe_Values.Look(ref securedSignal, "scpCreatureSecuredSignal");
            Scribe_Values.Look(ref documentedSignal, "scpCreatureDocumentedSignal");
            Scribe_Values.Look(ref failedSignal, "scpCreatureFailedSignal");
            Scribe_Values.Look(ref delivered, "scpCreatureDelivered");
            Scribe_Values.Look(ref secured, "scpCreatureSecured");
            Scribe_Values.Look(ref finished, "scpCreatureFinished");
            Scribe_Values.Look(ref lastGuardOrderTick, "scpCreatureLastGuardOrder", -1000);
            Scribe_Values.Look(ref guardedCell, "scpCreatureGuardedCell", IntVec3.Invalid);
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag == inSignalEnable && squad != null)
                foreach (Pawn member in squad)
                    if (member != null && !member.Spawned && !Find.WorldPawns.Contains(member))
                        Find.WorldPawns.PassToWorld(member, PawnDiscardDecideMode.KeepForever);
            base.Notify_QuestSignalReceived(signal);
        }

        public override void QuestPartTick()
        {
            int now = Find.TickManager.TicksGame;
            if (finished || map == null || specimens == null || now % 150 != 0) return;
            if (specimens.Any(thing => thing == null || thing.Destroyed))
            {
                finished = true;
                ReleaseSquad(now);
                Find.SignalManager.SendSignal(new Signal(failedSignal));
                return;
            }
            if (!delivered && specimens.All(thing => thing.Spawned && thing.Map == map))
            {
                delivered = true;
                Find.LetterStack.ReceiveLetter("SCP_CreatureTransferArrivedTitle".Translate(species),
                    "SCP_CreatureTransferArrivedBody".Translate(species, specimens.Count),
                    LetterDefOf.NeutralEvent, specimens[0]);
            }
            if (!delivered)
            {
                if (now - EnableTick > 180000)
                {
                    finished = true;
                    ReleaseSquad(now);
                    Find.SignalManager.SendSignal(new Signal(failedSignal));
                }
                return;
            }
            if (!secured)
            {
                Thing unsecured = specimens.FirstOrDefault(thing => !CompScpPost.IsObjectAtPost(thing));
                GuardAt(ColonistMoveDestination(unsecured) ?? (unsecured ?? specimens[0]).PositionHeld, now);
                if (!specimens.All(thing => thing.Spawned && thing.Map == map
                    && CompScpPost.IsObjectAtPost(thing) && thing.TryGetComp<CompScp>().IsInCustody)) return;
                secured = true;
                FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
                support.progress.trust = System.Math.Min(FoundationSupportProgress.MaximumTrust, support.progress.trust + 5);
                Find.LetterStack.ReceiveLetter("SCP_CreatureTransferSecuredTitle".Translate(species),
                    "SCP_CreatureTransferSecuredBody".Translate(species), LetterDefOf.PositiveEvent, specimens[0]);
                HoldSquadForShuttle();
                SetPickupLandingNearSquad();
                Find.SignalManager.SendSignal(new Signal(securedSignal));
            }
            TryBoardShuttle();
            var anomaly = DefDatabase<ScpFoundation.Definitions.ScpDef>.GetNamedSilentFail("SCP_Anomaly" + species);
            if (anomaly == null || Current.Game.GetComponent<ScpKnowledgeComponent>().LevelFor(anomaly)
                < KnowledgeLevel.Documented) return;
            // Keep the quest alive until the pickup shuttle has taken the escort.
            if (squad != null && squad.Any(member => member != null && member.Spawned
                && !member.Dead && member.Map == map)) return;
            finished = true;
            FoundationSupportComponent foundation = Current.Game.GetComponent<FoundationSupportComponent>();
            foundation.progress.trust = System.Math.Min(FoundationSupportProgress.MaximumTrust, foundation.progress.trust + 15);
            Find.SignalManager.SendSignal(new Signal(documentedSignal));
        }

        private IntVec3? ColonistMoveDestination(Thing unsecured)
        {
            if (unsecured == null) return null;
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                Job job = colonist.CurJob;
                if (job != null && job.def == ScpInteractionDefOf.SCP_Capture
                    && job.targetA.Thing == unsecured && job.targetB.Cell.IsValid)
                    return job.targetB.Cell;
            }
            return null;
        }

        private void GuardAt(IntVec3 cell, int now)
        {
            if (squad == null || !cell.IsValid) return;
            bool moved = guardedCell != cell;
            bool patrol = moved || now - lastGuardOrderTick >= 1200;
            guardedCell = cell;
            if (patrol) lastGuardOrderTick = now;
            JobDef wait = DefDatabase<JobDef>.GetNamed("SCP_294GuardWait");
            JobDef move = DefDatabase<JobDef>.GetNamed("SCP_294GuardGoto");
            foreach (Pawn member in squad.Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && pawn.Map == map))
            {
                Lord lord = LordUtility.GetLord(member);
                if (lord != null) lord.RemovePawn(member);
                if (member.mindState != null) member.mindState.exitMapAfterTick = int.MaxValue;
                if (member.jobs == null || member.CurJob != null && (member.CurJob.def == JobDefOf.AttackStatic
                    || member.CurJob.def == JobDefOf.AttackMelee || member.CurJob.def == JobDefOf.Wait_Combat)) continue;
                Job current = member.CurJob;
                bool near = member.Position.DistanceToSquared(cell) <= 25;
                if (!patrol && current != null && ((current.def == wait && near)
                    || (current.def == move && current.targetA.Cell.IsValid
                        && current.targetA.Cell.DistanceToSquared(cell) <= 25))) continue;
                IntVec3 destination;
                if (!CellFinder.TryRandomClosewalkCellNear(cell, map, 4, out destination,
                    candidate => candidate.Standable(map) && candidate.GetFirstPawn(map) == null)) destination = member.Position;
                Job order = destination == member.Position
                    ? JobMaker.MakeJob(wait) : JobMaker.MakeJob(move, destination);
                order.expiryInterval = 1500;
                member.jobs.StartJob(order, JobCondition.InterruptForced);
            }
        }

        private void HoldSquadForShuttle()
        {
            if (squad == null) return;
            JobDef waitDef = DefDatabase<JobDef>.GetNamed("SCP_294GuardWait");
            foreach (Pawn member in squad.Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && pawn.Map == map))
            {
                Lord lord = LordUtility.GetLord(member);
                if (lord != null) lord.RemovePawn(member);
                if (member.mindState != null) member.mindState.exitMapAfterTick = int.MaxValue;
                if (member.jobs == null) continue;
                Job current = member.CurJob;
                if (current != null && (current.def == waitDef || current.def == JobDefOf.AttackStatic
                    || current.def == JobDefOf.AttackMelee || current.def == JobDefOf.Wait_Combat)) continue;
                Job wait = JobMaker.MakeJob(waitDef);
                wait.expiryInterval = 6000;
                member.jobs.StartJob(wait, JobCondition.InterruptForced);
            }
        }

        private void SetPickupLandingNearSquad()
        {
            if (quest == null || squad == null) return;
            List<Pawn> present = squad.Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead
                && pawn.Map == map).ToList();
            if (present.Count == 0) return;
            IntVec3 landing;
            if (!DropCellFinder.TryFindDropSpotNear(present[0].Position, map, out landing,
                false, false, false, ThingDefOf.Shuttle.size, true)
                || present.Any(member => !member.CanReach(landing, PathEndMode.Touch, Danger.Deadly))) return;
            foreach (QuestPart_AddShipJob_Arrive arrival in quest.PartsListForReading.OfType<QuestPart_AddShipJob_Arrive>())
                if (arrival.inSignal == securedSignal) arrival.cell = landing;
        }

        private void TryBoardShuttle()
        {
            List<Pawn> present = squad == null ? new List<Pawn>() : squad.Where(pawn => pawn != null
                && pawn.Spawned && !pawn.Dead && pawn.Map == map).ToList();
            if (present.Count == 0) return;
            Thing shuttle = map.listerThings.ThingsOfDef(ThingDefOf.Shuttle).FirstOrDefault(thing =>
            {
                CompShuttle comp = thing.TryGetComp<CompShuttle>();
                return comp != null && comp.RequiredPawns != null && comp.RequiredPawns.Any(squad.Contains);
            });
            CompShuttle shuttleComp = shuttle == null ? null : shuttle.TryGetComp<CompShuttle>();
            if (shuttleComp == null || present.Any(pawn => !shuttleComp.IsAllowed(pawn)
                || !pawn.CanReach(shuttle, PathEndMode.Touch, Danger.Deadly)))
            {
                HoldSquadForShuttle();
                return;
            }
            foreach (Pawn member in present)
            {
                if (member.mindState != null) member.mindState.exitMapAfterTick = int.MaxValue;
                if (member.jobs == null || member.CurJob != null
                    && member.CurJob.def == JobDefOf.EnterTransporter
                    && member.CurJob.targetA.Thing == shuttle) continue;
                Lord lord = LordUtility.GetLord(member);
                if (lord != null) lord.RemovePawn(member);
                member.jobs.StartJob(JobMaker.MakeJob(JobDefOf.EnterTransporter, shuttle), JobCondition.InterruptForced);
            }
        }

        private void ReleaseSquad(int now)
        {
            if (squad == null) return;
            foreach (Pawn member in squad.Where(pawn => pawn != null && pawn.Spawned))
                if (member.mindState != null) member.mindState.exitMapAfterTick = now + 1;
        }
    }
}
