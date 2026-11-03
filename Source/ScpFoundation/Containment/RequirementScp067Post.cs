using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementScp067Post : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context)
        {
            return context.thing.Spawned && CompScpPost.IsObjectAtPost(context.thing);
        }

        public override string Description() { return "SCP_067PostRequirement".Translate(); }
    }
}
