using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp999 : ThingWithComps
    {
        private const int WanderInterval = 300;
        private const int GlideTicks = 120;
        private static readonly IntVec3[] Directions = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };
        private static readonly MethodInfo OpenDoor = typeof(Building_Door).GetMethod("DoorOpen",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private int nextWanderTick;
        private int nextHugTick;
        private IntVec3 previousCell = IntVec3.Invalid;
        private int moveTick;

        public override Vector3 DrawPos
        {
            get
            {
                Vector3 destination = base.DrawPos;
                if (!previousCell.IsValid || moveTick <= 0 || Find.TickManager == null)
                    return destination;
                Vector3 origin = previousCell.ToVector3Shifted();
                origin.y = destination.y;
                return Vector3.Lerp(origin, destination,
                    Mathf.Clamp01((Find.TickManager.TicksGame - moveTick) / (float)GlideTicks));
            }
        }

        public override string GetInspectString()
        {
            return "SCP_999Inspect".Translate();
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn)) yield return option;
            if (!Spawned || selPawn == null || selPawn.Faction != Faction.OfPlayer
                || selPawn.Dead || selPawn.Downed || selPawn.needs == null
                || selPawn.needs.mood == null || selPawn.jobs == null || selPawn.Map != Map
                || !selPawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some)) yield break;
            yield return new FloatMenuOption("SCP_999Hug".Translate(), () =>
            {
                JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("SCP_999Hug");
                if (def != null) selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(def, this), JobTag.Misc);
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextWanderTick, "scp999NextWander", 0);
            Scribe_Values.Look(ref nextHugTick, "scp999NextHug", 0);
            Scribe_Values.Look(ref previousCell, "scp999PreviousCell", IntVec3.Invalid);
            Scribe_Values.Look(ref moveTick, "scp999MoveTick", 0);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || Map == null) return;
            int now = Find.TickManager.TicksGame;
            if (now % 250 == 0)
            {
                ThoughtDef nearby = DefDatabase<ThoughtDef>.GetNamedSilentFail("SCP_999Nearby");
                if (nearby != null)
                    foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned)
                        if (!pawn.Dead && pawn.Position.DistanceToSquared(Position) <= 25
                            && pawn.needs != null && pawn.needs.mood != null)
                            pawn.needs.mood.thoughts.memories.TryGainMemory(nearby);
            }
            Map map = Map;
            if (nextWanderTick - now > WanderInterval) nextWanderTick = now + WanderInterval;
            if (now < nextWanderTick && (now + thingIDNumber) % 120 != 0) return;
            if (map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.jobs != null
                && pawn.jobs.curJob != null && pawn.jobs.curJob.def.defName == "SCP_999Hug"
                && pawn.jobs.curJob.targetA.Thing == this)) return;
            if ((now + thingIDNumber) % 120 == 0 && TryCheerUp(map, now)) return;
            if (now < nextWanderTick) return;
            nextWanderTick = now + WanderInterval;
            foreach (IntVec3 direction in Directions.InRandomOrder())
            {
                IntVec3 cell = Position + direction;
                if (!CanEnter(cell, map)) continue;
                MoveTo(cell, now);
                break;
            }
        }

        private bool TryCheerUp(Map map, int now)
        {
            IEnumerable<Pawn> targets = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => !pawn.Dead && !pawn.Downed && !pawn.InMentalState && pawn.Awake()
                    && pawn.needs != null && pawn.needs.mood != null)
                .OrderBy(pawn => pawn.needs.mood.CurLevelPercentage)
                .ThenBy(pawn => pawn.Position.DistanceToSquared(Position)).Take(4);
            foreach (Pawn target in targets)
            {
                if (Position.DistanceToSquared(target.Position) <= 2)
                {
                    if (now >= nextHugTick && target.needs.mood.CurLevelPercentage < 0.45f)
                    {
                        ThoughtDef hugged = DefDatabase<ThoughtDef>.GetNamedSilentFail("SCP_999Hugged");
                        if (hugged != null)
                        {
                            target.needs.mood.thoughts.memories.TryGainMemory(hugged);
                            ScpEventJournal.Add("SCP-999", this, "SCP_Journal999Hug".Translate(target.LabelShortCap));
                            nextHugTick = now + 15000;
                        }
                    }
                    nextWanderTick = now + WanderInterval;
                    return true;
                }
                IntVec3? step = FindStep(map, target.Position);
                if (!step.HasValue) continue;
                if (!CanEnter(step.Value, map)) { nextWanderTick = now + 120; return true; }
                nextWanderTick = now + WanderInterval;
                MoveTo(step.Value, now);
                return true;
            }
            return false;
        }

        private IntVec3? FindStep(Map map, IntVec3 destination)
        {
            var queue = new Queue<IntVec3>();
            var previous = new Dictionary<IntVec3, IntVec3>();
            queue.Enqueue(Position);
            previous.Add(Position, Position);
            while (queue.Count > 0 && previous.Count < 5000)
            {
                IntVec3 current = queue.Dequeue();
                if (current.DistanceToSquared(destination) <= 2)
                {
                    while (previous[current] != Position && current != Position) current = previous[current];
                    return current == Position ? (IntVec3?)null : current;
                }
                foreach (IntVec3 direction in Directions)
                {
                    IntVec3 cell = current + direction;
                    if (previous.ContainsKey(cell) || !CanPathThrough(cell, map)) continue;
                    previous.Add(cell, current);
                    queue.Enqueue(cell);
                }
            }
            return null;
        }

        private static bool CanPathThrough(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || cell.Fogged(map) || cell.GetFirstPawn(map) != null) return false;
            Building edifice = cell.GetEdifice(map);
            Building_Door door = edifice as Building_Door;
            if (door != null) return door.Faction == Faction.OfPlayer
                && !(door is Building_FoundationDoor) && !(door is Building_FoundationMultiTileDoor)
                && !(door is Building_FoundationShutter) && !CompRoomDecontaminator.DoorLocked(door);
            return edifice == null && cell.Standable(map);
        }

        private static bool CanEnter(IntVec3 cell, Map map)
        {
            if (!CanPathThrough(cell, map)) return false;
            Building_Door door = cell.GetDoor(map);
            if (door == null) return true;
            if (!door.Open && OpenDoor != null) OpenDoor.Invoke(door, new object[] { 120 });
            return door.Open;
        }

        private void MoveTo(IntVec3 cell, int now)
        {
            Map map = Map;
            previousCell = Position;
            DeSpawn();
            GenSpawn.Spawn(this, cell, map);
            moveTick = now;
        }
    }
}
