using CombatExtended;
using RimWorld;
using ScpFoundation.Interactions;
using UnityEngine;
using Verse;

namespace ScpFoundation.CombatExtendedCompat
{
    public sealed class Projectile_TranqDartCE : ProjectileCE
    {
        private const float HumanSizedDose = 0.24f;

        public override void Impact(Thing hitThing)
        {
            Pawn pawn = hitThing as Pawn;
            base.Impact(hitThing);
            if (pawn == null || pawn.Dead || pawn.RaceProps == null || !pawn.RaceProps.IsFlesh)
                return;

            float bodySize = pawn.BodySize < 0.1f ? 0.1f : pawn.BodySize;
            HealthUtility.AdjustSeverity(pawn, FoundationMedicalDefOf.SCP_TranqSedation, HumanSizedDose / bodySize);
        }
    }

    public sealed class Projectile_ContainmentNetCE : ProjectileCE
    {
        private const float HumanSizedNetStrength = 1f;

        public override void Impact(Thing hitThing)
        {
            Pawn pawn = hitThing as Pawn;
            base.Impact(hitThing);
            if (pawn == null || pawn.Dead)
                return;

            float bodySize = pawn.BodySize < 0.1f ? 0.1f : pawn.BodySize;
            HealthUtility.AdjustSeverity(pawn, FoundationMedicalDefOf.SCP_ContainmentNetting, HumanSizedNetStrength / bodySize);
        }
    }

    public sealed class Projectile_FoundationEmpCE : ProjectileCE
    {
        private static readonly Color PulseColor = new Color32(45, 175, 255, 255);

        public override Color DrawColor { get { return PulseColor; } }
        public override Color DrawColorTwo { get { return PulseColor; } }

        public override void Impact(Thing hitThing)
        {
            Map impactMap = Map;
            IntVec3 impactCell = Position;
            float radius = def.projectile.explosionRadius;
            base.Impact(hitThing);
            if (impactMap == null)
                return;

            FoundationEmpPulse.Apply(impactMap, impactCell, radius, launcher, hitThing);
        }
    }
}
