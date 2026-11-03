using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp106ProtocolBait : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn == job.targetA.Pawn || pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => job.targetB.Thing.TryGetComp<CompLarryProtocol>() == null);
            if (pawn != job.targetA.Pawn)
            {
                this.FailOnDestroyedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.A);
                yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
                yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, null, false);
            }
            else
                yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);

            yield return Toils_General.Do(() =>
            {
                CompLarryProtocol chamber = job.targetB.Thing.TryGetComp<CompLarryProtocol>();
                if (chamber != null) chamber.BeginProtocol(job.targetA.Pawn);
            });
        }
    }
}
