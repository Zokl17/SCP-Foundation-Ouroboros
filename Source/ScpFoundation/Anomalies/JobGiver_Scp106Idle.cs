using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobGiver_Scp106Idle : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait);
            job.expiryInterval = 120;
            return job;
        }
    }
}
