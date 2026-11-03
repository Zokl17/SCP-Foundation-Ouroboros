using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Definitions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public static class ScpObservation
    {
        public static int StaffTarget(ScpDef definition)
        {
            return definition.requirements.OfType<RequirementObservers>().Select(requirement => requirement.count).DefaultIfEmpty(0).Max();
        }

        public static List<Pawn> Observers(Thing thing)
        {
            List<Pawn> result = new List<Pawn>();
            if (!thing.Spawned || thing.Map.glowGrid.GroundGlowAt(thing.Position) < 0.3f) return result;
            foreach (Pawn pawn in thing.Map.mapPawns.FreeColonistsSpawned)
            {
                if (Status(pawn, thing) == ObservationStatus.Ready) result.Add(pawn);
            }
            return result;
        }

        private static bool HasDuty(Pawn pawn, Thing thing)
        {
            return pawn.CurJob != null && ((pawn.CurJob.targetA.Thing == thing
                && (pawn.CurJob.def == ScpInteractionDefOf.SCP_Observe || pawn.CurJob.def == ScpInteractionDefOf.SCP_Study
                    || pawn.CurJob.def == ScpInteractionDefOf.SCP_Restore))
                || (pawn.CurJob.def.defName == "SCP_173Escort" && pawn.CurJob.targetB.Thing == thing));
        }

        public static ObservationStatus Status(Pawn pawn, Thing thing)
        {
            if (!thing.Spawned || !pawn.Spawned || pawn.Map != thing.Map) return ObservationStatus.Unavailable;
            return ObservationPolicy.Evaluate(!pawn.Dead && !pawn.Downed && pawn.Awake(),
                pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight), pawn.pather.Moving,
                pawn.stances.curStance is Stance_Busy, HasDuty(pawn, thing),
                pawn.Drafted && pawn.CurJob != null && pawn.CurJob.def == JobDefOf.Wait_Combat,
                pawn.Position.DistanceToSquared(thing.Position), ScpSurveillance.Sight(pawn.Position, thing.Position, thing.Map),
                thing.Map.glowGrid.GroundGlowAt(thing.Position));
        }

        public static string Report(Thing thing, int required)
        {
            var text = new StringBuilder();
            List<Pawn> observers = Observers(thing);
            // Show every individually valid observer. Pairing is still checked separately by
            // RequirementObservers, and the per-pawn line explains a missing partner.
            text.AppendLine("SCP_ObserversCount".Translate(observers.Count, required));
            if (ScpSurveillance.Permitted(thing)) text.AppendLine((ScpSurveillance.Observed(thing) ? "SCP_MonitorLive" : "SCP_MonitorLost").Translate());
            foreach (Pawn pawn in thing.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Position.DistanceToSquared(thing.Position) > 64 && !HasDuty(pawn, thing)) continue;
                ObservationStatus status = Status(pawn, thing);
                string reason = status == ObservationStatus.Ready
                    && !observers.Any(other => other != pawn && ScpSurveillance.Sight(pawn.Position, other.Position, thing.Map))
                    ? "SCP_ObserverNoPartner" : "SCP_Observer_" + status;
                text.AppendLine(pawn.LabelShortCap + ": " + reason.Translate());
            }
            text.Append("SCP_ObserversHelp".Translate());
            return text.ToString();
        }

        public static int PairedCount(Thing thing, Pawn except = null)
        {
            List<Pawn> observers = Observers(thing).Where(observer => observer != except).ToList();
            int count = 0;
            foreach (Pawn pawn in observers)
            {
                foreach (Pawn other in observers)
                {
                    if (pawn == other || !ScpSurveillance.Sight(pawn.Position, other.Position, thing.Map)) continue;
                    count++;
                    break;
                }
            }
            return count;
        }

        public static int AssignedCount(Thing thing, Pawn except = null)
        {
            int count = 0;
            foreach (Pawn pawn in thing.Map.mapPawns.FreeColonistsSpawned)
                if (pawn != except && (HasDuty(pawn, thing) || Status(pawn, thing) == ObservationStatus.Ready)) count++;
            return count;
        }

        public static int AssignedTo(Thing thing, JobDef def, Pawn except = null)
        {
            return thing.Map.mapPawns.FreeColonistsSpawned.Count(pawn => pawn != except && pawn.CurJob != null
                && pawn.CurJob.targetA.Thing == thing && pawn.CurJob.def == def);
        }

        public static bool TrySpot(Pawn pawn, Thing thing, out IntVec3 spot, bool changeSide = false, bool sameRoom = false)
        {
            if (pawn == null || pawn.health == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight))
            {
                spot = IntVec3.Invalid;
                return false;
            }
            IEnumerable<IntVec3> candidates = GenRadial.RadialCellsAround(thing.Position, sameRoom ? 3f : 6f, false)
                .OrderBy(cell => cell.DistanceToSquared(pawn.Position));
            if (changeSide) candidates = candidates.OrderByDescending(cell => cell.DistanceToSquared(pawn.Position));
            foreach (IntVec3 cell in candidates)
            {
                if (!cell.InBounds(thing.Map) || !cell.Standable(thing.Map)) continue;
                Pawn occupant = cell.GetFirstPawn(thing.Map);
                if (occupant != null && occupant != pawn) continue;
                if (sameRoom && cell.GetRoom(thing.Map) != thing.Position.GetRoom(thing.Map)) continue;
                if (!ScpSurveillance.Sight(cell, thing.Position, thing.Map) || !pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some)) continue;
                spot = cell;
                return true;
            }
            spot = IntVec3.Invalid;
            return false;
        }
    }
}
