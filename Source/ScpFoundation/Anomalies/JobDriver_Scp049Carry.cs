using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp049Carry : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetC, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.C);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            // A pawn cannot be placed on the post while the carrier stands on it.
            // Approach from a free adjacent cell, then drop directly onto the post.
            yield return Toils_General.Do(() =>
            {
                Thing post = job.targetC.Thing;
                IntVec3 spot = GenRadial.RadialCellsAround(post.Position, 1.5f, false)
                    .Where(cell => cell != post.Position && cell.InBounds(pawn.Map) && cell.Standable(pawn.Map)
                        && cell.GetRoom(pawn.Map) == post.Position.GetRoom(pawn.Map)
                        && (cell.GetFirstPawn(pawn.Map) == null || cell.GetFirstPawn(pawn.Map) == pawn)
                        && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                    .OrderBy(cell => cell.DistanceToSquared(pawn.Position))
                    .DefaultIfEmpty(IntVec3.Invalid).First();
                if (!spot.IsValid) { EndJobWith(JobCondition.Incompletable); return; }
                job.targetB = spot;
            });
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Do(() =>
            {
                Pawn_Scp049 doctor = job.targetA.Pawn as Pawn_Scp049;
                Thing post = job.targetC.Thing;
                Thing placed;
                if (doctor == null || pawn.carryTracker.CarriedThing != doctor
                    || pawn.Position == post.Position
                    || !pawn.carryTracker.TryDropCarriedThing(post.Position, ThingPlaceMode.Direct, out placed)
                    || placed != doctor || !doctor.ContainAtPost(post))
                    EndJobWith(JobCondition.Incompletable);
            });
        }
    }
}
