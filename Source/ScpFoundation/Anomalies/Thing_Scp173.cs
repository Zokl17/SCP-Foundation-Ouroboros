using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp173 : ThingWithComps
    {
        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            // SCP-173 is an item, but damage is handled explicitly to avoid
            // treating a heavy projectile as a huge structure-damage hit.
            int damage = ScpIntegrityDamage.From(dinfo);
            if (damage > 0)
            {
                HitPoints = System.Math.Max(0, HitPoints - damage);
                if (HitPoints == 0) Destroy(DestroyMode.Vanish);
            }
            absorbed = true;
        }
    }
}
