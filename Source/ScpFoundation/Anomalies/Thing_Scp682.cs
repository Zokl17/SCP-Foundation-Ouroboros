using Verse;
using UnityEngine;
using ScpFoundation.Interactions;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp682 : Thing_ScpDirectional
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            CompScpPost post = CompScpPost.ForObject(this);
            if (post != null && post.parent.def.defName == "SCP_682AcidChamber"
                && CompScpPost.IsObjectAtPost(this))
            {
                Comps_PostDraw();
                return;
            }
            base.DrawAt(drawLoc, flip);
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            int damage = ScpIntegrityDamage.From(dinfo);
            if (damage > 0)
            {
                HitPoints = System.Math.Max(1, HitPoints - damage);
                GetComp<CompScp682>().OnDamaged();
            }
            absorbed = true;
        }
    }
}
