using RimWorld;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp173 : ThingWithComps, IAttackTarget
    {
        private int ruinedUntilTick;
        private Graphic ruinedGraphic;

        public bool IsRuined { get { return ruinedUntilTick > 0; } }
        public int RuinedUntilTick { get { return ruinedUntilTick; } }

        Thing IAttackTarget.Thing { get { return this; } }
        public LocalTargetInfo TargetCurrentlyAimingAt { get { return LocalTargetInfo.Invalid; } }
        public float TargetPriorityFactor { get { return 1f; } }
        public bool ThreatDisabled(IAttackTargetSearcher searcher)
        {
            CompScp scp = GetComp<CompScp>();
            return !Spawned || IsRuined || scp != null && scp.IsInCustody && !scp.IsBreached;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ruinedUntilTick, "scp173RuinedUntilTick", 0);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            map.attackTargetsCache.UpdateTarget(this);
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            // SCP-173 is an item, but damage is handled explicitly to avoid
            // treating a heavy projectile as a huge structure-damage hit.
            int damage = IsRuined ? 0 : ScpIntegrityDamage.From(dinfo);
            if (damage >= HitPoints && damage > 0)
            {
                HitPoints = 1;
                ruinedUntilTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
                if (Spawned) Map.attackTargetsCache.UpdateTarget(this);
            }
            else if (damage > 0) HitPoints -= damage;
            absorbed = true;
        }

        public void RestoreIfDue(int now)
        {
            if (!IsRuined || now < ruinedUntilTick) return;
            ruinedUntilTick = 0;
            HitPoints = MaxHitPoints;
            if (Spawned) Map.attackTargetsCache.UpdateTarget(this);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!IsRuined)
            {
                base.DrawAt(drawLoc, flip);
                return;
            }
            if (ruinedGraphic == null)
                ruinedGraphic = GraphicDatabase.Get<Graphic_Single>("Things/Item/Scp173Destroyed",
                    ShaderDatabase.Cutout, new Vector2(2.1f, 1.4f), Color.white);
            ruinedGraphic.Draw(drawLoc, Rot4.North, this);
            Comps_PostDraw();
        }
    }
}
