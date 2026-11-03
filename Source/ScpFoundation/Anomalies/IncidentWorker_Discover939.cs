using System.Linq;
using RimWorld;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover939 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef creature = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_939Object");
            ThingDef third = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_939ObjectThree");
            if (map == null || !map.IsPlayerHome || creature == null || third == null
                || !Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                || map.mapPawns.FreeColonistsSpawnedCount < 2
                || map.listerThings.ThingsOfDef(creature).Any()
                || map.listerThings.ThingsOfDef(third).Any()) return false;
            CompProperties_Scp comp = creature.GetCompProperties<CompProperties_Scp>();
            return comp != null && comp.anomaly != null
                && !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(comp.anomaly)
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("939");
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("SCP_939Object");
            ThingDef third = DefDatabase<ThingDef>.GetNamed("SCP_939ObjectThree");
            IntVec3 cell, second;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => ScpSpawnCells.FootprintClear(candidate, map, definition)
                    && !candidate.Roofed(map) && map.reachability.CanReachColony(candidate),
                map, out cell)
                || !CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                    candidate => candidate.DistanceToSquared(cell) >= 25
                        && ScpSpawnCells.FootprintClear(candidate, map, third)
                        && !candidate.Roofed(map) && map.reachability.CanReachColony(candidate),
                    map, out second)) return false;
            Thing creature = GenSpawn.Spawn(ThingMaker.MakeThing(definition), cell, map);
            Thing other = GenSpawn.Spawn(ThingMaker.MakeThing(third), second, map);
            creature.TryGetComp<CompScp>().NotifyDiscovered(false);
            other.TryGetComp<CompScp>().NotifyDiscovered(false);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("939");
            SendStandardLetter(parms, creature);
            return true;
        }
    }
}
