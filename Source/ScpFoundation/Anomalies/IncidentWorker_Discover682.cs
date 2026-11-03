using System.Linq;
using RimWorld;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover682 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef creature = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_682Object");
            if (map == null || !map.IsPlayerHome || creature == null
                || !Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                || map.mapPawns.FreeColonistsSpawnedCount < 4
                || map.listerThings.ThingsOfDef(creature).Any()) return false;
            CompProperties_Scp comp = creature.GetCompProperties<CompProperties_Scp>();
            return comp != null && comp.anomaly != null
                && !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(comp.anomaly)
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("682");
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("SCP_682Object");
            IntVec3 cell;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => ScpSpawnCells.FootprintClear(candidate, map, definition)
                    && !candidate.Roofed(map) && map.reachability.CanReachColony(candidate),
                map, out cell)) return false;
            Thing creature = GenSpawn.Spawn(ThingMaker.MakeThing(definition), cell, map);
            creature.TryGetComp<CompScp>().NotifyDiscovered(false);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("682");
            SendStandardLetter(parms, creature);
            return true;
        }
    }
}
