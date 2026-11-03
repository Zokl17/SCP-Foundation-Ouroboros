using System;
using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Knowledge;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public enum Scp093WorldVariant { Blue, Green, Violet, Yellow, Red }

    public sealed class CompProperties_Scp093Mirror : CompProperties
    {
        public CompProperties_Scp093Mirror() { compClass = typeof(CompScp093Mirror); }
    }

    public sealed class CompScp093Mirror : ThingComp
    {
        private bool discAttached;
        private Thing attachedDisc;
        private Pawn visitor;
        private bool checkedExistingPocket;
        private Scp093WorldVariant worldVariant = Scp093WorldVariant.Red;

        public bool DiscAttached { get { return discAttached; } }
        public Pawn Visitor { get { return visitor; } }
        public Scp093WorldVariant WorldVariant { get { return worldVariant; } }

        public static bool IsDiscAtActiveMirror(Thing disc)
        {
            if (disc == null || !disc.Spawned || disc.def.defName != "SCP_093Disc") return false;
            foreach (Thing thing in disc.Position.GetThingList(disc.Map))
            {
                CompScp093Mirror mirror = thing.TryGetComp<CompScp093Mirror>();
                if (mirror != null && mirror.discAttached && mirror.attachedDisc == disc)
                    return true;
            }
            return false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref discAttached, "scp093DiscAttached", false);
            Scribe_References.Look(ref attachedDisc, "scp093AttachedDisc");
            Scribe_References.Look(ref visitor, "scp093Visitor");
            Scribe_Values.Look(ref worldVariant, "scp093WorldVariant", Scp093WorldVariant.Red);
        }

        public override void CompTick()
        {
            if (checkedExistingPocket) return;
            checkedExistingPocket = true;
            MapPortal portal = parent as MapPortal;
            if (portal != null && portal.PocketMapExists && Current.Game != null)
            {
                ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                if (knowledge != null) knowledge.ClaimScp093FirstMapLoot((int)worldVariant);
            }
        }

        public override string CompInspectStringExtra()
        {
            return (discAttached ? "SCP_093MirrorVisitActive"
                : "SCP_093MirrorApplyDisc").Translate();
        }

        public override AcceptanceReport CanEnterPortal()
        {
            // The native portal action allows groups. SCP-093 is entered only by
            // the colonist who applies the disc with the dedicated job below.
            return "SCP_093MirrorApplyDisc".Translate();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            MapPortal portal = parent as MapPortal;
            if (portal != null && parent.Spawned && (discAttached || portal.PocketMapExists))
                yield return new Command_Action {
                    defaultLabel = "SCP_093DetachDisc".Translate(),
                    defaultDesc = "SCP_093DetachDiscDesc".Translate(),
                    icon = parent.def.uiIcon,
                    action = RequestClose
                };
        }

        public bool CanApply(Thing disc, Pawn pawn)
        {
            if (discAttached || !parent.Spawned || !parent.Map.IsPlayerHome
                || ((MapPortal)parent).PocketMapExists || disc == null
                || !disc.Spawned || disc.Map != parent.Map || pawn == null || !pawn.Spawned
                || pawn.Map != parent.Map || pawn.Dead || pawn.Downed || pawn.Drafted
                || !pawn.IsColonistPlayerControlled || pawn.jobs == null || pawn.carryTracker == null
                || pawn.health == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return false;
            Room room = parent.Position.GetRoom(parent.Map);
            return room != null && !room.TouchesMapEdge && !room.IsDoorway
                && disc.Position.GetRoom(parent.Map) == room
                && pawn.CanReserveAndReach(disc, Verse.AI.PathEndMode.Touch, Danger.Some)
                && pawn.CanReserveAndReach(parent, Verse.AI.PathEndMode.Touch, Danger.Some);
        }

        public void BeginVisit(Thing disc, Pawn pawn)
        {
            discAttached = true;
            attachedDisc = disc;
            visitor = pawn;
            int age = pawn.ageTracker.AgeBiologicalYears;
            worldVariant = age < 20 ? Scp093WorldVariant.Blue
                : age < 40 ? Scp093WorldVariant.Green
                : age < 60 ? Scp093WorldVariant.Violet
                : age < 80 ? Scp093WorldVariant.Yellow : Scp093WorldVariant.Red;
        }

        public void AbortVisit()
        {
            discAttached = false;
            attachedDisc = null;
            visitor = null;
        }

        public void NotifyEntered()
        {
            Find.LetterStack.ReceiveLetter("SCP_093EnteredTitle".Translate(),
                ("SCP_093Entered" + worldVariant + "Body").Translate(),
                LetterDefOf.NeutralEvent, parent);
        }

        public void RequestClose()
        {
            MapPortal portal = parent as MapPortal;
            if (portal == null || !parent.Spawned) return;
            Map pocket = portal.PocketMap;
            if (pocket != null && pocket.mapPawns.PawnsInFaction(Faction.OfPlayer).Count > 0)
            {
                Messages.Message("SCP_093CannotCloseWhileInside".Translate(), parent,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pocket == null) { CloseNow(); return; }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "SCP_093CloseWarning".Translate(), CloseNow));
        }

        private void CloseNow()
        {
            MapPortal portal = parent as MapPortal;
            if (portal == null || !parent.Spawned) return;
            Map pocket = portal.PocketMap;
            if (pocket != null)
            {
                if (pocket.mapPawns.PawnsInFaction(Faction.OfPlayer).Count > 0) return;
                PocketMapUtility.DestroyPocketMap(pocket);
                portal.exit = null;
            }
            if (attachedDisc != null && attachedDisc.Spawned && attachedDisc.Map == parent.Map
                && attachedDisc.Position == parent.Position)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 1.5f, false))
                {
                    if (cell == parent.Position || !cell.InBounds(parent.Map) || !cell.Standable(parent.Map)
                        || cell.GetRoom(parent.Map) != parent.Position.GetRoom(parent.Map)
                        || cell.GetThingList(parent.Map).Count != 0) continue;
                    attachedDisc.DeSpawn();
                    GenSpawn.Spawn(attachedDisc, cell, parent.Map);
                    break;
                }
            }
            discAttached = false;
            attachedDisc = null;
            visitor = null;
            Messages.Message("SCP_093Closed".Translate(), parent,
                MessageTypeDefOf.PositiveEvent, false);
        }
    }

    // MapPortal and PocketMapExit perform pawn transfer and retain the return link in saves.
    // Only the destination's scenery is specific to SCP-093.
    public sealed class GenStep_Scp093RedWorld : GenStep
    {
        public override int SeedPart { get { return 93; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (PocketMapUtility.currentlyGeneratingPortal == null)
                throw new InvalidOperationException("SCP-093 pocket map requires a portal entrance.");
            CompScp093Mirror mirror = PocketMapUtility.currentlyGeneratingPortal.TryGetComp<CompScp093Mirror>();
            Scp093WorldVariant variant = mirror == null ? Scp093WorldVariant.Red : mirror.WorldVariant;
            if (variant != Scp093WorldVariant.Violet)
            {
                GenerateVariant(map, variant);
                return;
            }
            TerrainDef dust = DefDatabase<TerrainDef>.GetNamed("SCP_093VioletGround");
            TerrainDef concrete = DefDatabase<TerrainDef>.GetNamed("AncientConcrete");
            TerrainDef tile = DefDatabase<TerrainDef>.GetNamed("PavedTile");
            int x = map.Size.x / 2;
            int z = map.Size.z / 2;
            foreach (IntVec3 cell in map.AllCells)
                map.terrainGrid.SetTerrain(cell, dust);

            // A readable route: safe arrival plaza, broken main street, two side ruins,
            // then the archive at the far end. The return mirror is never obstructed.
            Paint(map, x - 8, z - 29, x + 8, z - 16, concrete);
            Paint(map, x - 3, z - 16, x + 3, z + 16, concrete);
            Paint(map, x - 23, z - 2, x + 23, z + 2, concrete);
            Paint(map, x - 23, z - 8, x - 8, z + 9, concrete);
            Paint(map, x + 8, z - 8, x + 23, z + 9, concrete);
            Paint(map, x - 11, z + 14, x + 11, z + 29, concrete);
            Paint(map, x - 2, z - 29, x + 2, z - 18, tile);
            Paint(map, x - 1, z + 14, x + 1, z + 27, tile);
            for (int roadZ = z - 12; roadZ < z + 13; roadZ += 5)
                map.terrainGrid.SetTerrain(new IntVec3(x, 0, roadZ), dust);

            // Missing wall sections act as entrances and let the player flank the
            // sentry on the return trip instead of fighting in a sealed corridor.
            RuinWalls(map, x - 23, z - 8, x - 8, z + 9, x - 8, z, x - 18, z - 8);
            RuinWalls(map, x + 8, z - 8, x + 23, z + 9, x + 8, z, x + 18, z + 9);
            RuinWalls(map, x - 11, z + 14, x + 11, z + 29, x, z + 14, x - 7, z + 29);
            for (int p = -2; p <= 2; p += 4)
            {
                Wall(map, x - 7, z - 23 + p);
                Wall(map, x + 7, z - 23 + p);
            }

            // Ordinary Core wrecks and practical salvage, with no DLC dependency.
            Place(map, "AncientRustedTruck", x - 10, z - 16);
            Place(map, "AncientRustedCar", x + 8, z - 16);
            Place(map, "AncientRustedCarFrame", x - 10, z + 10);
            Place(map, "AncientLamp", x - 5, z - 23);
            Place(map, "AncientLamp", x + 5, z - 23);
            Place(map, "AncientLamp", x + 5, z - 3);
            Place(map, "AncientLamp", x - 5, z + 12);
            Place(map, "AncientLamp", x - 7, z + 23);
            Place(map, "AncientSystemRack", x - 7, z + 19);
            Place(map, "ChunkSlagSteel", x - 18, z - 5);
            Place(map, "ChunkSlagSteel", x + 17, z + 5);
            // Abandoned rooms need more than their quest loot to look lived in.
            // Keep the main street, archive console and return mirror clear.
            Place(map, "AncientBarrel", x - 20, z - 4);
            Place(map, "AncientSmallCrate", x - 14, z - 5);
            Place(map, "AncientLongCrate", x - 20, z + 5);
            Place(map, "AncientBarrel", x - 11, z + 6);
            Place(map, "AncientSmallCrate", x - 16, z + 7);
            Place(map, "AncientBarrel", x + 20, z - 4);
            Place(map, "AncientSmallCrate", x + 13, z - 5);
            Place(map, "AncientLongCrate", x + 20, z + 5);
            Place(map, "AncientBarrel", x + 11, z + 6);
            Place(map, "AncientSmallCrate", x + 16, z + 7);
            Place(map, "AncientBarrel", x - 8, z + 17);
            Place(map, "AncientSmallCrate", x - 5, z + 26);
            Place(map, "AncientLongCrate", x + 8, z + 26);
            Place(map, "AncientBarrel", x + 8, z + 17);
            Place(map, "AncientSmallCrate", x - 4, z + 18);
            Place(map, "AncientSmallCrate", x + 4, z + 18);
            Place(map, "AncientSmallCrate", x - 6, z - 12);
            Place(map, "AncientBarrel", x + 6, z - 8);
            Place(map, "AncientLongCrate", x - 6, z + 7);
            Place(map, "AncientSmallCrate", x + 6, z + 10);
            ScatterFilth(map, x - 21, z - 6, x - 10, z + 7);
            ScatterFilth(map, x + 10, z - 6, x + 21, z + 7);
            ScatterFilth(map, x - 9, z + 16, x + 9, z + 27);
            ScatterFilth(map, x - 7, z - 15, x + 7, z + 12);
            LightRoute(map, x, z);
            ScatterScrap(map, x, z);
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            if (knowledge != null && knowledge.ClaimScp093FirstMapLoot((int)variant))
            {
                Loot(map, "ComponentIndustrial", 4, x - 17, z - 1);
                Loot(map, "MedicineIndustrial", 3, x - 15, z + 2);
                Loot(map, "Gun_BoltActionRifle", 1, x + 5, z + 21);
                Loot(map, "MedicineIndustrial", 4, x + 7, z + 21);
                Loot(map, "ComponentSpacer", 1, x + 7, z + 24);
            }
            PlaceArchive(map, x, z + 23, variant);

            ThingDef exit = PocketMapUtility.currentlyGeneratingPortal.def.portal.exitDef;
            GenSpawn.Spawn(ThingMaker.MakeThing(exit), new IntVec3(x, 0, z - 23), map);
        }

        private static void GenerateVariant(Map map, Scp093WorldVariant variant)
        {
            int x = map.Size.x / 2;
            int z = map.Size.z / 2;
            string groundName = variant == Scp093WorldVariant.Red ? "SCP_093RedDust"
                : "SCP_093" + variant + "Ground";
            TerrainDef ground = DefDatabase<TerrainDef>.GetNamed(groundName);
            TerrainDef concrete = DefDatabase<TerrainDef>.GetNamed("AncientConcrete");
            foreach (IntVec3 cell in map.AllCells) map.terrainGrid.SetTerrain(cell, ground);
            switch (variant)
            {
                case Scp093WorldVariant.Blue:
                    // The blue report's open field leads into a sunken concrete passage.
                    Paint(map, x - 5, z - 18, x + 5, z + 25, concrete);
                    Paint(map, x - 11, z + 3, x + 11, z + 13,
                        DefDatabase<TerrainDef>.GetNamed("SCP_093BluePit"));
                    Paint(map, x - 3, z + 3, x + 3, z + 25, concrete);
                    RuinWalls(map, x - 5, z + 13, x + 5, z + 27, x, z + 13, x, z + 27);
                    Paint(map, x - 22, z - 5, x - 10, z + 8, concrete);
                    RuinWalls(map, x - 22, z - 5, x - 10, z + 8, x - 10, z + 1, x - 18, z + 8);
                    Paint(map, x + 11, z - 12, x + 23, z + 3, concrete);
                    RuinWalls(map, x + 11, z - 12, x + 23, z + 3, x + 11, z - 3, x + 19, z + 3);
                    Place(map, "AncientRustedCarFrame", x - 16, z - 13);
                    Place(map, "AncientLongCrate", x - 16, z + 2);
                    Place(map, "AncientBarrel", x + 17, z - 7);
                    Place(map, "AncientSmallCrate", x + 18, z - 2);
                    Place(map, "AncientLamp", x - 4, z + 18);
                    Place(map, "AncientSmallCrate", x + 3, z + 21);
                    break;
                case Scp093WorldVariant.Green:
                    // Farmstead and cellar entrance from the green report.
                    Paint(map, x - 17, z - 3, x + 17, z + 13,
                        DefDatabase<TerrainDef>.GetNamed("SCP_093GreenField"));
                    Paint(map, x - 3, z - 22, x + 3, z + 26, concrete);
                    Paint(map, x - 10, z + 15, x + 10, z + 28, concrete);
                    RuinWalls(map, x - 10, z + 15, x + 10, z + 28, x, z + 15, x - 7, z + 28);
                    Paint(map, x - 22, z - 12, x - 12, z - 2, concrete);
                    RuinWalls(map, x - 22, z - 12, x - 12, z - 2, x - 12, z - 7, x - 19, z - 2);
                    Place(map, "AncientRustedTruck", x + 16, z - 13);
                    Place(map, "AncientBarrel", x - 17, z - 7);
                    Place(map, "AncientSmallCrate", x + 15, z + 7);
                    Place(map, "AncientLongCrate", x - 15, z + 8);
                    Place(map, "AncientBarrel", x - 7, z + 19);
                    Place(map, "AncientLongCrate", x + 6, z + 22);
                    Place(map, "AncientSmallCrate", x - 5, z + 25);
                    break;
                case Scp093WorldVariant.Yellow:
                    // The yellow report's rows of cubicles and manager's office.
                    Paint(map, x - 20, z - 10, x + 20, z + 28, concrete);
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 4; col++)
                        {
                            int cx = x - 15 + col * 10;
                            int cz = z - 4 + row * 8;
                            Wall(map, cx, cz);
                            Wall(map, cx + 1, cz);
                            Place(map, "AncientSmallCrate", cx + 2, cz + 2);
                        }
                    RuinWalls(map, x - 8, z + 19, x + 8, z + 29, x, z + 19, x + 6, z + 29);
                    Place(map, "AncientRustedCar", x - 23, z - 16);
                    Place(map, "AncientBarrel", x - 12, z + 6);
                    Place(map, "AncientLongCrate", x + 13, z + 11);
                    Place(map, "AncientSmallCrate", x - 11, z + 16);
                    Place(map, "AncientSystemRack", x + 13, z + 17);
                    Place(map, "AncientSystemRack", x - 5, z + 24);
                    Place(map, "AncientLamp", x + 5, z + 24);
                    break;
                case Scp093WorldVariant.Red:
                    // The red report's stone and machine hub.
                    Paint(map, x - 18, z - 8, x + 18, z + 28, concrete);
                    Paint(map, x - 3, z - 23, x + 3, z + 22, concrete);
                    RuinWalls(map, x - 18, z - 8, x + 18, z + 28, x, z - 8, x, z + 28);
                    for (int dx = -13; dx <= 13; dx += 13)
                        for (int dz = 0; dz <= 18; dz += 9)
                            Place(map, "AncientSystemRack", x + dx, z + dz);
                    Place(map, "AncientLamp", x - 7, z + 22);
                    Place(map, "AncientLamp", x + 7, z + 22);
                    break;
            }
            if (variant != Scp093WorldVariant.Red)
            {
                LightRoute(map, x, z);
                ScatterScrap(map, x, z);
            }
            ScatterFilth(map, x - 24, z - 24, x + 24, z + 27);
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            if (knowledge != null && knowledge.ClaimScp093FirstMapLoot((int)variant))
            {
                Loot(map, "ComponentIndustrial", 3, x - 5, z + 17);
                Loot(map, "MedicineIndustrial", 2, x + 5, z + 17);
                if (variant == Scp093WorldVariant.Red)
                    Loot(map, "ComponentSpacer", 1, x + 5, z + 24);
            }
            PlaceArchive(map, x, z + 23, variant);
            ThingDef exit = PocketMapUtility.currentlyGeneratingPortal.def.portal.exitDef;
            GenSpawn.Spawn(ThingMaker.MakeThing(exit), new IntVec3(x, 0, z - 23), map);
        }

        private static void PlaceArchive(Map map, int x, int z, Scp093WorldVariant variant)
        {
            Thing archive = Place(map, "SCP_093ArchiveConsole", x, z);
            CompScp093Archive comp = archive == null ? null : archive.TryGetComp<CompScp093Archive>();
            if (comp != null) comp.SetVariant(variant);
        }

        private static void LightRoute(Map map, int x, int z)
        {
            for (int dz = -18; dz <= 18; dz += 9)
            {
                PlaceIfOpen(map, "AncientLamp", x - 5, z + dz);
                PlaceIfOpen(map, "AncientLamp", x + 5, z + dz);
            }
            PlaceIfOpen(map, "AncientLamp", x - 5, z + 24);
            PlaceIfOpen(map, "AncientLamp", x + 5, z + 24);
            PlaceIfOpen(map, "AncientLamp", x - 16, z + 2);
            PlaceIfOpen(map, "AncientLamp", x + 16, z + 2);
        }

        private static void ScatterScrap(Map map, int x, int z)
        {
            string[] props = { "AncientSmallCrate", "AncientBarrel", "ChunkSlagSteel" };
            for (int i = 0; i < 46; i++)
            {
                int dx = ((i * 17 + i * i * 3) % 51) - 25;
                int dz = ((i * 23 + i * i * 7) % 51) - 25;
                if (Math.Abs(dx) < 5 || (Math.Abs(dx) < 8 && dz > 19)) continue;
                PlaceIfOpen(map, props[i % props.Length], x + dx, z + dz);
            }
        }

        private static void PlaceIfOpen(Map map, string defName, int x, int z)
        {
            IntVec3 cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetThingList(map).Count != 0) return;
            Place(map, defName, x, z);
        }

        private static void Paint(Map map, int x0, int z0, int x1, int z1, TerrainDef terrain)
        {
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (cell.InBounds(map)) map.terrainGrid.SetTerrain(cell, terrain);
                }
        }

        private static void RuinWalls(Map map, int x0, int z0, int x1, int z1,
            int entranceX, int entranceZ, int breakX, int breakZ)
        {
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if ((x == x0 || x == x1 || z == z0 || z == z1)
                        && (Math.Abs(x - entranceX) + Math.Abs(z - entranceZ) > 1)
                        && (Math.Abs(x - breakX) + Math.Abs(z - breakZ) > 1))
                        Wall(map, x, z);
        }

        private static void Wall(Map map, int x, int z)
        {
            IntVec3 cell = new IntVec3(x, 0, z);
            if (cell.InBounds(map))
                GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Wall"),
                    DefDatabase<ThingDef>.GetNamed("Steel")), cell, map);
        }

        private static Thing Place(Map map, string defName, int x, int z)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            IntVec3 cell = new IntVec3(x, 0, z);
            if (GenAdj.OccupiedRect(cell, Rot4.North, def.size)
                .FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z)))
                return GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
            return null;
        }

        private static void Loot(Map map, string defName, int count, int x, int z)
        {
            Thing item = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName));
            item.stackCount = count;
            GenSpawn.Spawn(item, new IntVec3(x, 0, z), map);
        }

        private static void ScatterFilth(Map map, int x0, int z0, int x1, int z1)
        {
            ThingDef trash = DefDatabase<ThingDef>.GetNamed("Filth_Trash");
            ThingDef rubble = DefDatabase<ThingDef>.GetNamed("Filth_RubbleBuilding");
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    // A fixed, irregular pattern keeps the path legible and does not
                    // consume the map generator's random state.
                    int pattern = (x * 17 + z * 29 + x * z) & 31;
                    if (pattern != 2 && pattern != 7 && pattern != 19) continue;
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null)
                        FilthMaker.TryMakeFilth(cell, map, pattern == 19 ? rubble : trash);
                }
        }
    }
}
