using System.Collections.Generic;
using System.Linq;
using ScpFoundation.Runtime;
using ScpFoundation.Anomalies;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class JobDriver_ScpCapture : JobDriver
    {
        private int stalledTicks;
        private int blockedTicks;
        private int nextPathTick;
        private int nextPauseNoticeTick;
        private int nextReplacementTick;
        private bool routeWarned;
        private IntVec3 lastCarrierPosition = IntVec3.Invalid;

        private bool Transporting173 { get { return job.targetA.Thing is Thing_Scp173; } }
        private bool Transporting055Crate { get { return job.targetA.Thing is Thing_Scp055ShippingCrate; } }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetB, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetC, job, errorOnFailed: errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() =>
            {
                Thing creature = job.targetA.Thing;
                CompScp682 reptile = creature == null ? null : creature.TryGetComp<CompScp682>();
                return reptile != null && pawn.carryTracker.CarriedThing != creature && !reptile.IsStunned;
            });
            this.FailOnDespawnedNullOrForbidden(TargetIndex.C);
            // An escort or another colonist may briefly step onto the post while
            // SCP-173 is already carried. Wait for the cell to clear instead of
            // aborting the job and dropping the statue in an unsafe doorway.
            this.FailOn(() => !(Transporting173 && pawn.carryTracker.CarriedThing == job.targetA.Thing)
                && !DestinationClear());
            if (Transporting173)
            {
                AddFinishAction(condition =>
                {
                    Thing statue = job.targetA.Thing;
                    if (condition != JobCondition.Succeeded && pawn.carryTracker.CarriedThing == statue)
                    {
                        CompScp173 movement = statue.TryGetComp<CompScp173>();
                        if (movement != null) movement.DelayAfterFailedTransport();
                        Thing dropped;
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                    }
                });
                yield return WaitForEscorts();
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            if (Transporting173) yield return WaitForEscorts();
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            if (Transporting173) yield return CarryWithEscorts();
            else
            {
                yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
                yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, null, false);
                yield return Toils_General.Do(() =>
                {
                    Thing thing = job.targetA.Thing;
                    if (thing.Spawned && thing.Position == job.targetB.Cell)
                    {
                        CompScpPost post = job.targetC.Thing.TryGetComp<CompScpPost>();
                        Thing_Scp055ShippingCrate crate = thing as Thing_Scp055ShippingCrate;
                        if (crate != null)
                        {
                            Thing installed = crate.InstallAt(post);
                            if (installed == null)
                            {
                                Messages.Message(Scp055Delivery.PostProblem(post, crate)
                                    ?? "SCP_055CargoNeedRoom".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                                EndJobWith(JobCondition.Incompletable);
                            }
                            else job.SetTarget(TargetIndex.A, installed);
                            return;
                        }
                        thing.TryGetComp<CompScp>().NotifyCaptured();
                        if (post != null) post.Bind(thing);
                    }
                });
            }
        }

        private bool DestinationClear()
        {
            Thing post = job.targetC.Thing;
            if (post == null || !post.Spawned || post.Faction != Faction.OfPlayer
                || !job.targetB.Cell.InBounds(post.Map)) return false;
            Thing target = job.targetA.Thing;
            if (target == null) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(job.targetB.Cell, Rot4.North, target.def.size))
                if (!cell.InBounds(post.Map) || !cell.Standable(post.Map)
                    || cell.GetThingList(post.Map).Any(thing => thing != post
                        && thing != pawn && thing != target
                        && (thing is Pawn && !Transporting055Crate || thing.def.category == ThingCategory.Item))) return false;
            return true;
        }

        private Toil WaitForEscorts()
        {
            Toil wait = ToilMaker.MakeToil();
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.tickAction = () =>
            {
                if (++stalledTicks > 6000)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (Scp173Transport.TeamReady(job, pawn, job.targetA.Thing))
                {
                    stalledTicks = 0;
                    ReadyForNextToil();
                }
                else if (stalledTicks >= 300 && Find.TickManager.TicksGame >= nextPauseNoticeTick)
                {
                    Messages.Message("SCP_173TransportWaiting".Translate(), MessageTypeDefOf.NeutralEvent, false);
                    nextPauseNoticeTick = Find.TickManager.TicksGame + 2500;
                }
            };
            return wait;
        }

        private Toil CarryWithEscorts()
        {
            Toil carry = ToilMaker.MakeToil();
            carry.defaultCompleteMode = ToilCompleteMode.Never;
            carry.tickAction = () =>
            {
                Thing statue = job.targetA.Thing;
                if (pawn.carryTracker.CarriedThing != statue)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (!Scp173Transport.TeamAvailable(job, pawn)
                    && Find.TickManager.TicksGame >= nextReplacementTick)
                {
                    nextReplacementTick = Find.TickManager.TicksGame + 120;
                    Scp173Transport.ReplaceUnavailable(job, pawn);
                }
                if (!Scp173Transport.TeamNearby(job, pawn))
                {
                    if (pawn.pather.Moving) pawn.pather.StopDead();
                    if (Find.TickManager.TicksGame >= nextPauseNoticeTick)
                    {
                        Messages.Message("SCP_173TransportPaused".Translate(), MessageTypeDefOf.NeutralEvent, false);
                        nextPauseNoticeTick = Find.TickManager.TicksGame + 2500;
                    }
                    return;
                }
                if (pawn.Position == job.targetB.Cell)
                {
                    if (!DestinationClear())
                    {
                        if (Find.TickManager.TicksGame >= nextPauseNoticeTick)
                        {
                            Messages.Message("SCP_173PostOccupied".Translate(), MessageTypeDefOf.NeutralEvent, false);
                            nextPauseNoticeTick = Find.TickManager.TicksGame + 2500;
                        }
                        return;
                    }
                    if (!Scp173Transport.TeamReadyToPlace(job, pawn, statue))
                    {
                        if (Find.TickManager.TicksGame >= nextPauseNoticeTick)
                        {
                            Messages.Message("SCP_173TransportPaused".Translate(), MessageTypeDefOf.NeutralEvent, false);
                            nextPauseNoticeTick = Find.TickManager.TicksGame + 2500;
                        }
                        return;
                    }
                    CompScpPost post = job.targetC.Thing.TryGetComp<CompScpPost>();
                    if (post == null || GenSpawn.SpawningWipes(statue.def, post.parent.def, false))
                    {
                        if (Find.TickManager.TicksGame >= nextPauseNoticeTick)
                        {
                            Messages.Message("SCP_173PostOccupied".Translate(), MessageTypeDefOf.RejectInput, false);
                            nextPauseNoticeTick = Find.TickManager.TicksGame + 2500;
                        }
                        return;
                    }
                    Thing placed;
                    if (!pawn.carryTracker.TryDropCarriedThing(post.parent.Position, ThingPlaceMode.Direct, out placed)
                        || placed != statue || !statue.Spawned || statue.Position != post.parent.Position)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    post.Bind(statue);
                    statue.TryGetComp<CompScp>().NotifyCaptured();
                    statue.TryGetComp<CompScp>().TryRecover();
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (!pawn.pather.Moving && Find.TickManager.TicksGame >= nextPathTick)
                {
                    nextPathTick = Find.TickManager.TicksGame + 60;
                    pawn.pather.StartPath(job.targetB.Cell, PathEndMode.OnCell);
                }
                if (pawn.Position != lastCarrierPosition)
                { lastCarrierPosition = pawn.Position; blockedTicks = 0; routeWarned = false; }
                else blockedTicks++;
                if (blockedTicks > 600 && !routeWarned)
                {
                    Messages.Message("SCP_173TransportBlocked".Translate(), MessageTypeDefOf.RejectInput, false);
                    routeWarned = true;
                }
            };
            return carry;
        }
    }
}
