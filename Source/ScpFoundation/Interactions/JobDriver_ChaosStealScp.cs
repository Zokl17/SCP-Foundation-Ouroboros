using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    // Only the SCP-882 story raid assigns this job. Ordinary Chaos raids keep vanilla assault AI.
    public sealed class JobDriver_ChaosStealScp : JobDriver
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
                if (stolen == null || stolen != job.targetA.Thing) return;
                Messages.Message("SCP_882RaidStoleScp".Translate(stolen.LabelCap),
                    pawn, MessageTypeDefOf.NegativeEvent, false);
                pawn.ExitMap(false, Rot4.Invalid);
            });
        }
    }
}
