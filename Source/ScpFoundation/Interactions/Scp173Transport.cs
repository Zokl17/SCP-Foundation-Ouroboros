using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Anomalies;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public static class Scp173Transport
    {
        private const int WatchRadiusSquared = 16;

        public static List<Pawn> SelectEscorts(Thing statue, Pawn carrier)
        {
            if (statue == null || !statue.Spawned || carrier == null) return new List<Pawn>();
            return statue.Map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn != carrier && pawn.IsColonistPlayerControlled && !pawn.Dead
                    && !pawn.Downed && !pawn.InMentalState && pawn.Awake()
                    && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                    && pawn.carryTracker.CarriedThing == null
                    && (!pawn.Drafted || ScpObservation.Status(pawn, statue) == ObservationStatus.Ready)
                    && (pawn.CurJob == null || pawn.CurJob.def.defName != "SCP_173Escort")
                    && (pawn.CurJob == null || pawn.CurJob.targetA.Thing == statue
                        || (pawn.CurJob.def != ScpInteractionDefOf.SCP_Observe
                            && pawn.CurJob.def != ScpInteractionDefOf.SCP_Study
                            && pawn.CurJob.def != ScpInteractionDefOf.SCP_Restore))
                    && pawn.CanReach(statue, PathEndMode.Touch, Danger.Some))
                .OrderByDescending(pawn => ScpObservation.Status(pawn, statue) == ObservationStatus.Ready)
                .ThenBy(pawn => pawn.Position.DistanceToSquared(statue.Position))
                .Take(2).ToList();
        }

        public static bool TeamReady(Job job, Pawn carrier, Thing statue)
        {
            if (job.targetQueueB == null || job.targetQueueB.Count != 2 || carrier == null
                || !carrier.Spawned || statue == null) return false;
            Pawn first = job.targetQueueB[0].Pawn;
            Pawn second = job.targetQueueB[1].Pawn;
            return first != second && EscortReady(first, carrier, statue)
                && EscortReady(second, carrier, statue);
        }

        public static bool TeamWatching(Thing statue)
        {
            if (statue == null || !statue.Spawned) return false;
            return statue.Map.mapPawns.FreeColonistsSpawned.Any(carrier =>
            {
                Job capture = carrier.CurJob;
                return capture != null && capture.def == ScpInteractionDefOf.SCP_Capture
                    && capture.targetA.Thing == statue && TeamAvailable(capture, carrier)
                    && capture.targetQueueB.All(target => EscortWatching(target.Pawn, carrier, statue));
            });
        }

        public static bool TeamAvailable(Job job, Pawn carrier)
        {
            if (job.targetQueueB == null || job.targetQueueB.Count != 2) return false;
            return job.targetQueueB.All(target => AssignedEscort(target.Pawn, carrier, job.targetA.Thing));
        }

        public static bool TeamNearby(Job job, Pawn carrier)
        {
            return TeamAvailable(job, carrier) && job.targetQueueB.All(target =>
                target.Pawn.Position.DistanceToSquared(carrier.Position) <= 100);
        }

        public static bool TeamReadyToPlace(Job job, Pawn carrier, Thing statue)
        {
            return TeamReady(job, carrier, statue) && job.targetQueueB.All(target =>
                !target.Pawn.pather.Moving && target.Pawn.GetRoom() == carrier.GetRoom());
        }

        public static bool TryEscortSpot(Pawn escort, IntVec3 target, IntVec3 destination, out IntVec3 spot, bool sameRoom = false)
        {
            Map map = escort.Map;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(target, 3f, false)
                .OrderBy(cell => cell.DistanceToSquared(escort.Position)))
            {
                if (!cell.InBounds(map) || cell == destination || !cell.Standable(map)
                    || cell.GetFirstPawn(map) != null
                    || (sameRoom && cell.GetRoom(map) != target.GetRoom(map))
                    || !ScpSurveillance.Sight(cell, target, map)
                    || !escort.CanReach(cell, PathEndMode.OnCell, Danger.Some)) continue;
                spot = cell;
                return true;
            }
            spot = IntVec3.Invalid;
            return false;
        }

        public static void ReplaceUnavailable(Job job, Pawn carrier)
        {
            if (job.targetQueueB == null || job.targetQueueB.Count != 2 || !carrier.Spawned) return;
            Thing statue = job.targetA.Thing;
            for (int index = 0; index < 2; index++)
            {
                if (AssignedEscort(job.targetQueueB[index].Pawn, carrier, statue)) continue;
                Pawn replacement = carrier.Map.mapPawns.FreeColonistsSpawned
                    .Where(pawn => pawn != carrier && !job.targetQueueB.Any(target => target.Pawn == pawn)
                        && pawn.IsColonistPlayerControlled && !pawn.Drafted && !pawn.Dead
                        && !pawn.Downed && !pawn.InMentalState && pawn.Awake()
                        && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                        && pawn.carryTracker.CarriedThing == null
                        && (pawn.CurJob == null || pawn.CurJob.def.defName != "SCP_173Escort")
                        && pawn.CanReach(carrier, PathEndMode.Touch, Danger.Some))
                    .OrderBy(pawn => pawn.Position.DistanceToSquared(carrier.Position)).FirstOrDefault();
                if (replacement == null) continue;
                Job watch = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("SCP_173Escort"), carrier, statue);
                watch.playerForced = true;
                if (replacement.jobs.TryTakeOrderedJob(watch, JobTag.Misc))
                    job.targetQueueB[index] = replacement;
            }
        }

        private static bool AssignedEscort(Pawn escort, Pawn carrier, Thing statue)
        {
            return escort != null && escort.Spawned && escort.Map == carrier.Map
                && !escort.Dead && !escort.Downed && !escort.InMentalState && escort.Awake()
                && escort.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                && escort.CurJob != null && escort.CurJob.def.defName == "SCP_173Escort"
                && escort.CurJob.targetA.Pawn == carrier && escort.CurJob.targetB.Thing == statue;
        }

        private static bool EscortReady(Pawn escort, Pawn carrier, Thing statue)
        {
            return EscortWatching(escort, carrier, statue)
                && (!statue.Spawned || !escort.pather.Moving);
        }

        private static bool EscortWatching(Pawn escort, Pawn carrier, Thing statue)
        {
            if (!AssignedEscort(escort, carrier, statue)) return false;
            IntVec3 target = statue.Spawned ? statue.Position : carrier.Position;
            return escort.Position.DistanceToSquared(target) <= WatchRadiusSquared
                && ScpSurveillance.Sight(escort.Position, target, carrier.Map)
                && carrier.Map.glowGrid.GroundGlowAt(target) >= 0.3f;
        }
    }
}
