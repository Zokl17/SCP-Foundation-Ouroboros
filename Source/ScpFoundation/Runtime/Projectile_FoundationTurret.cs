using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    internal static class FoundationTurretMuzzle
    {
        internal static Vector3 Source(Thing launcher, LocalTargetInfo target, float barrelLength)
        {
            Building_TurretGun turret = launcher as Building_TurretGun;
            if (turret == null) return launcher.DrawPos;

            Vector2 topOffset = turret.def.building.turretTopOffset;
            Vector3 pivot = turret.DrawPos + new Vector3(topOffset.x, 0f, topOffset.y);
            LocalTargetInfo aim = turret.CurrentTarget.IsValid ? turret.CurrentTarget : target;
            Vector3 direction = aim.Cell.ToVector3Shifted() - turret.DrawPos;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? pivot + direction.normalized * barrelLength
                : pivot;
        }
    }

    // Vanilla launches turret projectiles from the building center. These 2x2
    // mounts have visible barrels, so move only our own shells to their muzzle.
    public sealed class Projectile_FoundationTurretBullet : Bullet
    {
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false,
            Thing equipment = null, ThingDef targetCoverDef = null)
        {
            origin = FoundationTurretMuzzle.Source(launcher, usedTarget, 0.6f);
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags,
                preventFriendlyFire, equipment, targetCoverDef);
        }
    }

    // RimWorld's Beam uses launcher.Position (one corner of a 2x2 building) to
    // offset its visual. Attach the beam to the same muzzle as the rotating top.
    public sealed class Projectile_FoundationTurretBeam : Bullet
    {
        public override Vector3 ExactPosition { get { return destination + Vector3.up * def.Altitude; } }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false,
            Thing equipment = null, ThingDef targetCoverDef = null)
        {
            Vector3 muzzle = FoundationTurretMuzzle.Source(launcher, usedTarget, 0.68f);
            base.Launch(launcher, muzzle, usedTarget, intendedTarget, hitFlags,
                preventFriendlyFire, equipment, targetCoverDef);
            if (def.projectile.beamMoteDef != null)
                MoteMaker.MakeInteractionOverlay(def.projectile.beamMoteDef, launcher,
                    usedTarget.ToTargetInfo(Map), muzzle - launcher.DrawPos, Vector3.zero);
            ImpactSomething();
        }
    }
}
