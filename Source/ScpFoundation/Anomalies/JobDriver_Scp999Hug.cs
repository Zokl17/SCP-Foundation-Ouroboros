using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp999Hug : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(240, TargetIndex.A);
            yield return Toils_General.Do(() =>
            {
                if (!(job.targetA.Thing is Thing_Scp999) || pawn.needs == null
                    || pawn.needs.mood == null) return;
                ThoughtDef hugged = DefDatabase<ThoughtDef>.GetNamedSilentFail("SCP_999Hugged");
                if (hugged != null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(hugged);
                    ScpEventJournal.Add("SCP-999", job.targetA.Thing,
                        "SCP_Journal999Hug".Translate(pawn.LabelShortCap));
                }
            });
        }
    }
}
