using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover504 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && map.IsPlayerHome
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("504")
                && map.mapPawns.FreeColonistsSpawnedCount > 0
                && DefDatabase<ThingDef>.GetNamedSilentFail("SCP_504Bush") != null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("SCP_504Bush");
            List<Thing> buildings = map.listerThings.AllThings.Where(thing =>
                thing.def.category == ThingCategory.Building && thing.Faction == Faction.OfPlayer).ToList();
            IntVec3 center = buildings.Count > 0
                ? new IntVec3((int)buildings.Average(thing => thing.Position.x), 0,
                    (int)buildings.Average(thing => thing.Position.z))
                : map.mapPawns.FreeColonistsSpawned.First().Position;
            List<IntVec3> nearby = GenRadial.RadialCellsAround(center, 30f, true).Where(candidate =>
                candidate.DistanceToSquared(center) >= 100
                && ScpSpawnCells.FootprintClear(candidate, map, definition)
                && !candidate.Roofed(map) && map.zoneManager.ZoneAt(candidate) == null
                && map.reachability.CanReachColony(candidate)).ToList();
            IntVec3 cell;
            if (nearby.Count > 0) cell = nearby.RandomElement();
            else if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => ScpSpawnCells.FootprintClear(candidate, map, definition)
                    && map.reachability.CanReachColony(candidate), map, out cell)) return false;
            Thing bush = GenSpawn.Spawn(ThingMaker.MakeThing(definition), cell, map);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("504");
            bush.TryGetComp<ScpFoundation.Runtime.CompScp>().NotifyDiscovered(false);
            SendStandardLetter(parms, bush);
            return true;
        }
    }
}
