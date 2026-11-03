using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover650 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            var anomaly = ScpInteractionDefOf.SCP_Statue.GetCompProperties<CompProperties_Scp>().anomaly;
            return map != null && map.IsPlayerHome && map.mapPawns.AnyFreeColonistSpawned
                && CompFoundationAccessTerminal.Online(map) != null
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("650")
                && !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(anomaly);
        }
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            IntVec3 cell;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => candidate.Standable(map) && !candidate.Fogged(map) && candidate.GetThingList(map).Count == 0
                    && map.reachability.CanReachColony(candidate), map, out cell)) return false;
            Thing thing = ThingMaker.MakeThing(ScpInteractionDefOf.SCP_Statue);
            GenSpawn.Spawn(thing, cell, map);
            CompScp comp = thing.TryGetComp<CompScp>();
            comp.NotifyDiscovered(false);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("650");
            SendStandardLetter(parms, thing);
            return true;
        }
    }
}
