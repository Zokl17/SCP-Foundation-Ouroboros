using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class Projectile_ContainmentNet : Bullet
    {
        private const float HumanSizedNetStrength = 1f;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Pawn pawn = hitThing as Pawn;
            base.Impact(hitThing, blockedByShield);
            if (blockedByShield || pawn == null || pawn.Dead)
                return;

            float bodySize = pawn.BodySize < 0.1f ? 0.1f : pawn.BodySize;
            HealthUtility.AdjustSeverity(pawn, FoundationMedicalDefOf.SCP_ContainmentNetting, HumanSizedNetStrength / bodySize);
        }
    }
}
