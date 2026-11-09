using RimWorld;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class Apparel_Scp427 : Apparel
    {
        public override int HitPoints
        {
            get { return base.HitPoints; }
            set { base.HitPoints = Wearer != null && value < base.HitPoints ? base.HitPoints : value; }
        }
    }

    public sealed class Hediff_Scp427Exposure : Hediff
    {
        public override string SeverityLabel
        {
            get { return UnityEngine.Mathf.RoundToInt(Severity * 100f) + "%"; }
        }
    }
}
