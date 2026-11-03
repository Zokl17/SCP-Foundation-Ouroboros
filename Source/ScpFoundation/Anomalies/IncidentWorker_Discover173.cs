using System.Linq;
using RimWorld;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover173 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_173Object");
            if (map == null || !map.IsPlayerHome || thingDef == null
                || !Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                || map.mapPawns.FreeColonistsSpawnedCount < 3
                || map.listerThings.ThingsOfDef(thingDef).Any()) return false;
            var anomaly = thingDef.GetCompProperties<CompProperties_Scp>().anomaly;
            return anomaly != null && !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(anomaly)
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("173");
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamed("SCP_173Object");
            IntVec3 cell;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => ScpSpawnCells.FootprintClear(candidate, map, thingDef)
                    && map.reachability.CanReachColony(candidate), map, out cell)) return false;
            Thing thing = GenSpawn.Spawn(ThingMaker.MakeThing(thingDef), cell, map);
            thing.TryGetComp<CompScp>().NotifyDiscovered();
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("173");
            return true;
        }
    }
}
