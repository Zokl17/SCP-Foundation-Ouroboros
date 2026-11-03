using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Storytelling
{
    // Restores only the physical snapshot. Quest actors and targets are placed by each GenStep.
    internal static class ScpSavedQuestSiteLayout
    {
        public static bool ExcludeDecoration(ThingDef def)
        {
            if (def == null) return false;
            string name = def.defName;
            return name == "VoidMonolith" || name == "AncientMechDropBeacon"
                || name.StartsWith("AncientExostrider", StringComparison.Ordinal)
                || name.StartsWith("AncientWarwalker", StringComparison.Ordinal)
                || name.StartsWith("AncientMiniWarwalker", StringComparison.Ordinal)
                || name.StartsWith("AncientMegaCannon", StringComparison.Ordinal);
        }

        private const int Size = 250;
        private const int GridBytes = Size * Size * 2;
        private static readonly string[] GridFiles = {
            "Terrain.bin", "Roofs.bin", "UnderTerrain.bin", "CompressedThings.bin"
        };

        public static bool Available(string root)
        {
            return File.Exists(Path.Combine(root, "Things.csv"))
                && GridFiles.All(name => File.Exists(Path.Combine(root, name)));
        }

        public static void Restore(Map map, string root, Faction faction)
        {
            if (map.Size.x != Size || map.Size.z != Size)
                throw new InvalidOperationException("Saved SCP quest site requires a 250x250 map.");
            if (!Available(root)) throw new FileNotFoundException("Incomplete SCP quest site: " + root);
            byte[][] grids = GridFiles.Select(name => File.ReadAllBytes(Path.Combine(root, name))).ToArray();
            if (grids.Any(grid => grid.Length != GridBytes))
                throw new InvalidOperationException("Saved SCP quest site has an invalid grid size: " + root);
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            CellRect bounds = new CellRect(0, 0, Size, Size);
            foreach (Thing existing in new List<Thing>(map.listerThings.AllThings))
                if (existing.Spawned) existing.Destroy(DestroyMode.Vanish);
            foreach (IntVec3 cell in bounds.Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                TerrainDef ground = DefDatabase<TerrainDef>.GetByShortHash(ReadHash(grids[0], index));
                if (ground != null) map.terrainGrid.SetTerrain(cell, ground);
                ushort underHash = ReadHash(grids[2], index);
                GenStep_Scp882Workshop.RestoreUnderTerrain(map.terrainGrid, cell,
                    underHash == 0 ? null : DefDatabase<TerrainDef>.GetByShortHash(underHash));
                ushort roofHash = ReadHash(grids[1], index);
                map.roofGrid.SetRoof(cell, roofHash == 0 ? null : DefDatabase<RoofDef>.GetByShortHash(roofHash));
            }
            HashSet<ushort> missingHashes = new HashSet<ushort>();
            foreach (IntVec3 cell in bounds.Cells)
            {
                ushort hash = ReadHash(grids[3], window.SourceByteIndex(cell.x, cell.z));
                if (hash == 0) continue;
                ThingDef thingDef = DefDatabase<ThingDef>.GetByShortHash(hash);
                if (thingDef == null) { missingHashes.Add(hash); continue; }
                if (ExcludeDecoration(thingDef)) continue;
                GenSpawn.Spawn(ThingMaker.MakeThing(thingDef), cell, map);
            }
            if (missingHashes.Count > 0)
                Log.Warning("SCP quest site omitted unknown compressed thing hashes: "
                    + string.Join(", ", missingHashes.Select(hash => hash.ToString()).ToArray()));
            HashSet<string> failedDefs = new HashSet<string>();
            foreach (string line in File.ReadLines(Path.Combine(root, "Things.csv")))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split(',');
                if (fields.Length != 9 || failedDefs.Contains(fields[2])) continue;
                ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(fields[2]);
                if (thingDef == null) continue;
                if (ExcludeDecoration(thingDef)) continue;
                int x = int.Parse(fields[0], CultureInfo.InvariantCulture);
                int z = int.Parse(fields[1], CultureInfo.InvariantCulture);
                int rotation = fields[4].Length == 0 ? 0 : int.Parse(fields[4], CultureInfo.InvariantCulture);
                IntVec3 cell = new IntVec3(x, 0, z);
                if (!GenAdj.OccupiedRect(cell, new Rot4(rotation), thingDef.size).FullyContainedWithin(bounds))
                    continue;
                try
                {
                    ThingDef stuff = fields[3].Length == 0 ? null : DefDatabase<ThingDef>.GetNamedSilentFail(fields[3]);
                    if (thingDef.MadeFromStuff && stuff == null) stuff = GenStuff.DefaultStuffFor(thingDef);
                    Thing thing = ThingMaker.MakeThing(thingDef, stuff);
                    if (fields[5].Length != 0) thing.stackCount = int.Parse(fields[5], CultureInfo.InvariantCulture);
                    if (fields[6].Length != 0) thing.HitPoints = Math.Min(thing.MaxHitPoints,
                        int.Parse(fields[6], CultureInfo.InvariantCulture));
                    if (thing is Plant && fields[7].Length != 0)
                        ((Plant)thing).Growth = float.Parse(fields[7], CultureInfo.InvariantCulture);
                    if (thing.def.CanHaveFaction && faction != null) thing.SetFaction(faction);
                    GenSpawn.Spawn(thing, cell, map, new Rot4(rotation));
                }
                catch (Exception exception)
                {
                    if (failedDefs.Add(fields[2]))
                        Log.Warning("SCP quest site could not restore " + fields[2] + ": " + exception);
                }
            }
            Log.Message("SCP quest site layout restored from " + root);
        }

        public static IntVec3 FreeCell(Map map, IntVec3 preferred, CellRect bounds,
            ISet<IntVec3> reserved = null, float radius = 6f)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(preferred, radius, true))
                if (cell.InBounds(map) && bounds.Contains(cell) && cell.Standable(map)
                    && (reserved == null || !reserved.Contains(cell))
                    && cell.GetThingList(map).All(thing => !(thing is Pawn)
                        && thing.def.category != ThingCategory.Building)) return cell;
            throw new InvalidOperationException("No free quest cell near " + preferred);
        }

        private static ushort ReadHash(byte[] grid, int index)
        {
            return (ushort)(grid[index] | grid[index + 1] << 8);
        }
    }
}
