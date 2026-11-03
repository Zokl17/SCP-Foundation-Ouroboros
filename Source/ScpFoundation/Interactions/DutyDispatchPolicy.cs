namespace ScpFoundation.Interactions
{
    public static class DutyDispatchPolicy
    {
        public static bool MatchesDutyJob(bool console, bool study, bool monitoringJob, bool observationJob, bool studyJob)
        {
            return console ? monitoringJob : study ? studyJob : observationJob;
        }

        public static bool ShouldDispatch(bool rosterActive, bool available, bool needsBreak, bool alreadyAtDuty,
            bool playerForcedJob, bool currentJobInterruptible)
        {
            return rosterActive && available && !needsBreak && !alreadyAtDuty && !playerForcedJob && currentJobInterruptible;
        }
    }
}
