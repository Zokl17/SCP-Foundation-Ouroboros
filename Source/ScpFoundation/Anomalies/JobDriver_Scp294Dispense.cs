using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp294Dispense : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(180, TargetIndex.A);
            yield return Toils_General.Do(() =>
            {
                Building_Scp294 machine = job.targetA.Thing as Building_Scp294;
                if (machine != null) machine.Dispense(pawn, job.count);
            });
        }
    }
}
