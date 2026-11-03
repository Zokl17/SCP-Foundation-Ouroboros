using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class JobDriver_Scp173Escort : JobDriver
    {
        private bool pursuingCarrier;
        private int nextPathTick;
        private int startedTick;

        public override bool TryMakePreToilReservations(bool errorOnFailed) { return true; }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil follow = ToilMaker.MakeToil();
            follow.defaultCompleteMode = ToilCompleteMode.Never;
            follow.handlingFacing = true;
            follow.initAction = () => startedTick = Find.TickManager.TicksGame;
            follow.tickAction = () =>
            {
                Pawn carrier = job.targetA.Pawn;
                Thing statue = job.targetB.Thing;
                if (carrier == null || !carrier.Spawned || carrier.Map != pawn.Map
                    || statue == null || statue.Destroyed
                    || (!statue.Spawned && carrier.carryTracker.CarriedThing != statue))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (carrier.CurJob == null || carrier.CurJob.def != ScpInteractionDefOf.SCP_Capture
                    || carrier.CurJob.targetA.Thing != statue)
                {
                    if (Find.TickManager.TicksGame - startedTick > 60) EndJobWith(JobCondition.Succeeded);
                    return;
                }
                bool carried = !statue.Spawned;
                LocalTargetInfo target = carried ? (LocalTargetInfo)carrier : statue;
                pawn.rotationTracker.FaceTarget(target);
                IntVec3 destination = carrier.CurJob.targetB.Cell;
                bool enterRoom = carried && carrier.Position == carrier.CurJob.targetB.Cell
                    && pawn.GetRoom() != carrier.GetRoom();
                // The carrier must be able to step onto the post; an escort standing
                // there would otherwise wait forever while blocking the delivery.
                if (pawn.Position == target.Cell || pawn.Position == destination || enterRoom)
                {
                    IntVec3 spot;
                    if (!pawn.pather.Moving && Find.TickManager.TicksGame >= nextPathTick
                        && Scp173Transport.TryEscortSpot(pawn, target.Cell, destination, out spot, enterRoom))
                    {
                        nextPathTick = Find.TickManager.TicksGame + 60;
                        pawn.pather.StartPath(spot, PathEndMode.OnCell);
                    }
                    return;
                }
                if (carried != pursuingCarrier)
                {
                    pursuingCarrier = carried;
                    pawn.pather.StopDead();
                    nextPathTick = 0;
                }
                if (pawn.Position.DistanceToSquared(target.Cell) > 9
                    || !ScpSurveillance.Sight(pawn.Position, target.Cell, pawn.Map))
                {
                    if (!pawn.pather.Moving && Find.TickManager.TicksGame >= nextPathTick)
                    {
                        nextPathTick = Find.TickManager.TicksGame + 60;
                        IntVec3 spot;
                        if (Scp173Transport.TryEscortSpot(pawn, target.Cell, destination, out spot))
                            pawn.pather.StartPath(spot, PathEndMode.OnCell);
                        else pawn.pather.StartPath(target, PathEndMode.Touch);
                    }
                    return;
                }
                if (pawn.pather.Moving) pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(target);
            };
            yield return follow;
        }
    }
}
