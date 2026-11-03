using System.Collections.Generic;
using System.Linq;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Storytelling
{
    public static class ScpOperationTargeting
    {
        public static IEnumerable<CompScp> ContainedTargets(Map map)
        {
            if (map == null || !map.IsPlayerHome) return Enumerable.Empty<CompScp>();
            return map.GetComponent<ScpStoryStateComponent>().Anomalies.Where(scp =>
                scp != null && scp.Definition != null && scp.Definition.source != null
                && !scp.Definition.source.originalFixture && scp.parent.Spawned && !scp.parent.Destroyed
                && scp.parent.Map == map && scp.IsInCustody);
        }
    }
}
