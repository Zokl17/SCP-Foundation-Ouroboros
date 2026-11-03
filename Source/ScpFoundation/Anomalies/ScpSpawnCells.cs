using System.Linq;
using Verse;

namespace ScpFoundation.Anomalies
{
    public static class ScpSpawnCells
    {
        public static bool FootprintClear(IntVec3 origin, Map map, ThingDef definition)
        {
            if (map == null || definition == null) return false;
            CellRect footprint = GenAdj.OccupiedRect(origin, Rot4.North, definition.size);
            foreach (IntVec3 cell in footprint)
                if (!cell.InBounds(map) || cell.Fogged(map) || !cell.Standable(map)
                    || cell.GetThingList(map).Any(t => t is Pawn || t.def.category == ThingCategory.Building
                        || t.def.category == ThingCategory.Item)) return false;
            return true;
        }
    }
}
