using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementScp294 : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context) { return true; }
        public override string Description() { return "SCP_294NoSpecialContainment".Translate(); }
    }
}
