using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Definitions;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using Verse;
using Verse.AI;

namespace ScpFoundation.Knowledge
{
    public sealed class JobDriver_ScpArchiveResearch : JobDriver
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
                ScpDef article = ScpArticleCatalog.ForNumber(job.count);
                ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                if (article == null || knowledge == null || !knowledge.HasEncountered(article)
                    || knowledge.ExperimentsComplete(article)
                    || CompFoundationAccessTerminal.Online(pawn.Map) != job.targetA.Thing
                    || Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(pawn) < 1)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                bool experimenting = knowledge.LevelFor(article) == KnowledgeLevel.Documented;
                if (knowledge.AddResearch(article, pawn.GetStatValue(StatDefOf.ResearchSpeed)))
                {
                    int stage = knowledge.ExperimentStageFor(article);
                    Messages.Message(experimenting
                        ? "SCP_ExperimentAdvanced".Translate(article.scpId, stage,
                            ResearchModel.ExperimentTrust(article.objectClass, stage))
                        : "SCP_ArchiveAdvanced".Translate(article.scpId), job.targetA.Thing,
                        MessageTypeDefOf.PositiveEvent, false);
                    EndJobWith(JobCondition.Succeeded);
                }
                pawn.skills.Learn(SkillDefOf.Intellectual, 0.03f);
            };
            yield return research;
        }
    }
}
