using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Storytelling
{
    public sealed class JobDriver_Scp079Interview : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetB, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(600, TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Wait(300, TargetIndex.B);
            yield return Toils_General.Do(() => {
                Thing computer = job.targetA.Thing;
                Thing terminal = job.targetB.Thing;
                CompScp scp = computer == null ? null : computer.TryGetComp<CompScp>();
                CompScp079 interview = computer == null ? null : computer.TryGetComp<CompScp079>();
                if (scp == null || !scp.CanResearchNow || interview == null
                    || CompFoundationAccessTerminal.Online(pawn.Map) != terminal
                    || Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(pawn) < 2
                    || Current.Game.GetComponent<FoundationContactComponent>().Scp079InterviewAcceptedTick < 0
                    || Current.Game.GetComponent<ScpKnowledgeComponent>().LevelFor(scp.Definition)
                        < KnowledgeLevel.PartiallyDocumented)
                    return;
                if (interview.RecordInterview(job.count))
                    Messages.Message("SCP_079InterviewRecorded".Translate(), computer,
                        MessageTypeDefOf.PositiveEvent, false);
            });
        }
    }
}
