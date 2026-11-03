using System.Linq;
using RimWorld;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover354 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef lake = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_354Lake");
            return map != null && map.IsPlayerHome && lake != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("354")
                && map.mapPawns.FreeColonistsSpawnedCount >= 4
                && map.listerThings.ThingsOfDef(lake).Count == 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef lake = DefDatabase<ThingDef>.GetNamed("SCP_354Lake");
            IntVec3 cell;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(20,
                candidate => SafeSite(candidate, map, lake) && map.reachability.CanReachColony(candidate),
                map, out cell)) return false;
            Thing pool = GenSpawn.Spawn(ThingMaker.MakeThing(lake), cell, map);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("354");
            ScpEventJournal.Add("SCP-354", pool, "SCP_354DiscoveredBody".Translate());
            SendStandardLetter(parms, pool);
            return true;
        }

        public static bool SafeSite(IntVec3 origin, Map map, ThingDef definition)
        {
            if (!ScpSpawnCells.FootprintClear(origin, map, definition)) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(origin, Rot4.North, definition.size))
                if (cell.Roofed(map) || map.zoneManager.ZoneAt(cell) != null
                    || cell.GetThingList(map).Any(thing => thing is Plant && ((Plant)thing).sown))
                    return false;
            return true;
        }
    }
}
