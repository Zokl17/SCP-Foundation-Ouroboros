using RimWorld;
using Verse;

namespace ScpFoundation.Runtime
{
    public abstract class BreachEffect
    {
        public abstract void Apply(CompScp anomaly);
    }

    public sealed class BreachEffectLetter : BreachEffect
    {
        public string titleKey = "SCP_BreachTitle";
        public string bodyKey = "SCP_BreachBody";
        public override void Apply(CompScp anomaly)
        {
            Find.LetterStack.ReceiveLetter(anomaly.Definition.scpId + ": " + titleKey.Translate(),
                bodyKey.Translate(anomaly.PublicLabel),
                LetterDefOf.ThreatSmall, anomaly.parent);
        }
    }
}
