using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp294Move : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetB, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Wait(180, TargetIndex.B);
            yield return Toils_General.Do(() =>
            {
                Building_Scp294 machine = job.targetA.Thing as Building_Scp294;
                CompScpPost post = job.targetB.Thing == null ? null
                    : job.targetB.Thing.TryGetComp<CompScpPost>();
                if (machine == null || post == null || post.parent.def != ScpInteractionDefOf.SCP_ContainmentAnchor
                    || pawn.carryTracker.CarriedThing != machine
                    || !post.parent.Spawned || post.parent.Map != pawn.Map
                    || post.parent.Faction != Faction.OfPlayer || post.BoundObject != null
                    || post.parent.Position.GetThingList(pawn.Map).Exists(thing => thing != post.parent
                        && thing != pawn && (thing is Pawn || thing.def.category == ThingCategory.Item
                            || thing.def.category == ThingCategory.Building))
                    || GenSpawn.SpawningWipes(machine.def, post.parent.def, false))
                {
                    Thing dropped;
                    if (pawn.carryTracker.CarriedThing == machine)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                Thing placed;
                if (!pawn.carryTracker.TryDropCarriedThing(post.parent.Position, ThingPlaceMode.Direct, out placed)
                    || placed != machine || !post.parent.Spawned || !machine.Spawned
                    || machine.Position != post.parent.Position)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                post.Bind(machine);
                CompScp scp = machine.GetComp<CompScp>();
                scp.NotifyCaptured();
                scp.TryRecover();
            });
        }
    }
}
