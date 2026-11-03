using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Storytelling;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class JobDriver_Load650ForReturn : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, ShiftPolicy.ReservationSlots, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition => {
                if (condition != JobCondition.Succeeded && pawn.carryTracker.CarriedThing == job.targetA.Thing)
                {
                    Thing dropped;
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                }
            });
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => {
                QuestPart_Scp650Return transfer = QuestPart_Scp650Return.For(job.targetA.Thing);
                return transfer == null || transfer.ship == null || !transfer.ship.ShipExistsAndIsSpawned
                    || transfer.ship.shipThing != job.targetB.Thing;
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Wait(60, TargetIndex.B).WithProgressBarToilDelay(TargetIndex.B);
            yield return Toils_General.Do(() => {
                CompTransporter transporter = job.targetB.Thing.TryGetComp<CompTransporter>();
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried != job.targetA.Thing || transporter == null
                    || !transporter.innerContainer.TryAddOrTransfer(carried, true))
                    EndJobWith(JobCondition.Incompletable);
            });
        }
    }
}
