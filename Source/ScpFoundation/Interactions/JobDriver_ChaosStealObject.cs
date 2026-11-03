using System.Collections.Generic;
using ScpFoundation.Storytelling;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class JobDriver_ChaosStealObject : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Do(() =>
            {
                Thing stolen = pawn.carryTracker == null ? null : pawn.carryTracker.CarriedThing;
                if (stolen == null) return;
                Current.Game.GetComponent<ChaosInsurgencyOperationsComponent>().NotifyObjectStolen(stolen, pawn);
                pawn.ExitMap(false, Rot4.Invalid);
            });
        }
    }
}
