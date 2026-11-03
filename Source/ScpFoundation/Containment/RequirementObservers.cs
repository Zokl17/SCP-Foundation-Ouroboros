using System.Collections.Generic;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementObservers : ContainmentRequirement
    {
        public int count = 3;
        public bool allowRemote;
        public override bool IsSatisfied(ContainmentContext context) { return (allowRemote && ScpSurveillance.Observed(context.thing)) || ScpObservation.PairedCount(context.thing) >= count; }
        public override string Description() { return "SCP_RequirementObservers".Translate(count) + (allowRemote ? "\n" + "SCP_RemoteAlternative".Translate().ToString() : ""); }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (count < 2 || count > 3) yield return "Paired observation supports two or three staff per object.";
        }
    }
}
