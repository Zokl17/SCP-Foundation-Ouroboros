using ScpFoundation.Anomalies;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementAtPost : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context)
        {
            return context.thing.Spawned && (CompScpPost.IsObjectAtPost(context.thing)
                || CompScp093Mirror.IsDiscAtActiveMirror(context.thing));
        }

        public override string Description() { return "SCP_GenericPostRequirement".Translate(); }
    }
}
