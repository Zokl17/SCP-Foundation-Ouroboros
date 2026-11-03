using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class JobDriver_ScpSession : JobDriver
    {
        private bool researchReady;
        private int nextResearchCheck;
        private int handoverDeadline = -1;
        private bool Study { get { return job.def == ScpInteractionDefOf.SCP_Study; } }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            CompScpInteraction interaction = job.targetA.Thing == null ? null : job.targetA.Thing.TryGetComp<CompScpInteraction>();
            if (interaction == null || !interaction.CanWorkAtPost) return false;
            return pawn.Reserve(job.targetA, job, ShiftPolicy.ReservationSlots, 0, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetB, job, errorOnFailed: errorOnFailed);
        }
        public override string GetReport()
        {
            if (handoverDeadline >= 0) return "SCP_ShiftWaitingRelief".Translate();
            string key = Study ? (pawn.pather.Moving ? "SCP_StudyMoving" : researchReady ? "SCP_StudyWorking" : "SCP_StudyWaiting")
                : job.def == ScpInteractionDefOf.SCP_Observe ? (pawn.pather.Moving ? "SCP_ObserveMoving" : "SCP_ObserveWorking") : "SCP_RestoreWorking";
            return key.Translate();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref handoverDeadline, "scpHandoverDeadline", -1);
        }

        private bool ContinueShift(CompScp anomaly)
        {
            if (job.playerForced || job.def == ScpInteractionDefOf.SCP_Restore) return true;
            CompScpPost post = CompScpPost.ForObject(anomaly.parent);
            if (post == null || !post.useShifts) { handoverDeadline = -1; return true; }
            if (CompScpPost.NeedsBreak(pawn)) return false;
            bool researcherWatching = !Study && job.def == ScpInteractionDefOf.SCP_Observe
                && !anomaly.CanResearchNow && post.OnDuty(pawn, true, true);
            if (post.OnDuty(pawn, Study, true) || researcherWatching)
            {
                handoverDeadline = -1;
                return true;
            }
            if (Study) return false;
            int now = Find.TickManager.TicksGame;
            if (handoverDeadline < 0) handoverDeadline = now + ShiftPolicy.HandoverTicks;
            bool critical = (pawn.needs.food != null && pawn.needs.food.CurLevelPercentage < 0.2f)
                || (pawn.needs.rest != null && pawn.needs.rest.CurLevelPercentage < 0.2f);
            bool hold = ShiftPolicy.HoldForRelief(false, post.ReliefReady(anomaly.parent) >= ScpObservation.StaffTarget(anomaly.Definition), critical, now, handoverDeadline);
            return hold;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => job.targetA.Thing == null || job.targetA.Thing.TryGetComp<CompScpInteraction>() == null
                || !job.targetA.Thing.TryGetComp<CompScpInteraction>().CanWorkAtPost);
            // Keep the three study phases stable for jobs saved on a later toil.
            for (int phase = 0; phase < (Study ? 3 : 1); phase++)
            {
                if (phase > 0)
                    yield return Toils_General.Do(() =>
                    {
                        IntVec3 next;
                        if (!ScpObservation.TrySpot(pawn, job.targetA.Thing, out next, true, true)) return;
                        if (!pawn.Reserve(next, job)) return;
                        pawn.Map.reservationManager.Release(job.targetB, pawn, job);
                        job.targetB = next;
                    });
                yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
                Toil session = ToilMaker.MakeToil();
                // Both ordered and automatic observation last until duty/needs conditions end them.
                // Research changes position twice, then remains active until the shift,
                // needs, containment or completed dossier ends it. This keeps the
                // progress bar stable instead of recreating the job every 1800 ticks.
                session.defaultCompleteMode = SessionWorkPolicy.ContinuousSession(Study, phase)
                    ? ToilCompleteMode.Never : ToilCompleteMode.Delay;
                session.defaultDuration = 600;
                session.handlingFacing = true;
                session.initAction = () => { pawn.pather.StopDead(); pawn.rotationTracker.FaceTarget(job.targetA); researchReady = false; nextResearchCheck = 0; };
                session.AddFailCondition(() => (pawn.needs.food != null && pawn.needs.food.CurLevelPercentage < 0.1f)
                    || (pawn.needs.rest != null && pawn.needs.rest.CurLevelPercentage < 0.1f));
                session.tickIntervalAction = delta =>
                {
                    CompScpInteraction interaction = job.targetA.Thing.TryGetComp<CompScpInteraction>();
                    CompScp anomaly = interaction.Anomaly;
                    if (!job.playerForced && job.def == ScpInteractionDefOf.SCP_Observe && ScpSurveillance.Observed(anomaly.parent))
                    { EndJobWith(JobCondition.Succeeded); return; }
                    if (!ContinueShift(anomaly)) { EndJobWith(JobCondition.Succeeded); return; }
                    if (!interaction.CanWorkAtPost || !ScpSurveillance.Sight(pawn.Position, anomaly.parent.Position, pawn.Map)
                        || pawn.Position.DistanceToSquared(anomaly.parent.Position) > 36
                        || (job.def != ScpInteractionDefOf.SCP_Observe && pawn.GetRoom() != anomaly.parent.Position.GetRoom(pawn.Map))
                        || (job.def == ScpInteractionDefOf.SCP_Observe && !interaction.observationEnabled)
                        || (Study && !interaction.researchEnabled))
                    { EndJobWith(JobCondition.Incompletable); return; }
                    pawn.rotationTracker.FaceTarget(anomaly.parent);
                    if (!Study)
                    {
                        if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research))
                            pawn.skills.Learn(SkillDefOf.Intellectual, delta * 0.01f);
                        return;
                    }
                    if (anomaly.Definition.research == null) return;
                    if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research))
                    { EndJobWith(JobCondition.Incompletable); return; }
                    if (Find.TickManager.TicksGame >= nextResearchCheck)
                    {
                        researchReady = anomaly.CanResearchNow;
                        nextResearchCheck = Find.TickManager.TicksGame + 60;
                    }
                    float amount = ScpStudyApparel.ResearchAmount(pawn,
                        SessionWorkPolicy.ResearchAmount(Study, researchReady, pawn.pather.Moving, pawn.GetStatValue(StatDefOf.ResearchSpeed), delta));
                    if (amount <= 0) return;
                    ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                    bool experimenting = knowledge.LevelFor(anomaly.Definition) == KnowledgeLevel.Documented;
                    if (knowledge.AddResearch(anomaly.Definition, amount))
                    {
                        string title = experimenting ? "SCP_ExperimentLetter".Translate() : "SCP_ResearchLetter".Translate();
                        string body = experimenting
                            ? "SCP_ExperimentAdvanced".Translate(anomaly.Definition.scpId,
                                knowledge.ExperimentStageFor(anomaly.Definition),
                                ResearchModel.ExperimentTrust(anomaly.Definition.objectClass,
                                    knowledge.ExperimentStageFor(anomaly.Definition))).ToString() + "\n\n" + interaction.Dossier()
                            : interaction.Dossier();
                        Find.LetterStack.ReceiveLetter(title, body, LetterDefOf.PositiveEvent, anomaly.parent);
                    }
                    pawn.skills.Learn(SkillDefOf.Intellectual, delta * 0.05f);
                    if (!knowledge.NeedsResearch(anomaly.Definition)) EndJobWith(JobCondition.Succeeded);
                };
                if (Study)
                {
                    session.activeSkill = () => SkillDefOf.Intellectual;
                }
                yield return session;
            }
            if (job.def == ScpInteractionDefOf.SCP_Restore)
                yield return Toils_General.Do(() =>
                {
                    if (!job.targetA.Thing.TryGetComp<CompScp>().TryRecover())
                        job.targetA.Thing.TryGetComp<CompScp>().ShowRequirements();
                });
        }
    }
}
