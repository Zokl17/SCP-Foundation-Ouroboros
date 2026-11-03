using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class JobDriver_Scp127Locker : JobDriver
    {
        private Building_Scp127Locker Locker { get { return job.targetA.Thing as Building_Scp127Locker; } }
        private bool Storing { get { return job.def.defName == "SCP_127Store"; } }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return Locker != null && pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(60, TargetIndex.A);
            yield return Toils_General.Do(() =>
            {
                Building_Scp127Locker locker = Locker;
                if (locker == null || pawn.inventory == null || pawn.equipment == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (Storing)
                {
                    ThingWithComps gun = pawn.equipment.Primary;
                    if (gun == null || gun.def.defName != "SCP_127Gun" || locker.StoredGun != null
                        || !pawn.equipment.TryTransferEquipmentToContainer(gun, pawn.inventory.innerContainer))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    Thing dropped;
                    if (!pawn.inventory.innerContainer.TryDrop(gun, locker.Position, pawn.Map,
                        ThingPlaceMode.Direct, 1, out dropped, null, null) || dropped == null
                        || dropped.Position != locker.Position)
                        EndJobWith(JobCondition.Incompletable);
                    return;
                }

                ThingWithComps stored = locker.StoredGun;
                if (stored == null || pawn.equipment.Primary != null
                    && !pawn.equipment.TryTransferEquipmentToContainer(pawn.equipment.Primary,
                        pawn.inventory.innerContainer))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                stored.DeSpawn();
                pawn.equipment.AddEquipment(stored);
            });
        }
    }
}
