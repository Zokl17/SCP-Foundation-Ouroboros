using System.Collections.Generic;
using ScpFoundation.Definitions;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class CompProperties_Scp : CompProperties
    {
        public ScpDef anomaly;
        public bool requireCapture;
        public CompProperties_Scp() { compClass = typeof(CompScp); }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (anomaly == null) yield return "Scp comp requires an anomaly ScpDef.";
            if (parentDef.tickerType != TickerType.Rare && parentDef.tickerType != TickerType.Normal)
                yield return "Scp comp requires tickerType Rare or Normal.";
            if (parentDef.stackLimit != 1) yield return "Scp runtime instances must not stack.";
        }
    }
}
