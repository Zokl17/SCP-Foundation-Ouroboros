using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    // The unloaded machine stays carryable until a colonist installs it at a post.
    public sealed class Thing_Scp294Transit : ThingWithComps
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (!Spawned || pawn == null || pawn.Faction != Faction.OfPlayer || pawn.Dead
                || pawn.Downed || pawn.jobs == null || pawn.Map != Map
                || !pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some)) yield break;
            bool found = false;
            foreach (CompScpPost post in CompScpPost.All(Map))
            {
                if (!CanDeliverTo(pawn, post)) continue;
                found = true;
                Thing destination = post.parent;
                yield return new FloatMenuOption("SCP_294MoveToPost".Translate(destination.Position), () =>
                {
                    if (!CanDeliverTo(pawn, post)) return;
                    JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("SCP_294CourierDeliver");
                    if (jobDef == null) return;
                    Job job = JobMaker.MakeJob(jobDef, this, destination);
                    job.count = 1;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
            if (!found) yield return new FloatMenuOption("SCP_294NoPost".Translate(), null);
        }

        private bool CanDeliverTo(Pawn pawn, CompScpPost post)
        {
            return post != null && post.parent.def == ScpInteractionDefOf.SCP_ContainmentAnchor
                && post.parent.Spawned && post.parent.Map == Map
                && post.parent.Faction == Faction.OfPlayer && post.BoundObject == null
                && !post.parent.Position.GetThingList(Map).Any(thing => thing != post.parent
                    && (thing is Pawn || thing.def.category == ThingCategory.Item
                        || thing.def.category == ThingCategory.Building))
                && pawn.CanReserveAndReach(post.parent, PathEndMode.OnCell, Danger.Some);
        }
    }
}
