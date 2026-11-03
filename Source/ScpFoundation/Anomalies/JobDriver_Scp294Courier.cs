using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp294Courier : JobDriver
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
                CompScpPost post = job.targetB.Thing == null ? null : job.targetB.Thing.TryGetComp<CompScpPost>();
                Thing carried = pawn.carryTracker.CarriedThing;
                if (post == null || post.parent.def != ScpInteractionDefOf.SCP_ContainmentAnchor
                    || carried == null || carried.def.defName != "SCP_294Transit"
                    || !post.parent.Spawned || post.parent.Map != pawn.Map
                    || post.parent.Faction != Faction.OfPlayer || post.BoundObject != null
                    || post.parent.Position.GetThingList(pawn.Map).Any(thing => thing != post.parent
                        && thing != pawn && (thing is Pawn || thing.def.category == ThingCategory.Item
                            || thing.def.category == ThingCategory.Building)))
                {
                    Thing dropped;
                    if (carried != null) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                Thing machine = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_294Machine"));
                machine.SetFaction(Faction.OfPlayer);
                if (GenSpawn.SpawningWipes(machine.def, post.parent.def, false))
                {
                    Thing dropped;
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                try
                {
                    GenSpawn.Spawn(machine, post.parent.Position, pawn.Map);
                    post.Bind(machine);
                    CompScp scp = machine.TryGetComp<CompScp>();
                    scp.NotifyCaptured();
                    scp.TryRecover();
                    carried.Destroy(DestroyMode.Vanish);
                }
                catch (System.Exception error)
                {
                    if (machine.Spawned) machine.Destroy(DestroyMode.Vanish);
                    Thing dropped;
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                    Log.Error("SCP-294 installation failed: " + error);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
        }
    }
}
