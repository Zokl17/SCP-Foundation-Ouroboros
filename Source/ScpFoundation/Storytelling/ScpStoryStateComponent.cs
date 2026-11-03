using System.Collections.Generic;
using ScpFoundation.Containment;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Storytelling
{
    // Shared map registry for SCP work, containment, shifts and the terminal.
    public sealed class ScpStoryStateComponent : MapComponent
    {
        private readonly HashSet<CompScp> anomalies = new HashSet<CompScp>();

        public ScpStoryStateComponent(Map map) : base(map) { }
        public void Register(CompScp comp) { anomalies.Add(comp); }
        public void Unregister(CompScp comp) { anomalies.Remove(comp); }
        public IEnumerable<CompScp> Anomalies { get { return anomalies; } }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 == 0) ScpDutyDispatcher.Dispatch(map);
            if (Find.TickManager.TicksGame % 250 != 0) return;
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            foreach (ScpFoundation.Definitions.ScpDef article in ScpArticleCatalog.Articles)
                if (!knowledge.HasEncountered(article) && ScpArticleCatalog.Representative(map, article) != null)
                    knowledge.RecordEncounter(article);
        }
    }
}
