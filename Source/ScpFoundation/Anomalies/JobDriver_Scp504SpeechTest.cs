using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp504SpeechTest : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Wait(150, TargetIndex.B);
            yield return Toils_General.Do(() =>
            {
                Thing tomatoes = job.targetA.Thing;
                Pawn listener = job.targetB.Pawn;
                if (!CompScp504.HasRipeFruit(tomatoes)
                    || (tomatoes.TryGetComp<CompScp504Bush>() != null && !CompScpPost.IsObjectAtPost(tomatoes))
                    || !CompScp504.CanHear(tomatoes, pawn) || !CompScp504.CanHear(tomatoes, listener))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                string outcome = Scp504Policy.TestInteraction(Rand.Value);
                InteractionDef interaction = DefDatabase<InteractionDef>.GetNamedSilentFail(outcome);
                if (interaction == null || !pawn.interactions.TryInteractWith(listener, interaction))
                    EndJobWith(JobCondition.Incompletable);
            });
        }
    }

    // A failed joke is requested by the test job, never chosen as a random colony interaction.
    public sealed class InteractionWorker_Scp504BadJoke : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient) { return 0f; }
    }
}
