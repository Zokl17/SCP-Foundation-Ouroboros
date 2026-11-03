using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    // Restores the user's 354.rws map, except for loose construction supplies.
    public sealed class GenStep_Scp354Defense : GenStep
    {
        private const int SourceSize = 250;
        private const int CellCount = SourceSize * SourceSize;
        public override int SeedPart { get { return 354; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_354DefenseSite")
                throw new InvalidOperationException("SCP-354 requires its own quest site.");
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            string root = Path.Combine(def.modContentPack.RootDir, "Assets", "Scp354Defense");
            string thingsPath = Path.Combine(root, "Things.csv");
            string terrainPath = Path.Combine(root, "Terrain.bin");
            string roofsPath = Path.Combine(root, "Roofs.bin");
            string underPath = Path.Combine(root, "UnderTerrain.bin");
            string compressedPath = Path.Combine(root, "CompressedThings.bin");
            if (!File.Exists(thingsPath) || !File.Exists(terrainPath) || !File.Exists(roofsPath)
                || !File.Exists(underPath) || !File.Exists(compressedPath))
            {
                Log.Error("SCP-354 map layout files are missing: " + root);
                return;
            }
            byte[] terrain = File.ReadAllBytes(terrainPath);
            byte[] roofs = File.ReadAllBytes(roofsPath);
            byte[] underTerrain = File.ReadAllBytes(underPath);
            byte[] compressedThings = File.ReadAllBytes(compressedPath);
            if (terrain.Length != CellCount * 2 || roofs.Length != CellCount * 2
                || underTerrain.Length != CellCount * 2 || compressedThings.Length != CellCount * 2)
            {
                Log.Error("SCP-354 map terrain or roof data has an invalid size.");
                return;
            }

            Faction faction = map.Parent.Faction;
            if (faction == null || faction.def.defName != "SCP_Foundation")
            {
                Log.Error("SCP-354 defense site needs the Foundation faction.");
                return;
            }

            CellRect siteRect = new CellRect(window.TargetX, window.TargetZ, window.Width, window.Height);
            foreach (Thing existing in new List<Thing>(map.listerThings.AllThings))
                if (existing.Spawned) existing.Destroy(DestroyMode.Vanish);

            foreach (IntVec3 cell in siteRect.Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                ushort terrainHash = (ushort)(terrain[index] | terrain[index + 1] << 8);
                TerrainDef ground = DefDatabase<TerrainDef>.GetByShortHash(terrainHash);
                if (ground != null) map.terrainGrid.SetTerrain(cell, ground);
                ushort underHash = (ushort)(underTerrain[index] | underTerrain[index + 1] << 8);
                GenStep_Scp882Workshop.RestoreUnderTerrain(map.terrainGrid, cell, underHash == 0 ? null
                    : DefDatabase<TerrainDef>.GetByShortHash(underHash));
                ushort roofHash = (ushort)(roofs[index] | roofs[index + 1] << 8);
                map.roofGrid.SetRoof(cell, roofHash == 0 ? null : DefDatabase<RoofDef>.GetByShortHash(roofHash));
            }

            foreach (IntVec3 cell in siteRect.Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                ushort hash = (ushort)(compressedThings[index] | compressedThings[index + 1] << 8);
                if (hash == 0) continue;
                ThingDef savedDef = DefDatabase<ThingDef>.GetByShortHash(hash);
                if (savedDef == null) throw new InvalidOperationException("Unknown SCP-354 map thing hash: " + hash);
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
                if (thingDef == null) continue; // Inactive optional DLC.
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
                        Log.Warning("SCP-354 map could not restore " + fields[2] + ": " + exception);
                }
            }

            if (map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("SCP_354Lake")).Count != 1)
                throw new InvalidOperationException("SCP-354 map must contain one red pool.");
            SpawnGuards(map, faction, window);
            Log.Message("SCP-354 defense map imported from 354.rws into " + map.Size + ".");
        }

        private static void SpawnGuards(Map map, Faction faction, Scp882MapWindow window)
        {
            if (faction.HostileTo(Faction.OfPlayer))
            {
                Log.Warning("SCP-354 defenders cannot join while the Foundation is hostile to the player.");
                return;
            }
            List<Pawn> guards = new List<Pawn>();
            IntVec3 center = new IntVec3(window.MapX(168), 0, window.MapZ(195));
            SpawnGuardGroup(map, faction, center, guards, 3, "SCP_MTF_354ExosuitGuard", "SCP_Nu7HKCarbine");
            SpawnGuardGroup(map, faction, center, guards, 7, "SCP_354HeavyGunner", "SCP_Nu7LightMachineGun");
            SpawnGuardGroup(map, faction, center, guards, 3, "SCP_354Rifleman", "SCP_Nu7HKCarbine");
            SpawnGuardGroup(map, faction, center, guards, 2, "SCP_354EmpSpecialist", "SCP_FoundationEMPRifle");
            LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, center, 30, false), map, guards);
        }

        private static void SpawnGuardGroup(Map map, Faction faction, IntVec3 center,
            List<Pawn> guards, int count, string kindName, string weaponName)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName);
            ThingDef weapon = DefDatabase<ThingDef>.GetNamedSilentFail(weaponName);
            if (kind == null || weapon == null)
                throw new InvalidOperationException("Missing SCP-354 defender loadout: " + kindName + ", " + weaponName);
            for (int i = 0; i < count; i++)
            {
                IntVec3 spawnCell = IntVec3.Invalid;
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(center, 18f, true))
                    if (candidate.InBounds(map) && candidate.Standable(map)
                        && candidate.GetThingList(map).Find(thing => thing is Pawn) == null)
                    { spawnCell = candidate; break; }
                if (!spawnCell.IsValid)
                    throw new InvalidOperationException("No free SCP-354 defender spawn cell near " + center);
                Pawn pawn = FoundationSupportComponent.GenerateEquippedPawn(kind, faction, weapon);
                GenSpawn.Spawn(pawn, spawnCell, map);
                ColorUniform(pawn, kindName == "SCP_354HeavyGunner");
                guards.Add(pawn);
            }
        }

        internal static void RefreshExistingGuards(Map map)
        {
            if (map == null || map.Parent == null || map.Parent.def.defName != "SCP_354DefenseSite") return;
            RemoveNorthwestWreck(map);
            RemoveGeneratedDebris(map);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction == null || pawn.Faction.def.defName != "SCP_Foundation"
                    || pawn.kindDef == null || pawn.apparel == null) continue;
                string kindName = pawn.kindDef.defName;
                bool exosuit = kindName == "SCP_MTF_Nu7" || kindName == "SCP_MTF_354ExosuitGuard";
                bool heavy = kindName == "SCP_354HeavyGunner";
                bool vest = kindName == "SCP_354Rifleman" || kindName == "SCP_354EmpSpecialist";
                if (!exosuit && !heavy && !vest) continue;
                EnsureApparel(pawn, "Apparel_BasicShirt");
                EnsureApparel(pawn, exosuit ? "Apparel_Pants" : "Apparel_FlakPants");
                if (exosuit)
                {
                    EnsureApparel(pawn, "SCP_MtfExosuit");
                    EnsureApparel(pawn, "SCP_MtfExoHelmet");
                }
                else if (heavy)
                {
                    EnsureApparel(pawn, "SCP_Nu7Armor");
                    EnsureApparel(pawn, "SCP_Nu7Helmet");
                }
                else
                {
                    EnsureApparel(pawn, "SCP_FoundationAssaultVest");
                    EnsureApparel(pawn, "SCP_Nu7Helmet");
                }
                ColorUniform(pawn, heavy);
            }
        }

        internal static void RemoveGeneratedDebris(Map map)
        {
            if (map == null || map.Parent == null || map.Parent.def.defName != "SCP_354DefenseSite") return;
            string path = Path.Combine(map.Parent.def.modContentPack.RootDir, "Assets", "Scp354Defense", "Things.csv");
            if (!File.Exists(path)) return;
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            Dictionary<string, int> saved = new Dictionary<string, int>();
            foreach (string line in File.ReadLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split(',');
                if (fields.Length != 9 || !IsDebris(fields[2])) continue;
                int x, z;
                if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out z)) continue;
                string key = fields[2] + ":" + window.MapX(x) + ":" + window.MapZ(z);
                int count;
                saved.TryGetValue(key, out count);
                saved[key] = count + 1;
            }
            int removed = 0;
            foreach (Thing thing in new List<Thing>(map.listerThings.AllThings))
            {
                if (!thing.Spawned || !IsDebris(thing.def.defName)) continue;
                string key = thing.def.defName + ":" + thing.Position.x + ":" + thing.Position.z;
                int count;
                if (saved.TryGetValue(key, out count) && count > 0)
                {
                    saved[key] = count - 1;
                    continue;
                }
                thing.Destroy(DestroyMode.Vanish);
                removed++;
            }
            if (removed > 0) Log.Message("SCP-354 removed " + removed + " generated ruin debris objects.");
        }

        private static bool IsDebris(string name)
        {
            return name.StartsWith("Ancient", StringComparison.Ordinal)
                || name.StartsWith("Filth_Machine", StringComparison.Ordinal)
                || name.StartsWith("Filth_Oil", StringComparison.Ordinal)
                || name.StartsWith("Filth_Rubble", StringComparison.Ordinal)
                || name == "Filth_Trash" || name == "RubblePile" || name == "SandbagRubble"
                || name == "ShipChunk";
        }

        private static void RemoveNorthwestWreck(Map map)
        {
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            int minX = window.MapX(52), maxX = window.MapX(63);
            int minZ = window.MapZ(173), maxZ = window.MapZ(194);
            foreach (Thing thing in new List<Thing>(map.listerThings.AllThings))
            {
                if (!thing.Spawned || thing.Position.x < minX || thing.Position.x > maxX
                    || thing.Position.z < minZ || thing.Position.z > maxZ) continue;
                string name = thing.def.defName;
                if (name == "AncientExostriderLeg" || name == "AncientExostriderHead"
                    || name == "AncientExostriderRemains" || name == "AncientExostriderCannon"
                    || name == "Filth_MachineBits" || name == "ShipChunk")
                    thing.Destroy(DestroyMode.Vanish);
            }
        }

        private static void EnsureApparel(Pawn pawn, string defName)
        {
            if (pawn.apparel.WornApparel.Any(item => item.def.defName == defName)) return;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            Apparel apparel = ThingMaker.MakeThing(def, stuff) as Apparel;
            if (apparel != null) pawn.apparel.Wear(apparel);
        }

        private static void ColorUniform(Pawn pawn, bool blue)
        {
            Color fabricColor = blue ? new Color(0.31f, 0.42f, 0.57f)
                : new Color(0.55f, 0.57f, 0.61f);
            foreach (Apparel apparel in pawn.apparel.WornApparel)
                if (apparel.def.defName == "Apparel_BasicShirt" || apparel.def.defName == "Apparel_Pants"
                    || apparel.def.defName == "Apparel_FlakPants")
                    apparel.DrawColor = fabricColor;
                else if (apparel.def.defName == "SCP_FoundationAssaultVest"
                    || apparel.def.defName == "SCP_Nu7Helmet")
                    apparel.DrawColor = Color.white;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }
}
