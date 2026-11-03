using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Knowledge;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp294Research : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil research = ToilMaker.MakeToil();
            research.defaultCompleteMode = ToilCompleteMode.Never;
            research.activeSkill = () => SkillDefOf.Intellectual;
            research.tickAction = () =>
            {
                Building_Scp294 machine = job.targetA.Thing as Building_Scp294;
                if (machine == null || !machine.CanResearch(pawn, job.def))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                bool experimenting = knowledge.LevelFor(machine.Anomaly) == KnowledgeLevel.Documented;
                bool advanced = knowledge.AddResearch(machine.Anomaly,
                    ScpStudyApparel.ResearchAmount(pawn, pawn.GetStatValue(StatDefOf.ResearchSpeed)));
                if (advanced)
                {
                    if (experimenting)
                    {
                        int stage = knowledge.ExperimentStageFor(machine.Anomaly);
                        Messages.Message("SCP_ExperimentAdvanced".Translate(machine.Anomaly.scpId, stage,
                            ResearchModel.ExperimentTrust(machine.Anomaly.objectClass, stage)), machine,
                            MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        ScpEventRecord researchEntry = ScpEventJournal.Add("SCP-294", machine,
                            "SCP_Journal294Research".Translate((int)knowledge.LevelFor(machine.Anomaly)));
                        Messages.Message(ScpEventJournal.Notice(researchEntry, "SCP_294ResearchAdvanced".Translate()), machine,
                            MessageTypeDefOf.PositiveEvent, false);
                    }
                }
                pawn.skills.Learn(SkillDefOf.Intellectual, 0.03f);
                KnowledgeLevel level = knowledge.LevelFor(machine.Anomaly);
                if (job.def.defName == "SCP_294Study" && level >= KnowledgeLevel.Observed
                    || job.def.defName == "SCP_294Experiment" && level >= KnowledgeLevel.Documented
                        && (knowledge.ExperimentsComplete(machine.Anomaly) || experimenting && advanced))
                    EndJobWith(JobCondition.Succeeded);
            };
            yield return research;
        }
    }
}
