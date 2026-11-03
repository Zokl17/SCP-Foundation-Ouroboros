using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Runtime
{
    public sealed class JobGiver_ChimeraIdle : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            job.expiryInterval = 120;
            return job;
        }
    }
}
