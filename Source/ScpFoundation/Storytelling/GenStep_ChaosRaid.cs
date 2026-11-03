using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    // Rebuilds the player's Chaos raid save as a separate hostile quest map.
    public sealed class GenStep_ChaosRaid : GenStep
    {
        private const int SourceSize = 250;
        private const int CellCount = SourceSize * SourceSize;
        public override int SeedPart { get { return 127500; } }

        // The first eight are exosuit PKP gunners; the remaining twelve carry AKs.
        private static readonly int[,] GuardSpots = {
            { 159, 103 }, { 162, 103 }, { 165, 103 }, { 159, 109 }, { 168, 113 },
            { 174, 113 }, { 181, 109 }, { 181, 99 },
            { 150, 106 }, { 153, 116 }, { 161, 116 }, { 169, 119 }, { 177, 118 },
            { 184, 114 }, { 190, 105 }, { 188, 98 }, { 182, 90 }, { 173, 89 },
            { 163, 89 }, { 154, 91 }
        };

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_ChaosRaidSite")
                throw new InvalidOperationException("Chaos raid snapshot requires its own quest site.");
            Faction faction = map.Parent.Faction;
            if (faction == null || faction.def.defName != "SCP_ChaosInsurgency")
                throw new InvalidOperationException("Chaos raid site must belong to the Chaos Insurgency.");

            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            CellRect siteRect = new CellRect(window.TargetX, window.TargetZ, window.Width, window.Height);
            string root = Path.Combine(def.modContentPack.RootDir, "Assets", "ChaosRaid");
            string thingsPath = Path.Combine(root, "Things.csv");
            string terrainPath = Path.Combine(root, "Terrain.bin");
            string roofsPath = Path.Combine(root, "Roofs.bin");
            string underPath = Path.Combine(root, "UnderTerrain.bin");
            string compressedPath = Path.Combine(root, "CompressedThings.bin");
            if (!File.Exists(thingsPath) || !File.Exists(terrainPath) || !File.Exists(roofsPath)
                || !File.Exists(underPath) || !File.Exists(compressedPath))
                throw new InvalidOperationException("Chaos raid map assets are missing: " + root);
            byte[] terrain = File.ReadAllBytes(terrainPath);
            byte[] roofs = File.ReadAllBytes(roofsPath);
            byte[] under = File.ReadAllBytes(underPath);
            byte[] compressed = File.ReadAllBytes(compressedPath);
            if (terrain.Length != CellCount * 2 || roofs.Length != CellCount * 2
                || under.Length != CellCount * 2 || compressed.Length != CellCount * 2)
                throw new InvalidOperationException("Chaos raid map grid has an invalid size.");

            foreach (Thing existing in new List<Thing>(map.listerThings.AllThings))
                if (existing.Spawned) existing.Destroy(DestroyMode.Vanish);
            foreach (IntVec3 cell in siteRect.Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                TerrainDef ground = DefDatabase<TerrainDef>.GetByShortHash((ushort)(terrain[index] | terrain[index + 1] << 8));
                if (ground != null) map.terrainGrid.SetTerrain(cell, ground);
                ushort underHash = (ushort)(under[index] | under[index + 1] << 8);
                GenStep_Scp882Workshop.RestoreUnderTerrain(map.terrainGrid, cell, underHash == 0 ? null
                    : DefDatabase<TerrainDef>.GetByShortHash(underHash));
                ushort roofHash = (ushort)(roofs[index] | roofs[index + 1] << 8);
                map.roofGrid.SetRoof(cell, roofHash == 0 ? null : DefDatabase<RoofDef>.GetByShortHash(roofHash));
            }
            foreach (IntVec3 cell in siteRect.Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                ushort hash = (ushort)(compressed[index] | compressed[index + 1] << 8);
                if (hash == 0) continue;
                ThingDef savedDef = DefDatabase<ThingDef>.GetByShortHash(hash);
                if (savedDef == null) throw new InvalidOperationException("Unknown Chaos raid map thing hash: " + hash);
                if (ScpSavedQuestSiteLayout.ExcludeDecoration(savedDef)) continue;
                GenSpawn.Spawn(ThingMaker.MakeThing(savedDef), cell, map);
            }

            HashSet<string> failedDefs = new HashSet<string>();
            foreach (string line in File.ReadLines(thingsPath))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split(',');
                if (fields.Length != 9 || failedDefs.Contains(fields[2])) continue;
                ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(fields[2]);
                if (thingDef == null) continue; // Optional inactive DLC.
                if (ScpSavedQuestSiteLayout.ExcludeDecoration(thingDef)) continue;
                int x = int.Parse(fields[0], CultureInfo.InvariantCulture);
                int z = int.Parse(fields[1], CultureInfo.InvariantCulture);
                int rotation = fields[4].Length == 0 ? 0 : int.Parse(fields[4], CultureInfo.InvariantCulture);
                IntVec3 cell = new IntVec3(window.MapX(x), 0, window.MapZ(z));
                if (!GenAdj.OccupiedRect(cell, new Rot4(rotation), thingDef.size).FullyContainedWithin(siteRect))
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
                    if (thing.def.CanHaveFaction) thing.SetFaction(faction);
                    GenSpawn.Spawn(thing, cell, map, new Rot4(rotation));
                }
                catch (Exception exception)
                {
                    if (failedDefs.Add(fields[2]))
                        Log.Warning("Chaos raid map could not restore " + fields[2] + ": " + exception);
                }
            }

            ThingDef gun = DefDatabase<ThingDef>.GetNamed("SCP_127Gun");
            if (map.listerThings.ThingsOfDef(gun).Count != 1)
                throw new InvalidOperationException("Chaos raid map must retain the saved SCP-127.");
            SpawnCacheMedicine(map, window);
            SpawnGuards(map, faction, window);
            Log.Message("Chaos raid map imported from the player's save with 20 defenders.");
        }

        private static void SpawnCacheMedicine(Map map, Scp882MapWindow window)
        {
            SpawnStack(map, window, "SCP_500Pill", 10, 163, 103);
            SpawnStack(map, window, "SCP_500Pill", 10, 164, 103);
            SpawnStack(map, window, "SCP_ClassAAmnestic", 6, 163, 104);
            SpawnStack(map, window, "SCP_MnesticStabilizer", 6, 164, 104);
            SpawnStack(map, window, "SCP_HemostaticInjector", 6, 165, 104);
        }

        private static void SpawnStack(Map map, Scp882MapWindow window, string defName, int count, int x, int z)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            IntVec3 cell = new IntVec3(window.MapX(x), 0, window.MapZ(z));
            if (!cell.InBounds(map) || !cell.Standable(map))
                throw new InvalidOperationException("Chaos cache cell is blocked: " + cell);
            Thing stack = ThingMaker.MakeThing(def);
            stack.stackCount = count;
            GenSpawn.Spawn(stack, cell, map);
        }

        private static void SpawnGuards(Map map, Faction faction, Scp882MapWindow window)
        {
            PawnKindDef heavy = DefDatabase<PawnKindDef>.GetNamed("SCP_ChaosHeavy");
            PawnKindDef rifleman = DefDatabase<PawnKindDef>.GetNamed("SCP_ChaosInsurgent");
            ThingDef machineGun = DefDatabase<ThingDef>.GetNamed("SCP_ChaosLightMachineGun");
            ThingDef rifle = DefDatabase<ThingDef>.GetNamed("SCP_ChaosAKGrenadier");
            List<Pawn> guards = new List<Pawn>();
            for (int i = 0; i < GuardSpots.GetLength(0); i++)
            {
                bool isHeavy = i < 8;
                IntVec3 preferred = new IntVec3(window.MapX(GuardSpots[i, 0]), 0, window.MapZ(GuardSpots[i, 1]));
                IntVec3 spawnCell = IntVec3.Invalid;
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(preferred, 8f, true))
                    if (candidate.InBounds(map) && candidate.Standable(map)
                        && (candidate.x < window.MapX(156) || candidate.x > window.MapX(166)
                            || candidate.z < window.MapZ(100) || candidate.z > window.MapZ(106))
                        && candidate.GetThingList(map).Find(thing => thing is Pawn) == null)
                    { spawnCell = candidate; break; }
                if (!spawnCell.IsValid)
                    throw new InvalidOperationException("No free Chaos guard cell near " + preferred);
                Pawn pawn = FoundationSupportComponent.GenerateEquippedPawn(
                    isHeavy ? heavy : rifleman, faction, isHeavy ? machineGun : rifle);
                GenSpawn.Spawn(pawn, spawnCell, map);
                guards.Add(pawn);
            }
            IntVec3 center = new IntVec3(window.MapX(167), 0, window.MapZ(105));
            LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, center, 65, false), map, guards);
        }
    }
}
