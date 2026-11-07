using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public static class Scp914Installation
    {
        public static IntVec3 SideCell(IntVec3 origin, bool left)
        {
            CellRect body = GenAdj.OccupiedRect(origin, Rot4.North,
                DefDatabase<ThingDef>.GetNamed("SCP_914Machine").size);
            return new IntVec3(left ? body.minX - 2 : body.maxX + 2, 0,
                (body.minZ + body.maxZ) / 2);
        }

        public static bool RoomReady(Map map, IntVec3 origin, Thing cargo)
        {
            ThingDef machine = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_914Machine");
            if (map == null || machine == null || !origin.InBounds(map)) return false;
            Room room = origin.GetRoom(map);
            if (room == null || room.TouchesMapEdge || room.IsDoorway
                || room.CellCount < 35 || room.OpenRoofCount > 0) return false;
            CellRect body = GenAdj.OccupiedRect(origin, Rot4.North, machine.size);
            for (int x = body.minX - 2; x <= body.maxX + 2; x++)
                for (int z = body.minZ - 1; z <= body.maxZ + 1; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map) || cell.GetRoom(map) != room || !cell.Roofed(map)
                        || !cell.Standable(map) || cell.Fogged(map)) return false;
                    bool onPad = (x == body.minX - 1 || x == body.maxX + 1)
                        && z == (body.minZ + body.maxZ) / 2;
                    if (cell.GetThingList(map).Any(thing => thing != cargo
                        && (thing.def.category == ThingCategory.Building
                            || (onPad && thing.def.category == ThingCategory.Item)))) return false;
                }
            return true;
        }

        public static bool MachineSpaceClear(Map map, IntVec3 origin, Thing cargo)
        {
            return RoomReady(map, origin, cargo)
                && !GenAdj.OccupiedRect(origin, Rot4.North,
                    DefDatabase<ThingDef>.GetNamed("SCP_914Machine").size)
                    .Any(cell => cell.GetFirstPawn(map) != null);
        }
    }

    public sealed class Thing_Scp914Transit : ThingWithComps
    {
        private bool CanCarry(Pawn pawn)
        {
            return Spawned && Map.IsPlayerHome && pawn != null && pawn.Map == Map
                && pawn.IsColonistPlayerControlled && !pawn.Drafted && !pawn.Downed
                && !pawn.InMentalState && pawn.carryTracker.CarriedThing == null
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                && pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some);
        }

        private Pawn FindAssistant(Pawn carrier, IntVec3 cell, bool carrierLeft)
        {
            IntVec3 assistantCell = Scp914Installation.SideCell(cell, !carrierLeft);
            return Map.mapPawns.FreeColonistsSpawned
                .Where(candidate => candidate != carrier && CanCarry(candidate)
                    && candidate.CanReserveAndReach(assistantCell,
                        PathEndMode.OnCell, Danger.Some))
                .OrderBy(candidate => candidate.Position.DistanceToSquared(Position))
                .FirstOrDefault();
        }

        private void ChooseCell(Pawn pawn)
        {
            if (!CanCarry(pawn)) return;
            Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target =>
            {
                if (!CanCarry(pawn)) return;
                bool needsAssistant = false;
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(target.Cell, 3.5f, true)
                    .OrderBy(candidate => candidate.DistanceToSquared(target.Cell)))
                {
                    if (!Scp914Installation.RoomReady(Map, cell, this)) continue;
                    bool carrierLeft = pawn.Position.x <= cell.x;
                    for (int side = 0; side < 2; side++)
                    {
                        IntVec3 carrierCell = Scp914Installation.SideCell(cell, carrierLeft);
                        if (pawn.CanReserveAndReach(carrierCell,
                            PathEndMode.OnCell, Danger.Some))
                        {
                            Pawn assistant = FindAssistant(pawn, cell, carrierLeft);
                            if (assistant != null)
                            {
                                JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("SCP_914Install");
                                JobDef assistDef = DefDatabase<JobDef>.GetNamedSilentFail("SCP_914InstallAssist");
                                if (jobDef == null || assistDef == null) return;
                                Job assist = JobMaker.MakeJob(assistDef, pawn, cell,
                                    Scp914Installation.SideCell(cell, !carrierLeft));
                                if (!assistant.jobs.TryTakeOrderedJob(assist, JobTag.Misc)) return;
                                Job job = JobMaker.MakeJob(jobDef, this, cell, carrierCell);
                                job.count = 1;
                                job.targetQueueB = new List<LocalTargetInfo> { assistant };
                                if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                                    assistant.jobs.EndCurrentJob(JobCondition.InterruptForced);
                                return;
                            }
                            needsAssistant = true;
                        }
                        carrierLeft = !carrierLeft;
                    }
                }
                Messages.Message((needsAssistant ? "SCP_914InstallNoAssistant"
                    : "SCP_914InstallRoomInvalid").Translate(), this,
                    MessageTypeDefOf.RejectInput, false);
            });
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (CanCarry(pawn)) yield return new FloatMenuOption("SCP_914Install".Translate(),
                () => ChooseCell(pawn));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || !Map.IsPlayerHome) yield break;
            yield return new Command_Action {
                defaultLabel = "SCP_914Install".Translate(),
                defaultDesc = "SCP_914InstallDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Item/ScpArtifacts/Scp914"),
                action = () => {
                    var options = new List<FloatMenuOption>();
                    foreach (Pawn candidate in Map.mapPawns.FreeColonistsSpawned)
                    {
                        Pawn pawn = candidate;
                        if (CanCarry(pawn)) options.Add(new FloatMenuOption(pawn.LabelShortCap,
                            () => ChooseCell(pawn)));
                    }
                    if (options.Count == 0) options.Add(new FloatMenuOption(
                        "SCP_914InstallNoCarrier".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }

    public sealed class JobDriver_Scp914Install : JobDriver
    {
        private Pawn Assistant
        {
            get { return job.targetQueueB != null && job.targetQueueB.Count == 1
                ? job.targetQueueB[0].Pawn : null; }
        }

        private bool AssistantReady
        {
            get
            {
                Pawn assistant = Assistant;
                return assistant != null && assistant.Spawned && assistant.Map == pawn.Map
                    && !assistant.Downed && assistant.CurJob != null
                    && assistant.CurJob.def.defName == "SCP_914InstallAssist"
                    && assistant.CurJob.targetA.Pawn == pawn
                    && assistant.CurJob.targetC.Cell == assistant.Position;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetC, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition => {
                if (condition != JobCondition.Succeeded) DropCargo();
            });
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            int readyTicks = 0;
            int elapsedTicks = 0;
            Toil assemble = ToilMaker.MakeToil();
            assemble.defaultCompleteMode = ToilCompleteMode.Never;
            assemble.tickAction = () => {
                elapsedTicks++;
                if (elapsedTicks > 6000 || Assistant == null || Assistant.Downed
                    || pawn.carryTracker.CarriedThing == null)
                {
                    DropCargo();
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (AssistantReady && Scp914Installation.MachineSpaceClear(pawn.Map,
                    job.targetB.Cell, pawn.carryTracker.CarriedThing))
                    readyTicks++;
                else readyTicks = 0;
                if (readyTicks >= 1000) ReadyForNextToil();
            };
            yield return assemble;
            yield return Toils_General.Do(() => {
                Thing cargo = pawn.carryTracker.CarriedThing;
                IntVec3 origin = job.targetB.Cell;
                if (cargo == null || cargo.def.defName != "SCP_914Transit"
                    || !AssistantReady || !Scp914Installation.MachineSpaceClear(pawn.Map, origin, cargo))
                {
                    DropCargo();
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                Thing machine = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_914Machine"));
                machine.SetFaction(Faction.OfPlayer);
                try
                {
                    GenSpawn.Spawn(machine, origin, pawn.Map);
                    cargo.Destroy(DestroyMode.Vanish);
                }
                catch (System.Exception error)
                {
                    if (machine.Spawned) machine.Destroy(DestroyMode.Vanish);
                    DropCargo();
                    Log.Error("SCP-914 installation failed: " + error);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
        }

        private void DropCargo()
        {
            if (pawn.carryTracker.CarriedThing == null) return;
            Thing dropped;
            pawn.carryTracker.TryDropCarriedThing(pawn.Position,
                ThingPlaceMode.Near, out dropped);
            Messages.Message("SCP_914InstallInterrupted".Translate(), pawn,
                MessageTypeDefOf.RejectInput, false);
        }
    }

    public sealed class JobDriver_Scp914InstallAssist : JobDriver
    {
        private int nextPathTick;
        private int startedTick;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetC, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil follow = ToilMaker.MakeToil();
            follow.defaultCompleteMode = ToilCompleteMode.Never;
            follow.initAction = () => startedTick = Find.TickManager.TicksGame;
            follow.tickAction = () => {
                Pawn carrier = job.targetA.Pawn;
                if (carrier == null || !carrier.Spawned || carrier.Map != pawn.Map)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (carrier.CurJob == null || carrier.CurJob.def.defName != "SCP_914Install")
                {
                    if (Find.TickManager.TicksGame - startedTick > 600)
                        EndJobWith(JobCondition.Succeeded);
                    return;
                }
                IntVec3 destination = job.targetC.Cell;
                if (carrier.Position == carrier.CurJob.targetC.Cell)
                {
                    if (pawn.Position != destination && !pawn.pather.Moving
                        && Find.TickManager.TicksGame >= nextPathTick)
                    {
                        nextPathTick = Find.TickManager.TicksGame + 60;
                        pawn.pather.StartPath(destination, PathEndMode.OnCell);
                    }
                    return;
                }
                if (pawn.Position.DistanceToSquared(carrier.Position) > 4
                    && !pawn.pather.Moving && Find.TickManager.TicksGame >= nextPathTick)
                {
                    nextPathTick = Find.TickManager.TicksGame + 45;
                    pawn.pather.StartPath(carrier, PathEndMode.Touch);
                }
            };
            yield return follow;
        }
    }
}
