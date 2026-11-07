using Verse;
using UnityEngine;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using RimWorld;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp682 : Thing_ScpDirectional, IAttackTarget
    {
        private readonly Graphic[] facingGraphics = new Graphic[4];

        Thing IAttackTarget.Thing { get { return this; } }
        public LocalTargetInfo TargetCurrentlyAimingAt { get { return LocalTargetInfo.Invalid; } }
        public float TargetPriorityFactor { get { return 1f; } }

        public bool ThreatDisabled(IAttackTargetSearcher searcher)
        {
            CompScp682 movement = GetComp<CompScp682>();
            CompScp scp = GetComp<CompScp>();
            return !Spawned || movement != null && movement.IsStunned
                || scp != null && scp.IsInCustody && !scp.IsBreached;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            map.attackTargetsCache.UpdateTarget(this);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            CompScpPost post = CompScpPost.ForObject(this);
            if (post != null && post.parent.def.defName == "SCP_682AcidChamber"
                && CompScpPost.IsObjectAtPost(this))
            {
                Comps_PostDraw();
                return;
            }
            CompScp682 movement = GetComp<CompScp682>();
            int facing = Rotation.AsInt;
            Graphic graphic = facingGraphics[facing];
            if (graphic == null)
            {
                string suffix = Rotation == Rot4.North ? "north" : Rotation == Rot4.South ? "south"
                    : Rotation == Rot4.East ? "east" : "west";
                Vector2 size = Rotation.IsHorizontal ? new Vector2(4f, 2f)
                    : new Vector2(2.67f, 4f);
                graphic = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_" + suffix,
                    ShaderDatabase.Cutout, size, Color.white);
                facingGraphics[facing] = graphic;
            }
            graphic.Draw(drawLoc + (movement == null ? Vector3.zero : movement.DrawOffset), Rot4.North, this);
            Comps_PostDraw();
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            int damage = ScpIntegrityDamage.From(dinfo);
            if (damage > 0 && dinfo.Def != null && dinfo.Def.isRanged
                && dinfo.Def.armorCategory == DamageArmorCategoryDefOf.Sharp)
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * 0.25f));
            if (damage > 0)
            {
                HitPoints = System.Math.Max(1, HitPoints - damage);
                GetComp<CompScp682>().OnDamaged();
            }
            absorbed = true;
        }
    }
}
