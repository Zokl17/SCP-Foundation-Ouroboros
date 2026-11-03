using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    // The projectile represents a short pulse of the lamp, not a damaging bullet.
    public sealed class Projectile_Scp316Light : Projectile
    {
        private static readonly Color PaleLight = new Color32(235, 235, 235, 255);

        public override Color DrawColor { get { return PaleLight; } }
        public override Color DrawColorTwo { get { return PaleLight; } }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Pawn pawn = blockedByShield ? null : hitThing as Pawn;
            if (pawn != null && !pawn.Dead && pawn.health != null)
            {
                HediffDef effect = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_316Desaturation");
                if (effect != null)
                    HealthUtility.AdjustSeverity(pawn, effect, 0.34f);
            }
            base.Impact(hitThing, blockedByShield);
        }
    }

    // The lamp only needs a left and right silhouette while held by a pawn.
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PawnRenderUtility), "DrawEquipmentAiming")]
    internal static class Scp316LampDrawPatch
    {
        private static Graphic right;
        private static Graphic left;

        private static bool Prefix(Thing eq, Vector3 drawLoc)
        {
            if (eq == null || eq.def.defName != "SCP_316Lamp") return true;
            if (right == null)
            {
                Vector2 size = new Vector2(0.8f, 0.8f);
                right = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Item/ScpArtifacts/Scp316Lamp", ShaderDatabase.Cutout, size, Color.white);
                left = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Item/ScpArtifacts/Scp316LampLeft", ShaderDatabase.Cutout, size, Color.white);
            }

            ThingWithComps weapon = eq as ThingWithComps;
            CompEquippable comp = weapon == null ? null : weapon.GetComp<CompEquippable>();
            Verb verb = comp == null ? null : comp.PrimaryVerb;
            Pawn holder = verb == null ? null : verb.CasterPawn;
            bool facesLeft = holder != null && (verb.CurrentTarget.IsValid
                ? verb.CurrentTarget.CenterVector3.x < holder.DrawPos.x
                : holder.Rotation == Rot4.West);
            (facesLeft ? left : right).Draw(drawLoc, Rot4.North, eq);
            return false;
        }
    }
}
