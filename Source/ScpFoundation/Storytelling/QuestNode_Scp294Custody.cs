using System;
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
    public sealed class QuestNode_Scp294Custody : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            if (map == null || !map.IsPlayerHome || map.mapPawns.FreeColonistsSpawnedCount == 0
                || Current.Game == null || QuestPart_Scp294Custody.ActiveFor(map)) return false;
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            Faction faction = support.FoundationFaction;
            var anomaly = DefDatabase<ScpFoundation.Definitions.ScpDef>.GetNamedSilentFail("SCP_Anomaly294");
            ThingDef machine = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_294Machine");
            return support.progress.unlocked && faction != null && !faction.HostileTo(Faction.OfPlayer)
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && anomaly != null && machine != null
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("294")
                && !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(anomaly)
                && !Find.Maps.Any(other => other.listerThings.ThingsOfDef(machine).Count > 0)
                && CompScpPost.All(map).Any(post => QuestPart_Scp294Custody.FreePost(post));
        }

        protected override void RunInt()
        {
            Map map = QuestGen.slate.Get<Map>("map");
            Faction faction = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("SCP_FoundationOperative");
            ThingDef rifle = DefDatabase<ThingDef>.GetNamed("SCP_Nu7HKCarbine");
            Thing crate = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_294Transit"));
            var pawns = new List<Pawn>();
            for (int i = 0; i < 4; i++)
                pawns.Add(FoundationSupportComponent.GenerateEquippedPawn(kind, faction, rifle,
                    QuestGen_Pawns.GeneratePawn(QuestGen.quest, kind, faction)));
            var contents = new List<Thing>(pawns.Cast<Thing>()) { crate };
            QuestGen.slate.Set("scp294Faction", faction);
            QuestGen.slate.Set("scp294Pawns", pawns);
            QuestGen.slate.Set("scp294Contents", contents);
            QuestGen.quest.AddPart(new QuestPart_Scp294Custody {
                map = map,
                courier = pawns[0],
                squad = pawns,
                crate = crate,
                inSignalEnable = QuestGen.quest.InitiateSignal,
                deliveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp294Delivered"),
                documentedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp294Documented"),
                failedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp294Failed")
            });
        }
    }

    public sealed class QuestPart_Scp294Custody : QuestPartActivable
    {
        public Map map;
        public Pawn courier;
        public List<Pawn> squad;
        public Thing crate;
        public string deliveredSignal;
        public string documentedSignal;
        public string failedSignal;
        private Thing machine;
        private bool delivered;
        private bool secured;
        private bool pickupCalled;
        private bool completed;
        private int lastOrderTick = -1000;
        private int outsidePostSinceTick = -1;
        private IntVec3 guardedCell = IntVec3.Invalid;

        public static bool ActiveFor(Map target)
        {
            return target != null && Find.QuestManager != null
                && Find.QuestManager.QuestsListForReading.Any(quest =>
                    (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing)
                    &&
                    quest.PartsListForReading.OfType<QuestPart_Scp294Custody>()
                        .Any(part => part.map == target));
        }

        public static bool FreePost(CompScpPost post)
        {
            if (post == null || post.parent.def != ScpInteractionDefOf.SCP_ContainmentAnchor
                || post.BoundObject != null || !CompScpInteraction.AnchorAvailable(post.parent)) return false;
            return !post.parent.Position.GetThingList(post.parent.Map).Any(thing =>
                thing != post.parent && thing.def.category == ThingCategory.Building);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scp294QuestMap");
            Scribe_References.Look(ref courier, "scp294Courier");
            Scribe_Collections.Look(ref squad, "scp294Squad", LookMode.Reference);
            Scribe_References.Look(ref crate, "scp294Crate");
            Scribe_References.Look(ref machine, "scp294QuestMachine");
            Scribe_Values.Look(ref deliveredSignal, "scp294DeliveredSignal");
            Scribe_Values.Look(ref documentedSignal, "scp294DocumentedSignal");
            Scribe_Values.Look(ref failedSignal, "scp294FailedSignal");
            Scribe_Values.Look(ref delivered, "scp294Delivered", false);
            // In older saves, "delivered" meant that the machine was already at a post.
            Scribe_Values.Look(ref secured, "scp294Secured", delivered);
            // Older quests already called the return shuttle at delivery.
            Scribe_Values.Look(ref pickupCalled, "scp294PickupCalled", delivered);
            Scribe_Values.Look(ref completed, "scp294Completed", false);
            Scribe_Values.Look(ref lastOrderTick, "scp294LastOrderTick", -1000);
            Scribe_Values.Look(ref outsidePostSinceTick, "scp294OutsidePostSinceTick", -1);
            Scribe_Values.Look(ref guardedCell, "scp294GuardedCell", IntVec3.Invalid);
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            // Quests created by earlier versions also need world pawns before the shuttle is set up.
            if (signal.tag == inSignalEnable && squad != null)
                foreach (Pawn member in squad)
                    if (member != null && !member.Spawned && !Find.WorldPawns.Contains(member))
                        Find.WorldPawns.PassToWorld(member, PawnDiscardDecideMode.KeepForever);
            base.Notify_QuestSignalReceived(signal);
        }

        public override void QuestPartTick()
        {
            int now = Find.TickManager.TicksGame;
            if (completed || map == null || now % 90 != 0) return;
            ThingDef machineDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_294Machine");
            if (!delivered)
            {
                if (machine == null && crate != null && crate.Spawned && crate.Map == map)
                    machine = crate;
                if (machine == null && machineDef != null)
                    machine = map.listerThings.ThingsOfDef(machineDef)
                        .FirstOrDefault(CompScpPost.IsObjectAtPost);
                if (machine != null && machine.Spawned)
                {
                    delivered = true;
                    outsidePostSinceTick = -1;
                    Find.LetterStack.ReceiveLetter("SCP_294CustodyDeliveredTitle".Translate(),
                        "SCP_294CustodyDeliveredBody".Translate(), LetterDefOf.NeutralEvent, machine);
                    GuardAt(machine.Position);
                    return;
                }
                if (now - EnableTick > 180000 || crate == null || crate.Destroyed)
                {
                    completed = true;
                    SendSquadHome(now);
                    Find.SignalManager.SendSignal(new Signal(failedSignal));
                    return;
                }
                return;
            }
            if (machine == crate && crate.Destroyed && machineDef != null)
                machine = map.listerThings.ThingsOfDef(machineDef)
                    .FirstOrDefault(CompScpPost.IsObjectAtPost);
            if (machine == null || machine.Destroyed)
            {
                completed = true;
                SendSquadHome(now);
                Find.SignalManager.SendSignal(new Signal(failedSignal));
                return;
            }
            if (pickupCalled) TryBoardShuttle();
            if (!CompScpPost.IsObjectAtPost(machine))
            {
                Thing destination = ColonistMoveDestination();
                if (!secured)
                    GuardAt(destination == null ? machine.PositionHeld : destination.Position);
                if (!secured) return;
                if (outsidePostSinceTick < 0) outsidePostSinceTick = now;
                if (now - outsidePostSinceTick > 180000)
                {
                    completed = true;
                    SendSquadHome(now);
                    Find.SignalManager.SendSignal(new Signal(failedSignal));
                }
                return;
            }
            outsidePostSinceTick = -1;
            if (!secured)
            {
                secured = true;
                GrantTrust(5);
                Find.LetterStack.ReceiveLetter("SCP_294CustodySecuredTitle".Translate(),
                    "SCP_294CustodySecuredBody".Translate(), LetterDefOf.PositiveEvent, machine);
            }
            if (!pickupCalled)
            {
                pickupCalled = true;
                HoldSquadForShuttle();
                SetPickupLandingNearSquad();
                Find.SignalManager.SendSignal(new Signal(deliveredSignal));
                TryBoardShuttle();
            }
            var anomaly = DefDatabase<ScpFoundation.Definitions.ScpDef>.GetNamedSilentFail("SCP_Anomaly294");
            if (anomaly == null || Current.Game.GetComponent<ScpKnowledgeComponent>().LevelFor(anomaly)
                < KnowledgeLevel.Documented) return;
            // Ending the quest earlier makes cleanup send the pickup shuttle away mid-boarding.
            if (squad != null && squad.Any(member => member != null && member.Spawned
                && !member.Dead && member.Map == map)) return;
            completed = true;
            GrantTrust(15);
            Find.SignalManager.SendSignal(new Signal(documentedSignal));
        }

        private static void GrantTrust(int amount)
        {
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            support.progress.trust = Math.Min(FoundationSupportProgress.MaximumTrust, support.progress.trust + amount);
        }

        private Thing ColonistMoveDestination()
        {
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                Job job = colonist.CurJob;
                if (job != null && (job.def.defName == "SCP_294MoveToPost"
                    || job.def.defName == "SCP_294CourierDeliver") && job.targetA.Thing == machine)
                    return job.targetB.Thing;
            }
            return null;
        }

        private void GuardAt(IntVec3 cell)
        {
            if (squad == null || !cell.IsValid) return;
            JobDef guardWait = DefDatabase<JobDef>.GetNamed("SCP_294GuardWait");
            JobDef guardGoto = DefDatabase<JobDef>.GetNamed("SCP_294GuardGoto");
            List<Pawn> present = squad.Where(member => member != null && member.Spawned
                && !member.Dead && member.Map == map).ToList();
            if (present.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            bool patrol = guardedCell != cell || now - lastOrderTick >= 1440;
            foreach (Pawn member in present)
            {
                Lord previous = LordUtility.GetLord(member);
                if (previous != null) previous.RemovePawn(member);
                if (member.mindState != null) member.mindState.exitMapAfterTick = int.MaxValue;
                if (member.jobs == null) continue;
                Job current = member.CurJob;
                if (current != null && (current.def == JobDefOf.AttackStatic
                    || current.def == JobDefOf.AttackMelee || current.def == JobDefOf.Wait_Combat)) continue;
                bool near = member.Position.DistanceToSquared(cell) <= 25;
                bool staying = current != null && current.def == guardWait && near;
                bool approaching = current != null && current.def == guardGoto
                    && current.targetA.Cell.IsValid && current.targetA.Cell.DistanceToSquared(cell) <= 25;
                if (!patrol && (staying || approaching)) continue;
                IntVec3 destination;
                if (!CellFinder.TryRandomClosewalkCellNear(cell, map, 4, out destination, candidate =>
                    candidate != cell && candidate.Standable(map)
                    && !candidate.GetThingList(map).Any(thing => thing is Pawn)))
                    destination = member.Position;
                Job order = destination == member.Position
                    ? JobMaker.MakeJob(guardWait) : JobMaker.MakeJob(guardGoto, destination);
                order.expiryInterval = 1800;
                member.jobs.StartJob(order, JobCondition.InterruptForced);
            }
            guardedCell = cell;
            if (patrol) lastOrderTick = now;
        }

        private void HoldSquadForShuttle()
        {
            if (squad == null) return;
            JobDef guardWait = DefDatabase<JobDef>.GetNamed("SCP_294GuardWait");
            foreach (Pawn member in squad)
            {
                if (member == null || !member.Spawned || member.Dead || member.Map != map) continue;
                Lord lord = LordUtility.GetLord(member);
                if (lord != null) lord.RemovePawn(member);
                if (member.mindState != null) member.mindState.exitMapAfterTick = int.MaxValue;
                if (member.jobs == null) continue;
                Job current = member.CurJob;
                if (current != null && (current.def == guardWait
                    || current.def == JobDefOf.AttackStatic || current.def == JobDefOf.AttackMelee
                    || current.def == JobDefOf.Wait_Combat)) continue;
                Job wait = JobMaker.MakeJob(guardWait);
                wait.expiryInterval = 6000;
                member.jobs.StartJob(wait, JobCondition.InterruptForced);
            }
        }

        private void SetPickupLandingNearSquad()
        {
            if (quest == null || squad == null) return;
            List<Pawn> present = squad.Where(member => member != null && member.Spawned
                && !member.Dead && member.Map == map).ToList();
            if (present.Count == 0) return;
            IntVec3 landingCell;
            if (!DropCellFinder.TryFindDropSpotNear(present[0].Position, map, out landingCell,
                false, false, false, ThingDefOf.Shuttle.size, true)
                || present.Any(member => !member.CanReach(landingCell, PathEndMode.Touch, Danger.Deadly))) return;
            foreach (QuestPart_AddShipJob_Arrive arrival in quest.PartsListForReading.OfType<QuestPart_AddShipJob_Arrive>())
                if (arrival.inSignal == deliveredSignal) arrival.cell = landingCell;
        }

        private void TryBoardShuttle()
        {
            if (squad == null) return;
            List<Pawn> present = squad.Where(member => member != null && member.Spawned
                && !member.Dead && member.Map == map).ToList();
            if (present.Count == 0) return;
            Thing shuttle = map.listerThings.ThingsOfDef(ThingDefOf.Shuttle).FirstOrDefault(thing =>
            {
                CompShuttle comp = thing.TryGetComp<CompShuttle>();
                return comp != null && comp.RequiredPawns != null
                    && comp.RequiredPawns.Any(squad.Contains);
            });
            CompShuttle shuttleComp = shuttle == null ? null : shuttle.TryGetComp<CompShuttle>();
            if (shuttleComp == null || present.Any(member => !shuttleComp.IsAllowed(member)
                || !member.CanReach(shuttle, PathEndMode.Touch, Danger.Deadly)))
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
                Lord previous = LordUtility.GetLord(member);
                if (previous != null) previous.RemovePawn(member);
                member.jobs.StartJob(JobMaker.MakeJob(JobDefOf.EnterTransporter, shuttle), JobCondition.InterruptForced);
            }
        }

        private void ReleaseSquad(int now)
        {
            if (squad == null) return;
            foreach (Pawn member in squad)
            {
                if (member == null || !member.Spawned) continue;
                Lord lord = LordUtility.GetLord(member);
                if (lord != null && lord.LordJob is LordJob_DefendPoint) lord.RemovePawn(member);
                if (member.mindState != null) member.mindState.exitMapAfterTick = now + 60000;
            }
        }

        private void SendSquadHome(int now)
        {
            ReleaseSquad(now);
            if (squad == null) return;
            foreach (Pawn member in squad)
                if (member != null && member.Spawned && member.mindState != null)
                    member.mindState.exitMapAfterTick = now + 1;
        }
    }
}
