using System.Linq;
using RimWorld;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Scp999 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef slime = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_999Object");
            return map != null && map.IsPlayerHome && slime != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && map.mapPawns.FreeColonistsSpawnedCount > 0
                && map.mapPawns.FreeColonistsSpawned.Any(pawn => pawn.needs != null
                    && pawn.needs.mood != null && pawn.needs.mood.CurLevelPercentage < 0.3f)
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("999")
                && map.listerThings.ThingsOfDef(slime).Count == 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef slime = DefDatabase<ThingDef>.GetNamed("SCP_999Object");
            Pawn distressed = map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn.needs != null && pawn.needs.mood != null
                    && pawn.needs.mood.CurLevelPercentage < 0.3f)
                .OrderBy(pawn => pawn.needs.mood.CurLevelPercentage).First();
            IntVec3 cell = GenRadial.RadialCellsAround(distressed.Position, 8f, true)
                .Where(candidate => candidate.DistanceToSquared(distressed.Position) >= 4
                    && candidate.GetRoom(map) == distressed.Position.GetRoom(map)
                    && ScpSpawnCells.FootprintClear(candidate, map, slime))
                .OrderBy(candidate => candidate.DistanceToSquared(distressed.Position))
                .DefaultIfEmpty(IntVec3.Invalid).First();
            if (!cell.IsValid && !CellFinder.TryFindRandomEdgeCellWith(
                candidate => ScpSpawnCells.FootprintClear(candidate, map, slime), map, 0f, out cell))
                return false;
            Thing creature = GenSpawn.Spawn(ThingMaker.MakeThing(slime), cell, map);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("999");
            SendStandardLetter(parms, creature);
            return true;
        }
    }
}
