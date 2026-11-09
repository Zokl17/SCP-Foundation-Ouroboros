using ScpFoundation.Definitions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Anomalies
{
    internal static class ScpOperationalClearance
    {
        internal static bool Allows(Thing thing)
        {
            CompScp anomaly = thing == null ? null : thing.TryGetComp<CompScp>();
            return anomaly != null && Allows(anomaly.Definition);
        }

        internal static bool Allows(ScpDef article)
        {
            ScpKnowledgeComponent knowledge = Current.Game == null ? null
                : Current.Game.GetComponent<ScpKnowledgeComponent>();
            return article != null && knowledge != null
                && knowledge.LevelFor(article) >= KnowledgeLevel.PartiallyDocumented;
        }
    }
}
