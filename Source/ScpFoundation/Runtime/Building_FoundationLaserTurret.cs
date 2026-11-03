using RimWorld;

namespace ScpFoundation.Runtime
{
    public sealed class Building_FoundationLaserTurret : Building_TurretGun
    {
        protected override void Tick()
        {
            base.Tick();
            if (CurrentTarget.IsValid) Top.ForceFaceTarget(CurrentTarget);
        }
    }
}
