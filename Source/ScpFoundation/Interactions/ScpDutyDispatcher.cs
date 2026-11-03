using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    // Active rosters are duties, not ordinary low-priority work scans. This dispatcher
    // brings assigned pawns back after safe breaks and preempts recreation or meditation.
    public static class ScpDutyDispatcher
    {
        public static void Dispatch(Map map)
        {
            if (map == null || !map.IsPlayerHome) return;
            var claimed = new HashSet<Pawn>();
            foreach (CompScpPost post in CompScpPost.All(map).ToList()) DispatchPost(post, claimed);
            foreach (CompMonitoringConsole console in CompMonitoringConsole.All(map).ToList()) DispatchConsole(console, claimed);
            RestoreReadyContainment(map);
        }

        private static bool Available(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && !pawn.InMentalState && !pawn.Drafted
                && pawn.Map != null && pawn.jobs != null;
        }

        private static bool AlreadyAt(Pawn pawn, Thing target, bool console, bool study)
        {
            if (pawn == null || pawn.CurJob == null || pawn.CurJob.targetA.Thing != target) return false;
            return DutyDispatchPolicy.MatchesDutyJob(console, study,
                pawn.CurJob.def == ScpInteractionDefOf.SCP_Monitor,
                pawn.CurJob.def == ScpInteractionDefOf.SCP_Observe,
                pawn.CurJob.def == ScpInteractionDefOf.SCP_Study);
        }

        private static bool CanDispatch(Pawn pawn, Thing target, bool console, bool study)
        {
            bool currentInterruptible = pawn == null || pawn.CurJobDef == null || pawn.CurJobDef.playerInterruptible;
            bool playerForced = pawn != null && pawn.CurJob != null && pawn.CurJob.playerForced;
            return DutyDispatchPolicy.ShouldDispatch(true, Available(pawn), pawn != null && CompScpPost.NeedsBreak(pawn),
                AlreadyAt(pawn, target, console, study), playerForced, currentInterruptible);
        }

        private static bool Start(Pawn pawn, Job job)
        {
            if (job == null) return false;
            job.playerForced = false;
            return pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
        }

        private static void DispatchPost(CompScpPost post, HashSet<Pawn> claimed)
        {
            if (post == null || !post.useShifts || !post.parent.Spawned) return;
            Thing target = post.BoundObject;
            if (target == null || !target.Spawned || target.Map != post.parent.Map) return;
            float hour = GenLocalDate.HourFloat(post.parent.Map);
            foreach (ScpDutyShift shift in post.shifts.ToList())
            {
                if (shift == null || !ShiftPolicy.Due(shift.hours, hour, true)) continue;
                TryPostPawn(post, target, shift.observerA, false, claimed);
                TryPostPawn(post, target, shift.observerB, false, claimed);
                if (ShiftPolicy.Due(shift.hours, hour, false)) TryPostPawn(post, target, shift.researcher, true, claimed);
            }
        }

        private static void TryPostPawn(CompScpPost post, Thing target, Pawn pawn, bool study, HashSet<Pawn> claimed)
        {
            if (claimed.Contains(pawn) || !CanDispatch(pawn, target, false, study) || !post.CanStart(pawn, study)) return;
            WorkGiver_ScpObserve giver = study ? (WorkGiver_ScpObserve)new WorkGiver_ScpStudy() : new WorkGiver_ScpObserve();
            if (Start(pawn, giver.JobOnThing(pawn, target, false))) claimed.Add(pawn);
        }

        private static void DispatchConsole(CompMonitoringConsole console, HashSet<Pawn> claimed)
        {
            if (console == null || !console.useShifts || !console.parent.Spawned || !console.HasAssignments) return;
            foreach (ConsoleDutyShift shift in console.shifts.ToList())
            {
                Pawn pawn = shift == null ? null : shift.employee;
                if (claimed.Contains(pawn) || !console.Due(pawn, true) || !console.Eligible(pawn)
                    || !CanDispatch(pawn, console.parent, true, false)) continue;
                Job job = WorkGiver_ScpMonitor.CreateJob(pawn, console.parent, false);
                if (Start(pawn, job)) claimed.Add(pawn);
            }
        }

        private static void RestoreReadyContainment(Map map)
        {
            foreach (CompScpPost post in CompScpPost.All(map).ToList())
            {
                Thing target = post.BoundObject;
                CompScp anomaly = target == null ? null : target.TryGetComp<CompScp>();
                if (anomaly == null || !ScpOperationalPolicy.NeedsRecovery(anomaly.OperationalFlags)) continue;
                bool localDutyPresent = ScpObservation.Observers(target).Any(pawn => !post.useShifts
                    || post.OnDuty(pawn, false, true) || post.OnDuty(pawn, true, true));
                float hour = GenLocalDate.HourFloat(post.parent.Map);
                bool assignedDuty = post.useShifts && post.shifts.Any(shift => shift != null
                    && ShiftPolicy.Due(shift.hours, hour, true)
                    && (shift.observerA != null || shift.observerB != null || shift.researcher != null));
                if (assignedDuty || localDutyPresent || ScpSurveillance.Observed(target)) anomaly.TryRecover();
            }
        }
    }
}
