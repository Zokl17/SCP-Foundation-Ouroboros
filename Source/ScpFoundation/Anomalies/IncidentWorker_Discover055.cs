using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover055 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && map.IsPlayerHome
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && Current.Game.GetComponent<FoundationAssignmentComponent>().progress.stage == FoundationAssignmentStage.Completed
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("055")
                && Scp055Delivery.ReadyPost(map) != null
                && !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(ScpInteractionDefOf.SCP_Anomaly055);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            return TrySpawn((Map)parms.target);
        }

        public bool TryExecuteDebug(Map map)
        {
            if (map == null || !map.IsPlayerHome
                || map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCP_AntimemeticObject).Count > 0) return false;
            return TrySpawn(map);
        }

        private static bool TrySpawn(Map map)
        {
            return Scp055Delivery.Deliver(map);
        }
    }
}
