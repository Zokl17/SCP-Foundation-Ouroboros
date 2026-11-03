using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    // The physical layout was exported from the player's 330.rws save.
    public sealed class GenStep_Scp330Village : GenStep
    {
        private const int Width = 250;
        private const int Height = 250;
        private const int CellCount = Width * Height;
        public override int SeedPart { get { return 330; } }

        private static readonly int[,] VillagerSpots = {
            { 96, 168 }, { 100, 171 }, { 108, 169 }, { 113, 173 }, { 98, 180 },
            { 104, 184 }, { 111, 181 }, { 117, 176 }, { 93, 176 }, { 107, 190 }
        };

        public override void Generate(Map map, GenStepParams parms)
        {
            // This replaces an entire quest map. Never run against a colony map.
            if (map.Parent == null || map.Parent.def.defName != "SCP_330VillageSite")
                throw new InvalidOperationException("SCP-330 requires its own quest site; actual parent: "
                    + (map.Parent == null ? "null" : map.Parent.def.defName));
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);

            string root = Path.Combine(def.modContentPack.RootDir, "Assets", "Scp330Village");
            string thingsPath = Path.Combine(root, "Things.csv");
            string terrainPath = Path.Combine(root, "Terrain.bin");
            string roofsPath = Path.Combine(root, "Roofs.bin");
            if (!File.Exists(thingsPath) || !File.Exists(terrainPath) || !File.Exists(roofsPath))
            {
                Log.Error("SCP-330 village layout files are missing: " + root);
                return;
            }

            byte[] terrain = File.ReadAllBytes(terrainPath);
            byte[] roofs = File.ReadAllBytes(roofsPath);
            byte[] underTerrain = File.ReadAllBytes(Path.Combine(root, "UnderTerrain.bin"));
            byte[] compressedThings = File.ReadAllBytes(Path.Combine(root, "CompressedThings.bin"));
            if (terrain.Length != CellCount * 2 || roofs.Length != CellCount * 2
                || underTerrain.Length != CellCount * 2 || compressedThings.Length != CellCount * 2)
            {
                Log.Error("SCP-330 village terrain or roof data has an invalid size.");
                return;
            }

            Faction faction = Faction.OfAncients;
            if (faction == null || faction.HostileTo(Faction.OfPlayer))
            {
                Log.Error("SCP-330 village needs a neutral ancient faction.");
                return;
            }

            CellRect siteRect = new CellRect(window.TargetX, window.TargetZ, window.Width, window.Height);
            foreach (Thing existing in new List<Thing>(map.listerThings.AllThings))
                if (existing.Spawned)
                    existing.Destroy(DestroyMode.Vanish);

            foreach (IntVec3 cell in siteRect.Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                ushort terrainHash = (ushort)(terrain[index] | terrain[index + 1] << 8);
                TerrainDef ground = DefDatabase<TerrainDef>.GetByShortHash(terrainHash);
                if (ground != null) map.terrainGrid.SetTerrain(cell, ground);
                ushort underHash = (ushort)(underTerrain[index] | underTerrain[index + 1] << 8);
                RestoreUnderTerrain(map.terrainGrid, cell, underHash == 0 ? null
                    : DefDatabase<TerrainDef>.GetByShortHash(underHash));

                ushort roofHash = (ushort)(roofs[index] | roofs[index + 1] << 8);
                RoofDef roof = roofHash == 0 ? null : DefDatabase<RoofDef>.GetByShortHash(roofHash);
                map.roofGrid.SetRoof(cell, roof);
            }

            // Vanilla stores natural rocks and other compressible things separately.
            foreach (IntVec3 cell in siteRect.Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                ushort hash = (ushort)(compressedThings[index] | compressedThings[index + 1] << 8);
                if (hash == 0) continue;
                ThingDef savedDef = DefDatabase<ThingDef>.GetByShortHash(hash);
                if (savedDef == null)
                    throw new InvalidOperationException("Unknown SCP-330 map thing hash: " + hash);
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
                if (thingDef == null) continue; // Content from an inactive DLC.
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
                        Log.Warning("SCP-330 map could not restore " + fields[2] + ": " + exception);
                }
            }

            ThingDef bowlDef = DefDatabase<ThingDef>.GetNamed("SCP_330Bowl");
            ThingDef relicDef = DefDatabase<ThingDef>.GetNamed("Reliquary");
            Thing relic = map.listerThings.ThingsOfDef(relicDef).SingleOrDefault();
            CompThingContainer container = relic == null ? null : relic.TryGetComp<CompThingContainer>();
            if (container == null || !container.GetDirectlyHeldThings().TryAdd(ThingMaker.MakeThing(bowlDef), true))
                throw new InvalidOperationException("SCP-330 village reliquary could not hold the bowl.");
            SpawnVillagers(map, faction, window);
            Log.Message("SCP-330 snapshot imported into " + map.Size + "; source corner ("
                + window.SourceX + ", " + window.SourceZ + "), size " + window.Width + "x" + window.Height + ".");
        }

        internal static void RestoreUnderTerrain(TerrainGrid grid, IntVec3 cell, TerrainDef savedTerrain)
        {
            // SetTerrain clears the underlying layer for natural ground. An empty
            // saved layer needs no setter call: SetUnderTerrain dereferences its argument.
            if (savedTerrain != null) grid.SetUnderTerrain(cell, savedTerrain);
        }

        private static void SpawnVillagers(Map map, Faction faction, Scp882MapWindow window)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("Villager");
            HediffDef woodenHand = DefDatabase<HediffDef>.GetNamed("WoodenHand");
            string[] candy = { "SCP_330CandyYellowEffect", "SCP_330CandyGreenEffect",
                "SCP_330CandyRedEffect", "SCP_330CandyPurpleEffect", "SCP_330CandyBlackEffect" };
            List<Pawn> villagers = new List<Pawn>();
            for (int i = 0; i < VillagerSpots.GetLength(0); i++)
            {
                IntVec3 preferred = new IntVec3(window.MapX(VillagerSpots[i, 0]), 0, window.MapZ(VillagerSpots[i, 1]));
                if (!preferred.InBounds(map)) continue;
                IntVec3 spawnCell = IntVec3.Invalid;
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(preferred, 6f, true))
                    if (candidate.InBounds(map) && candidate.Standable(map)
                        && !candidate.GetThingList(map).Any(thing => thing is Pawn))
                    { spawnCell = candidate; break; }
                if (!spawnCell.IsValid) continue;
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                if (i == 0 || i == 3 || i == 7)
                {
                    foreach (BodyPartRecord hand in pawn.RaceProps.body.AllParts
                        .Where(part => part.def.defName == "Hand"))
                    {
                        if (pawn.health.hediffSet.PartIsMissing(hand.parent))
                            pawn.health.RestorePart(hand.parent, null);
                        if (pawn.health.hediffSet.PartIsMissing(hand))
                            pawn.health.RestorePart(hand, null);
                        pawn.health.AddHediff(woodenHand, hand);
                    }
                }
                HediffDef effect = DefDatabase<HediffDef>.GetNamed(candy[i % candy.Length]);
                pawn.health.AddHediff(effect);
                GenSpawn.Spawn(pawn, spawnCell, map);
                villagers.Add(pawn);
            }
            if (villagers.Count > 0)
                LordMaker.MakeNewLord(faction,
                    new LordJob_DefendBase(faction,
                        new IntVec3(window.MapX(105), 0, window.MapZ(175)), 40, false),
                    map, villagers);
        }
    }
}
