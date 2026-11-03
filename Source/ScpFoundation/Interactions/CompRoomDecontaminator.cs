using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class ScpContaminationExtension : DefModExtension { }

    public sealed class CompProperties_RoomDecontaminator : CompProperties
    {
        public CompProperties_RoomDecontaminator() { compClass = typeof(CompRoomDecontaminator); }
    }

    public sealed class CompRoomDecontaminator : ThingComp
    {
        private enum EmergencyCause
        {
            None,
            DoorDestroyed,
            StructureBreached
        }

        private int ticksRemaining;
        private List<Building_Door> lockedDoors = new List<Building_Door>();
        private List<Building_Door> previouslyForbiddenDoors = new List<Building_Door>();
        private List<IntVec3> sealedCells = new List<IntVec3>();
        private int expectedDoorCount;
        private EmergencyCause emergencyCause;

        public bool Running { get { return ticksRemaining > 0; } }

        private bool Powered
        {
            get
            {
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                CompFlickable flick = parent.TryGetComp<CompFlickable>();
                return power != null && power.PowerOn && (flick == null || flick.SwitchIsOn);
            }
        }

        private Room Chamber { get { return parent.Spawned ? parent.Position.GetRoom(parent.Map) : null; } }

        private static IEnumerable<Building_Door> BorderDoors(Room room, Map map)
        {
            if (room == null) yield break;
            HashSet<Building_Door> seen = new HashSet<Building_Door>();
            foreach (IntVec3 cell in room.Cells)
                foreach (IntVec3 adjacent in GenAdj.CardinalDirections.Select(direction => cell + direction))
                {
                    Building_Door door = adjacent.GetDoor(map);
                    if (door != null && seen.Add(door)) yield return door;
                }
        }

        private static bool HasOpenDoor(Room room, Map map) { return BorderDoors(room, map).Any(door => door.Open); }

        public static bool DoorLocked(Building_Door door)
        {
            if (door == null || !door.Spawned || ScpInteractionDefOf.SCPV1_DeconGate == null) return false;
            return door.Map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCPV1_DeconGate)
                .Select(thing => thing.TryGetComp<CompRoomDecontaminator>())
                .Any(comp => comp != null && comp.Running && comp.lockedDoors.Contains(door));
        }

        private bool RoomReady(Room room)
        {
            return DecontaminationPolicy.RoomReady(room != null, room != null && room.TouchesMapEdge,
                room == null ? 0 : room.CellCount, room == null ? 0 : room.OpenRoofCount,
                room != null && HasOpenDoor(room, parent.Map));
        }

        private string DisabledReason(Room room)
        {
            if (!Powered) return "SCP_DeconNeedsPower".Translate();
            if (room == null || room.TouchesMapEdge) return "SCP_DeconNeedsRoom".Translate();
            if (room.CellCount > DecontaminationPolicy.MaximumRoomCells)
                return "SCP_DeconRoomTooLarge".Translate(DecontaminationPolicy.MaximumRoomCells);
            if (room.OpenRoofCount > 0) return "SCP_DeconNeedsRoof".Translate();
            if (HasOpenDoor(room, parent.Map)) return "SCP_DeconCloseDoors".Translate();
            return null;
        }

        private void StartCycle()
        {
            Room room = Chamber;
            LockDoors(room);
            sealedCells = room.Cells.ToList();
            expectedDoorCount = lockedDoors.Count;
            emergencyCause = EmergencyCause.None;
            ticksRemaining = DecontaminationPolicy.CycleTicks;
        }

        private EmergencyCause StructuralFailure(Room room)
        {
            int survivingDoors = lockedDoors.Count(door => door != null && !door.Destroyed && door.Spawned);
            if (survivingDoors != expectedDoorCount) return EmergencyCause.DoorDestroyed;
            if (room == null || room.TouchesMapEdge) return EmergencyCause.StructureBreached;
            HashSet<IntVec3> currentCells = new HashSet<IntVec3>(room.Cells);
            bool sameCells = sealedCells.All(currentCells.Contains);
            return DecontaminationPolicy.StructureIntact(expectedDoorCount, survivingDoors,
                sealedCells.Count, currentCells.Count, sameCells) ? EmergencyCause.None : EmergencyCause.StructureBreached;
        }

        private void EnterEmergency(EmergencyCause cause)
        {
            emergencyCause = cause;
            ticksRemaining = 0;
            ReleaseDoors();
            string reason = EmergencyReason(cause);
            Find.LetterStack.ReceiveLetter("SCP_DeconEmergencyTitle".Translate(),
                "SCP_DeconEmergencyBody".Translate(parent.LabelCap, reason), LetterDefOf.ThreatSmall, parent);
        }

        private static string EmergencyReason(EmergencyCause cause)
        {
            return (cause == EmergencyCause.DoorDestroyed
                ? "SCP_DeconEmergencyDoor" : "SCP_DeconEmergencyWall").Translate();
        }

        private void ClearCycleSnapshot()
        {
            sealedCells.Clear();
            expectedDoorCount = 0;
        }

        private string RepairReason()
        {
            Room room = Chamber;
            string normalReason = DisabledReason(room);
            if (normalReason != null) return normalReason;
            List<Building_Door> currentDoors = BorderDoors(room, parent.Map).ToList();
            HashSet<IntVec3> currentCells = new HashSet<IntVec3>(room.Cells);
            bool sameCells = sealedCells.All(currentCells.Contains);
            if (!DecontaminationPolicy.StructureIntact(expectedDoorCount, currentDoors.Count,
                sealedCells.Count, currentCells.Count, sameCells))
                return "SCP_DeconRepairStructure".Translate(expectedDoorCount, sealedCells.Count);
            return null;
        }

        private void ResetEmergency()
        {
            emergencyCause = EmergencyCause.None;
            ClearCycleSnapshot();
            Messages.Message("SCP_DeconEmergencyResetDone".Translate(), parent, MessageTypeDefOf.PositiveEvent, false);
        }

        private void LockDoors(Room room)
        {
            ReleaseDoors();
            lockedDoors = BorderDoors(room, parent.Map).ToList();
            previouslyForbiddenDoors = lockedDoors.Where(door => door.compForbiddable != null && door.compForbiddable.Forbidden).ToList();
            foreach (Building_Door door in lockedDoors)
                if (door.compForbiddable != null) door.compForbiddable.Forbidden = true;
        }

        private void MaintainDoorLocks()
        {
            foreach (Building_Door door in lockedDoors.Where(door => door != null && !door.Destroyed))
                if (door.compForbiddable != null) door.compForbiddable.Forbidden = true;
        }

        private void ReleaseDoors()
        {
            foreach (Building_Door door in lockedDoors.Where(door => door != null && !door.Destroyed))
                if (door.compForbiddable != null)
                    door.compForbiddable.Forbidden = previouslyForbiddenDoors.Contains(door);
            lockedDoors.Clear();
            previouslyForbiddenDoors.Clear();
        }

        public override void CompTick()
        {
            if (ticksRemaining <= 0 || !parent.Spawned) return;
            MaintainDoorLocks();
            Room room = Chamber;
            EmergencyCause structuralFailure = StructuralFailure(room);
            if (structuralFailure != EmergencyCause.None)
            {
                EnterEmergency(structuralFailure);
                return;
            }
            int next = DecontaminationPolicy.Advance(ticksRemaining, Powered, RoomReady(room));
            if (next == 0 && ticksRemaining > 1)
            {
                ticksRemaining = 0;
                ReleaseDoors();
                ClearCycleSnapshot();
                Messages.Message("SCP_DeconInterrupted".Translate(), parent, MessageTypeDefOf.NegativeEvent, false);
                return;
            }

            ticksRemaining = next;
            if (ticksRemaining > 0)
            {
                if (ticksRemaining % 12 == 0) EmitVapor(room);
                return;
            }

            Clean(room);
            ReleaseDoors();
            ClearCycleSnapshot();
            Messages.Message("SCP_DeconComplete".Translate(), parent, MessageTypeDefOf.PositiveEvent, false);
        }

        private void EmitVapor(Room room)
        {
            if (room == null) return;
            List<IntVec3> cells = room.Cells.Where(candidate => candidate.Standable(parent.Map)).ToList();
            if (cells.Count == 0) return;
            IntVec3 cell = cells.RandomElement();
            FleckMaker.ThrowAirPuffUp(cell.ToVector3Shifted(), parent.Map);
            if (ticksRemaining % 36 == 0)
                FleckMaker.ThrowAirPuffUp(parent.DrawPos, parent.Map);
        }

        private void Clean(Room room)
        {
            if (room == null) return;
            List<IntVec3> cells = room.Cells.ToList();
            foreach (IntVec3 cell in cells)
            {
                parent.Map.gasGrid.ClearCellUnsafe(cell);
                foreach (Thing thing in cell.GetThingList(parent.Map).ToList())
                    if (thing is Filth) thing.Destroy();
            }
            parent.Map.gasGrid.RecalculateEverHadGas();

            foreach (Pawn pawn in cells.SelectMany(cell => cell.GetThingList(parent.Map)).OfType<Pawn>().Distinct())
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs
                    .Where(item => item.def.GetModExtension<ScpContaminationExtension>() != null).ToList())
                    pawn.health.RemoveHediff(hediff);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksRemaining, "scpDecontaminationTicks", 0);
            Scribe_Values.Look(ref expectedDoorCount, "scpDecontaminationExpectedDoors", 0);
            Scribe_Values.Look(ref emergencyCause, "scpDecontaminationEmergency", EmergencyCause.None);
            Scribe_Collections.Look(ref lockedDoors, "scpDecontaminationLockedDoors", LookMode.Reference);
            Scribe_Collections.Look(ref previouslyForbiddenDoors, "scpDecontaminationPreviouslyForbiddenDoors", LookMode.Reference);
            Scribe_Collections.Look(ref sealedCells, "scpDecontaminationSealedCells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ticksRemaining = System.Math.Max(0, System.Math.Min(DecontaminationPolicy.CycleTicks, ticksRemaining));
                lockedDoors = lockedDoors ?? new List<Building_Door>();
                previouslyForbiddenDoors = previouslyForbiddenDoors ?? new List<Building_Door>();
                sealedCells = sealedCells ?? new List<IntVec3>();
                lockedDoors.RemoveAll(door => door == null || door.Destroyed);
                previouslyForbiddenDoors.RemoveAll(door => door == null || door.Destroyed || !lockedDoors.Contains(door));
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            ReleaseDoors();
            ticksRemaining = 0;
            emergencyCause = EmergencyCause.None;
            ClearCycleSnapshot();
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            if (emergencyCause != EmergencyCause.None)
                return "SCP_DeconEmergencyInspect".Translate(EmergencyReason(emergencyCause));
            if (ticksRemaining > 0)
                return "SCP_DeconRunningLocked".Translate((1f - (float)ticksRemaining / DecontaminationPolicy.CycleTicks).ToString("P0"), lockedDoors.Count);
            string reason = DisabledReason(Chamber);
            return reason == null ? "SCP_DeconReady".Translate().ToString() : reason;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) yield break;
            if (emergencyCause != EmergencyCause.None)
            {
                Command_Action reset = new Command_Action
                {
                    defaultLabel = "SCP_DeconEmergencyReset".Translate(),
                    defaultDesc = "SCP_DeconEmergencyResetDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("Things/Building/ScpDecontaminationGate"),
                    action = ResetEmergency
                };
                string repairReason = RepairReason();
                if (repairReason != null) reset.Disable(repairReason);
                yield return reset;
                yield break;
            }
            Command_Action command = new Command_Action
            {
                defaultLabel = "SCP_DeconStart".Translate(),
                defaultDesc = "SCP_DeconStartDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Building/ScpDecontaminationGate"),
                action = StartCycle
            };
            if (ticksRemaining > 0) command.Disable("SCP_DeconAlreadyRunning".Translate());
            else
            {
                string reason = DisabledReason(Chamber);
                if (reason != null) command.Disable(reason);
            }
            yield return command;
        }
    }
}
