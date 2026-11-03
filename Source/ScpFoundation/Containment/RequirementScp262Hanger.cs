using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementScp262Hanger : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context)
        {
            CompScpPost post = CompScpPost.ForObject(context.thing);
            return post != null && post.parent.def.defName == "SCP_262Hanger"
                && CompScpPost.IsObjectAtPost(context.thing);
        }

        public override string Description() { return "SCP_262HangerRequirement".Translate(); }
    }
}
