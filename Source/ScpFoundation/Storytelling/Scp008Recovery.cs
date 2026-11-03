using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using ScpFoundation.Anomalies;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_Scp008SiteSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            FactionDef gocDef = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_GlobalOccultCoalition");
            FactionDef entitiesDef = DefDatabase<FactionDef>.GetNamedSilentFail("Entities");
            return map != null && map.IsPlayerHome && Current.Game != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && Current.Game.GetComponent<FoundationContactComponent>().progress.stage == FoundationContactStage.Established
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("008")
                && gocDef != null && entitiesDef != null
                && Find.FactionManager.FirstFactionOfDef(gocDef) != null
                && Find.FactionManager.FirstFactionOfDef(entitiesDef) != null
                && DefDatabase<PawnKindDef>.GetNamedSilentFail("Gorehulk") != null
                && DefDatabase<PawnKindDef>.GetNamedSilentFail("Bulbfreak") != null
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_008GocLab") != null;
        }

        protected override void RunInt()
        {
            FactionDef gocDef = DefDatabase<FactionDef>.GetNamed("SCP_GlobalOccultCoalition");
            Faction faction = Find.FactionManager.FirstFactionOfDef(gocDef);
            ScpFactionRelationsComponent.EnsureHostile(faction, Faction.OfPlayer);
            QuestGen.slate.Set("siteFaction", faction);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_008GocLab")
            });
        }
    }

    public sealed class QuestNode_Scp008TrackRecovery : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp008Recovery {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp008Recovered"),
                destroyedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp008Destroyed"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp008Lost")
            });
        }
    }

    public sealed class QuestPart_Scp008Recovery : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal, destroyedSignal, lostSignal;
        private Thing sample;
        private bool entered, breached, finished;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scp008Site");
            Scribe_References.Look(ref sample, "scp008Sample");
            Scribe_Values.Look(ref recoveredSignal, "scp008RecoveredSignal");
            Scribe_Values.Look(ref destroyedSignal, "scp008DestroyedSignal");
            Scribe_Values.Look(ref lostSignal, "scp008LostSignal");
            Scribe_Values.Look(ref entered, "scp008SiteEntered", false);
            Scribe_Values.Look(ref breached, "scp008LabBreached", false);
            Scribe_Values.Look(ref finished, "scp008RecoveryFinished", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 30 != 0) return;
            Map map = site == null ? null : site.Map;
            if (map != null && !entered)
            {
                entered = true;
                sample = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("SCP_008SampleCase"))
                    .FirstOrDefault();
                if (sample == null) { Finish(lostSignal); return; }
            }
            if (sample == null) { if (entered) Finish(lostSignal); return; }
            if (!breached && map != null && !sample.Destroyed && PlayerOpenedLab(map))
            {
                breached = true; // Serialize this before any possible save during the breach letter.
                ReleaseSpecimens(map);
            }
            if (sample.Destroyed) { Finish(destroyedSignal); return; }
            if (sample.MapHeld != null && sample.MapHeld.IsPlayerHome) Finish(recoveredSignal);
        }

        private bool PlayerOpenedLab(Map map)
        {
            Room lab = sample.Position.GetRoom(map);
            if (lab == null) return false;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Any(pawn => pawn.Position.GetRoom(map) == lab)) return true;
            HashSet<Building_Door> doors = new HashSet<Building_Door>();
            foreach (IntVec3 cell in lab.BorderCellsCardinal)
            {
                Building_Door door = cell.GetDoor(map);
                if (door != null && door.Open) doors.Add(door);
            }
            return doors.Any(door => colonists.Any(pawn => pawn.Position.DistanceToSquared(door.Position) <= 4));
        }

        private void ReleaseSpecimens(Map map)
        {
            FactionDef entitiesDef = DefDatabase<FactionDef>.GetNamed("Entities");
            Faction entities = Find.FactionManager.FirstFactionOfDef(entitiesDef);
            Room lab = sample.Position.GetRoom(map);
            List<IntVec3> cells = lab.Cells.Where(cell => cell.Standable(map)
                && cell.GetThingList(map).All(thing => !(thing is Pawn) && thing.def.category != ThingCategory.Building))
                .OrderBy(cell => cell.DistanceToSquared(sample.Position)).ToList();
            PawnKindDef[] kinds = {
                DefDatabase<PawnKindDef>.GetNamed("Gorehulk"),
                DefDatabase<PawnKindDef>.GetNamed("Gorehulk"),
                DefDatabase<PawnKindDef>.GetNamed("Gorehulk"),
                DefDatabase<PawnKindDef>.GetNamed("Bulbfreak"),
                DefDatabase<PawnKindDef>.GetNamed("Bulbfreak")
            };
            if (cells.Count < kinds.Length)
                throw new InvalidOperationException("SCP-008 laboratory needs five free cells for specimens.");
            List<Pawn> specimens = new List<Pawn>();
            for (int i = 0; i < kinds.Length; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kinds[i], entities);
                GenSpawn.Spawn(pawn, cells[i], map);
                specimens.Add(pawn);
            }
            LordMaker.MakeNewLord(entities,
                new LordJob_AssaultColony(entities, false, true, false, false, false, false, false), map, specimens);
            Find.LetterStack.ReceiveLetter("SCP_008LabBreachTitle".Translate(),
                "SCP_008LabBreachBody".Translate(), LetterDefOf.ThreatBig, sample);
        }

        private void Finish(string signal)
        {
            finished = true;
            Find.SignalManager.SendSignal(new Signal(signal));
        }
    }

    // Restore the user's GOC base; retain a small procedural layout only if its
    // exported assets are absent during development.
    public sealed class GenStep_Scp008GocLab : GenStep
    {
        private const int CellCount = 250 * 250;
        public override int SeedPart { get { return 8; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_008RecoverySite")
                throw new InvalidOperationException("SCP-008 laboratory can only generate on its quest site.");
            string root = Path.Combine(def.modContentPack.RootDir, "Assets", "Scp008GocLab");
            if (File.Exists(Path.Combine(root, "Things.csv")))
            {
                GenerateSavedLayout(map, root);
                return;
            }
            IntVec3 middle = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            IntVec3 center = IntVec3.Invalid;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(middle, 50f, true))
            {
                CellRect area = new CellRect(candidate.x - 9, candidate.z - 9, 19, 19);
                if (!area.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z))) continue;
                if (!area.Cells.All(cell => cell.Standable(map) && cell.GetEdifice(map) == null)) continue;
                center = candidate;
                break;
            }
            if (!center.IsValid) throw new InvalidOperationException("No clear space for the SCP-008 laboratory.");
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamed("SCP_GlobalOccultCoalition");
            Faction goc = Find.FactionManager.FirstFactionOfDef(factionDef);
            ThingDef wall = DefDatabase<ThingDef>.GetNamed("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamed("Door");
            ThingDef steel = DefDatabase<ThingDef>.GetNamed("Steel");
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamed("SterileTile");
            CellRect room = new CellRect(center.x - 7, center.z - 7, 15, 15);
            foreach (IntVec3 cell in room.Cells)
            {
                map.terrainGrid.SetTerrain(cell, floor);
                map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                if (cell.x != room.minX && cell.x != room.maxX
                    && cell.z != room.minZ && cell.z != room.maxZ) continue;
                bool entrance = cell.x == center.x && cell.z == room.minZ;
                Thing building = ThingMaker.MakeThing(entrance ? door : wall, steel);
                if (building.def.CanHaveFaction) building.SetFaction(goc);
                GenSpawn.Spawn(building, cell, map);
            }
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_008SampleCase")),
                center, map);
            Pawn scientist = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("SCP_GocResearcher"), goc);
            scientist.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            scientist.health.AddHediff(DefDatabase<HediffDef>.GetNamed("SCP_008Infection"));
            GenSpawn.Spawn(scientist, center + IntVec3.North * 3, map);
            IntVec3[] positions = { center + IntVec3.South * 11 + IntVec3.West * 4,
                center + IntVec3.South * 11 + IntVec3.East * 4,
                center + IntVec3.East * 11, center + IntVec3.West * 11 };
            List<Pawn> guards = SpawnGuards(map, goc, positions,
                new CellRect(0, 0, map.Size.x, map.Size.z));
            guards.Add(scientist);
            LordMaker.MakeNewLord(goc, new LordJob_DefendBase(goc, center, 35, false), map, guards);
        }

        private void GenerateSavedLayout(Map map, string root)
        {
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            if (window.Width != 250 || window.Height != 250)
                throw new InvalidOperationException("SCP-008 saved site requires a 250x250 map.");
            string[] gridPaths = { "Terrain.bin", "Roofs.bin", "UnderTerrain.bin", "CompressedThings.bin" };
            if (gridPaths.Any(path => !File.Exists(Path.Combine(root, path))))
                throw new FileNotFoundException("SCP-008 map layout is incomplete: " + root);
            byte[][] grids = gridPaths.Select(path => File.ReadAllBytes(Path.Combine(root, path))).ToArray();
            if (grids.Any(grid => grid.Length != CellCount * 2))
                throw new InvalidOperationException("SCP-008 map grid has an invalid size.");
            Faction goc = Find.FactionManager.FirstFactionOfDef(
                DefDatabase<FactionDef>.GetNamed("SCP_GlobalOccultCoalition"));
            foreach (Thing existing in new List<Thing>(map.listerThings.AllThings))
                if (existing.Spawned) existing.Destroy(DestroyMode.Vanish);
            CellRect siteRect = new CellRect(0, 0, map.Size.x, map.Size.z);
            foreach (IntVec3 cell in siteRect.Cells)
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
            foreach (IntVec3 cell in siteRect.Cells)
            {
                ushort hash = ReadHash(grids[3], window.SourceByteIndex(cell.x, cell.z));
                if (hash == 0) continue;
                ThingDef thingDef = DefDatabase<ThingDef>.GetByShortHash(hash);
                if (thingDef == null)
                    throw new InvalidOperationException("Unknown SCP-008 map thing hash: " + hash);
                if (ScpSavedQuestSiteLayout.ExcludeDecoration(thingDef)) continue;
                GenSpawn.Spawn(ThingMaker.MakeThing(thingDef), cell, map);
            }
            HashSet<string> failedDefs = new HashSet<string>();
            foreach (string line in File.ReadLines(Path.Combine(root, "Things.csv")))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split(',');
                if (fields.Length != 9 || failedDefs.Contains(fields[2])) continue;
                ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(fields[2]);
                if (thingDef == null) continue;
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
                    if (thing.def.CanHaveFaction) thing.SetFaction(goc);
                    GenSpawn.Spawn(thing, cell, map, new Rot4(rotation));
                }
                catch (Exception exception)
                {
                    if (failedDefs.Add(fields[2]))
                        Log.Warning("SCP-008 map could not restore " + fields[2] + ": " + exception);
                }
            }
            // The case and infected researcher are placed in the user's plasteel room.
            CellRect labRect = new CellRect(window.MapX(87), window.MapZ(165), 12, 5);
            IntVec3 sampleCell = FreeCell(map, new IntVec3(window.MapX(93), 0, window.MapZ(167)), labRect);
            IntVec3 scientistCell = FreeCell(map, new IntVec3(window.MapX(90), 0, window.MapZ(167)),
                labRect, new HashSet<IntVec3> { sampleCell });
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_008SampleCase")),
                sampleCell, map);
            Pawn scientist = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("SCP_GocResearcher"), goc);
            scientist.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            scientist.health.AddHediff(DefDatabase<HediffDef>.GetNamed("SCP_008Infection"));
            GenSpawn.Spawn(scientist, scientistCell, map);
            LordMaker.MakeNewLord(goc, new LordJob_DefendBase(goc, scientistCell, 2, false), map,
                new List<Pawn> { scientist });
            IntVec3[] positions = {
                new IntVec3(window.MapX(89), 0, window.MapZ(174)),
                new IntVec3(window.MapX(97), 0, window.MapZ(174)),
                new IntVec3(window.MapX(83), 0, window.MapZ(168)),
                new IntVec3(window.MapX(102), 0, window.MapZ(169))
            };
            List<Pawn> guards = SpawnGuards(map, goc, positions, siteRect);
            LordMaker.MakeNewLord(goc, new LordJob_DefendBase(goc,
                new IntVec3(window.MapX(93), 0, window.MapZ(174)), 35, false), map, guards);
        }

        private static List<Pawn> SpawnGuards(Map map, Faction goc, IntVec3[] positions, CellRect bounds)
        {
            List<Pawn> guards = new List<Pawn>();
            HashSet<IntVec3> used = new HashSet<IntVec3>();
            PawnKindDef regular = DefDatabase<PawnKindDef>.GetNamed("SCP_GocOperative");
            PawnKindDef heavy = DefDatabase<PawnKindDef>.GetNamed("SCP_GocHeavyOperative");
            for (int i = 0; i < 14; i++)
            {
                IntVec3 cell = FreeCell(map, positions[i % positions.Length], bounds, used, 20f);
                Pawn guard = PawnGenerator.GeneratePawn(i < 3 ? heavy : regular, goc);
                GenSpawn.Spawn(guard, cell, map);
                guards.Add(guard);
                used.Add(cell);
            }
            return guards;
        }

        private static IntVec3 FreeCell(Map map, IntVec3 preferred, CellRect bounds,
            HashSet<IntVec3> excluded = null, float radius = 10f)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(preferred, radius, true))
                if (cell.InBounds(map) && bounds.Contains(cell) && cell.Standable(map)
                    && (excluded == null || !excluded.Contains(cell))
                    && cell.GetThingList(map).All(thing => !(thing is Pawn)
                        && thing.def.category != ThingCategory.Building)) return cell;
            throw new InvalidOperationException("No free SCP-008 quest spawn cell near " + preferred);
        }

        private static ushort ReadHash(byte[] grid, int index)
        {
            return (ushort)(grid[index] | grid[index + 1] << 8);
        }
    }
}
