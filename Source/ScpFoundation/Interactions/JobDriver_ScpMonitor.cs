using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class JobDriver_ScpMonitor : JobDriver
    {
        private int handoverDeadline = -1;
        private bool waiting;
        private CompMonitoringConsole Console { get { return job.targetA.Thing.TryGetComp<CompMonitoringConsole>(); } }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref handoverDeadline,"scpConsoleHandoverDeadline",-1);
            Scribe_Values.Look(ref waiting,"scpConsoleWaiting",false);
        }
        public override string GetReport()
        {
            return (waiting ? "SCP_ConsoleWaiting" : handoverDeadline >= 0 ? "SCP_ShiftWaitingRelief" : "SCP_ConsoleWorking").Translate();
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!job.targetB.IsValid) job.targetB = job.targetA.Thing.InteractionCell;
            return pawn.Reserve(job.targetA,job,2,0,errorOnFailed:errorOnFailed)
                && pawn.Reserve(job.targetB,job,errorOnFailed:errorOnFailed);
        }
        private bool KeepDuty()
        {
            if (job.playerForced || !Console.useShifts) { handoverDeadline = -1; return true; }
            if (Console.Due(pawn,true) && Console.Eligible(pawn)) { handoverDeadline = -1; return true; }
            int now = Find.TickManager.TicksGame;
            if (handoverDeadline < 0) handoverDeadline = now + ShiftPolicy.HandoverTicks;
            return ShiftPolicy.HoldForRelief(false,Console.Relief != null,CompScpPost.NeedsBreak(pawn),now,handoverDeadline);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Old saves have no target B; treat their existing job as an operator job.
            if (!job.targetB.IsValid) job.targetB = job.targetA.Thing.InteractionCell;
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !ScpSurveillance.Powered(job.targetA.Thing) || !ScpSurveillance.FitOperator(pawn) || !Console.HasAssignments);
            yield return Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
            Toil standby = ToilMaker.MakeToil();
            standby.defaultCompleteMode = ToilCompleteMode.Never;
            standby.initAction = () => waiting = pawn.Position != job.targetA.Thing.InteractionCell;
            standby.tickIntervalAction = delta =>
            {
                if (!job.playerForced && !Console.Eligible(pawn)) { EndJobWith(JobCondition.Succeeded); return; }
                if (Console.Operator == pawn || Console.Operator == null && pawn.CanReserve(job.targetA.Thing.InteractionCell)) ReadyForNextToil();
                else if (!Console.useShifts) EndJobWith(JobCondition.Succeeded);
            };
            yield return standby;
            yield return Toils_General.Do(() =>
            {
                if (!pawn.Reserve(job.targetA.Thing.InteractionCell,job)) { EndJobWith(JobCondition.Incompletable); return; }
                if (job.targetB.Cell != job.targetA.Thing.InteractionCell) pawn.Map.reservationManager.Release(job.targetB,pawn,job);
                waiting = false;
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);
            Toil watch = ToilMaker.MakeToil();
            watch.defaultCompleteMode = ToilCompleteMode.Never;
            watch.handlingFacing = true;
            watch.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (!KeepDuty()) ReadyForNextToil();
            };
            yield return watch;
            yield return Toils_General.Do(() =>
            {
                IntVec3 exit = Console.WaitingCell(pawn);
                job.targetC = exit.IsValid ? exit : pawn.Position;
            });
            yield return Toils_Goto.GotoCell(TargetIndex.C,PathEndMode.OnCell);
        }
    }
}
