using System;
using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp882 : JobDriver
    {
        private int clothDelivered;

        private bool Loading { get { return job.def == ScpInteractionDefOf.SCP_882Load; } }
        private bool Replacing { get { return job.def == ScpInteractionDefOf.SCP_882ReplaceRopes; } }
        private CompScp882 Tank
        {
            get
            {
                Thing thing = Loading ? job.targetB.Thing : job.targetA.Thing;
                return thing == null ? null : thing.TryGetComp<CompScp882>();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref clothDelivered, "scp882ClothDelivered", 0);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Tank == null || !pawn.Reserve(Loading ? job.targetB : job.targetA, job, errorOnFailed: errorOnFailed)) return false;
            return (!Loading && !Replacing) || pawn.Reserve(Loading ? job.targetA : job.targetB, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(Loading ? TargetIndex.B : TargetIndex.A);
            this.FailOn(() => Tank == null || (Loading ? Tank.ContainsObject : !Tank.ContainsObject));
            if (Loading)
            {
                // Old queued jobs and newly issued jobs can carry the vanilla -1
                // sentinel; StartCarryThing requires a positive item count.
                if (job.count < 1) job.count = 1;
                this.FailOnDestroyedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
                yield return Toils_General.Do(() =>
                {
                    Thing carried = pawn.carryTracker.CarriedThing;
                    if (carried == null || !Tank.LoadObject(carried)) EndJobWith(JobCondition.Incompletable);
                });
                yield break;
            }

            if (Replacing)
            {
                Toil fetch = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return fetch;
                yield return Toils_General.Do(() =>
                {
                    Thing cloth = job.targetB.Thing;
                    if (cloth == null || !cloth.Spawned || pawn.carryTracker.CarriedThing != null
                        || pawn.carryTracker.TryStartCarry(cloth,
                            Math.Min(CompScp882.RopeClothCost - clothDelivered, cloth.stackCount), false) <= 0)
                        EndJobWith(JobCondition.Incompletable);
                });
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                yield return Toils_General.Wait(120, TargetIndex.A);
                yield return Toils_General.Do(() =>
                {
                    Thing carried = pawn.carryTracker.CarriedThing;
                    if (carried == null || carried.def != ThingDefOf.Cloth)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    clothDelivered += carried.stackCount;
                    carried.Destroy(DestroyMode.Vanish);
                    if (clothDelivered >= CompScp882.RopeClothCost)
                    {
                        Tank.ReplaceRopes();
                        return;
                    }
                    pawn.Map.reservationManager.Release(job.targetB, pawn, job);
                    Thing next = CompScp882.AvailableCloth(pawn);
                    if (next == null)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    job.SetTarget(TargetIndex.B, next);
                    if (!pawn.Reserve(next, job)) EndJobWith(JobCondition.Incompletable);
                    else JumpToToil(fetch);
                });
                yield break;
            }

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(120, TargetIndex.A);
            yield return Toils_General.Do(() =>
            {
                if (job.def == ScpInteractionDefOf.SCP_882Inspect) Tank.Inspect();
                else if (job.def == ScpInteractionDefOf.SCP_882Unload && !Tank.UnloadObject())
                    EndJobWith(JobCondition.Incompletable);
            });
        }
    }
}
