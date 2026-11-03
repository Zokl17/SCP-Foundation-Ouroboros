using System;
using System.Collections.Generic;
using ScpFoundation.Containment;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Definitions
{
    public sealed class ScpResearchSpec
    {
        public float observedWork = 625;
        public float partialWork = 2500;
        public float fullWork = 7500;
        public string observedReportKey;
        public string partialReportKey;
        public string documentedReportKey;
        public List<string> experimentReportKeys = new List<string>();
    }
    public sealed class ScpSource
    {
        public string title;
        public string author;
        public string url;
        public string license;
        public bool originalFixture;
    }

    public class ScpDef : Def
    {
        public string scpId;
        public string objectClass;
        public bool archiveOnly;
        public List<string> tags = new List<string>();
        public List<ContainmentRequirement> requirements = new List<ContainmentRequirement>();
        public BreachPolicy breach = new BreachPolicy();
        public List<BreachEffect> breachEffects = new List<BreachEffect>();
        public ScpSource source;
        public ScpResearchSpec research;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (research != null && (!NonnegativeFinite(research.observedWork) || !NonnegativeFinite(research.partialWork) || !NonnegativeFinite(research.fullWork)
                || research.observedWork <= 0 || research.partialWork <= research.observedWork || research.fullWork <= research.partialWork
                || string.IsNullOrEmpty(research.observedReportKey) || string.IsNullOrEmpty(research.partialReportKey)
                || string.IsNullOrEmpty(research.documentedReportKey))) yield return "Research requires ordered positive work thresholds and all report keys.";
            if (research != null && research.experimentReportKeys != null && research.experimentReportKeys.Count > 0
                && (research.experimentReportKeys.Count != 3 || research.experimentReportKeys.Exists(string.IsNullOrEmpty)))
                yield return "Experiment reports require exactly three nonempty translation keys.";
            if (string.IsNullOrEmpty(scpId)) yield return "scpId is required.";
            if (string.IsNullOrEmpty(objectClass)) yield return "objectClass is required.";
            if (source == null) yield return "Source attribution is required.";
            else if (string.IsNullOrEmpty(source.title) || string.IsNullOrEmpty(source.author) || string.IsNullOrEmpty(source.license)
                || (!source.originalFixture && string.IsNullOrEmpty(source.url)))
                yield return "Source must include title, author, license and URL (except original fixtures).";
            if (!archiveOnly && (requirements == null || requirements.Count == 0)) yield return "At least one requirement is required.";
            else
            {
                HashSet<string> ids = new HashSet<string>();
                foreach (ContainmentRequirement requirement in requirements)
                {
                    if (requirement == null) { yield return "Null requirement."; continue; }
                    foreach (string error in requirement.ConfigErrors()) yield return error;
                    if (!ids.Add(requirement.id ?? "")) yield return "Duplicate requirement id: " + requirement.id;
                }
            }
            if (breach == null) yield return "Breach policy is required.";
            else
            {
                if (breach.graceTicks < 0 || breach.deterministicAfterTicks < 0
                || (breach.deterministicAfterTicks > 0 && breach.deterministicAfterTicks < breach.graceTicks))
                    yield return "Deterministic breach must be disabled (0) or no earlier than the nonnegative grace period.";
                if (!NonnegativeFinite(breach.riskPerDay) || !NonnegativeFinite(breach.recoveryPerDay)
                    || !NonnegativeFinite(breach.severity) || breach.severity == 0)
                    yield return "Risk/recovery rates must be finite and nonnegative; severity must be positive.";
                if (!NonnegativeFinite(breach.chancePerDayAtFullRisk) || breach.chancePerDayAtFullRisk > 1)
                    yield return "Breach chance must be within 0..1.";
            }
            if (breachEffects == null) yield return "breachEffects must not be null.";
            else foreach (BreachEffect effect in breachEffects)
                if (effect == null) yield return "Null breach effect.";
        }

        private static bool NonnegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        }
    }
}
