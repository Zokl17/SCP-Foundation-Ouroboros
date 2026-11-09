using UnityEngine;
using RimWorld;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public class Thing_ScpDirectional : ThingWithComps, IAttackTarget
    {
        private readonly Graphic[] facingGraphics = new Graphic[4];

        // Older saves can still contain the original SCP-939 class rather than Thing_Scp939.
        Thing IAttackTarget.Thing { get { return this; } }
        public LocalTargetInfo TargetCurrentlyAimingAt { get { return LocalTargetInfo.Invalid; } }
        public float TargetPriorityFactor { get { return 1f; } }

        public virtual bool ThreatDisabled(IAttackTargetSearcher searcher)
        {
            if (!Spawned) return true;
            CompScp scp = GetComp<CompScp>();
            CompScp939 creature = GetComp<CompScp939>();
            return creature != null && creature.IsIncapacitated
                || creature != null && creature.IsSedated && (scp == null || !scp.IsBreached)
                || scp != null && scp.IsInCustody && !scp.IsBreached;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            CompScp939 creature = GetComp<CompScp939>();
            if (creature == null)
            {
                base.PreApplyDamage(ref dinfo, out absorbed);
                return;
            }
            int damage = creature.IsIncapacitated ? 0 : ScpIntegrityDamage.From(dinfo);
            if (damage > 0)
            {
                if (damage >= HitPoints)
                {
                    HitPoints = 1;
                    creature.Incapacitate();
                }
                else HitPoints -= damage;
            }
            absorbed = true;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (GetType() == typeof(Thing_ScpDirectional)) map.attackTargetsCache.UpdateTarget(this);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            int facing = Rotation.AsInt;
            Graphic graphic = facingGraphics[facing];
            if (graphic == null)
            {
                string path = def.graphicData.texPath;
                string suffix = Rotation == Rot4.North ? "north" : Rotation == Rot4.South ? "south"
                    : Rotation == Rot4.East ? "east" : "west";
                string facingPath = path + "_" + suffix;
                Texture texture = ContentFinder<Texture2D>.Get(facingPath);
                float length = Mathf.Max(def.graphicData.drawSize.x, def.graphicData.drawSize.y);
                float scale = length / Mathf.Max(texture.width, texture.height);
                Vector2 size = new Vector2(texture.width * scale, texture.height * scale);
                graphic = GraphicDatabase.Get<Graphic_Single>(facingPath, ShaderDatabase.Cutout, size, Color.white);
                facingGraphics[facing] = graphic;
            }
            CompScp939 movement = GetComp<CompScp939>();
            graphic.Draw(drawLoc + (movement == null ? Vector3.zero : movement.DrawOffset), Rot4.North, this);
            Comps_PostDraw();
        }
    }

    public sealed class Thing_Scp939 : Thing_ScpDirectional
    {
        public override bool ThreatDisabled(IAttackTargetSearcher searcher)
        {
            return base.ThreatDisabled(searcher);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            map.attackTargetsCache.UpdateTarget(this);
        }
    }
}
