using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp106Carry : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => job.targetB.Thing.TryGetComp<CompLarryProtocol>() == null);
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
            yield return Toils_General.Do(() =>
            {
                CompLarryProtocol chamber = job.targetB.Thing.TryGetComp<CompLarryProtocol>();
                if (chamber != null) chamber.ContainDownedSCP(job.targetA.Pawn as Pawn_Scp106);
            });
        }
    }
}
