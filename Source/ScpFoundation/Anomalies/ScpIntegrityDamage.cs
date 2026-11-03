using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    internal static class ScpIntegrityDamage
    {
        // Entity integrity is separate from the game's building/item hit points.
        // Cap one impact so oversized projectile damage cannot remove the whole pool.
        public static int From(DamageInfo info)
        {
            return Mathf.Clamp(Mathf.CeilToInt(info.Amount), 0, 100);
        }
    }
}
