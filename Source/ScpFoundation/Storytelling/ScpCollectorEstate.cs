using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_ScpCollectorEstateSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            if (map == null || !map.IsPlayerHome || Current.Game == null || Faction.OfAncientsHostile == null)
                return false;
            ScpArrivalRegistry registry = Current.Game.GetComponent<ScpArrivalRegistry>();
            return registry.FirstEncounterComplete
                && Current.Game.GetComponent<FoundationContactComponent>().progress.stage == FoundationContactStage.Established
                && !registry.HasArrived("067") && !registry.HasArrived("316") && !registry.HasArrived("012")
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_CollectorEstate") != null
                && DefDatabase<ThingDef>.GetNamedSilentFail("SCP_067Pen") != null
                && DefDatabase<ThingDef>.GetNamedSilentFail("SCP_316Lamp") != null
                && DefDatabase<ThingDef>.GetNamedSilentFail("SCP_012Score") != null
                && DefDatabase<ThingDef>.GetNamedSilentFail("SCP_093Disc") != null;
        }

        protected override void RunInt()
        {
            QuestGen.slate.Set("siteFaction", Faction.OfAncientsHostile);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_CollectorEstate")
            });
        }
    }

    public sealed class QuestNode_ScpCollectorEstateTrack : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_ScpCollectorEstate {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpCollectorEstateRecovered"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpCollectorEstateLost"),
                sendTranslatedLetter = true
            });
        }
    }

    public sealed class QuestPart_ScpCollectorEstate : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal;
        public string lostSignal;
        public bool sendTranslatedLetter;
        private Thing pen, lamp, score, disc;
        private bool entered, finished, vaultArranged, discInitialized;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scpCollectorEstateSite");
            Scribe_References.Look(ref pen, "scpCollectorPen");
            Scribe_References.Look(ref lamp, "scpCollectorLamp");
            Scribe_References.Look(ref score, "scpCollectorScore");
            Scribe_References.Look(ref disc, "scpCollectorDisc");
            Scribe_Values.Look(ref recoveredSignal, "scpCollectorRecoveredSignal");
            Scribe_Values.Look(ref lostSignal, "scpCollectorLostSignal");
            Scribe_Values.Look(ref sendTranslatedLetter, "scpCollectorSendTranslatedLetter", false);
            Scribe_Values.Look(ref entered, "scpCollectorEntered", false);
            Scribe_Values.Look(ref finished, "scpCollectorFinished", false);
            Scribe_Values.Look(ref vaultArranged, "scpCollectorVaultArranged", false);
            Scribe_Values.Look(ref discInitialized, "scpCollectorDiscInitialized", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 60 != 0) return;
            Map siteMap = site == null ? null : site.Map;
            if (siteMap != null && !discInitialized)
            {
                ThingDef discDef = DefDatabase<ThingDef>.GetNamed("SCP_093Disc");
                disc = siteMap.listerThings.ThingsOfDef(discDef).FirstOrDefault();
                if (disc == null)
                {
                    // Existing collector maps predate the disc; add it once to their vault.
                    IntVec3 discCell = new IntVec3(71, 0, 131);
                    if (!discCell.Standable(siteMap) || discCell.GetEdifice(siteMap) != null)
                    { Finish(false); return; }
                    disc = GenSpawn.Spawn(ThingMaker.MakeThing(discDef), discCell, siteMap);
                }
                discInitialized = true;
            }
            if (siteMap != null && !entered)
            {
                entered = true;
                pen = siteMap.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("SCP_067Pen")).FirstOrDefault();
                score = siteMap.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("SCP_012Score")).FirstOrDefault();
                ThingDef lampDef = DefDatabase<ThingDef>.GetNamed("SCP_316Lamp");
                lamp = siteMap.mapPawns.AllPawnsSpawned.Select(pawn => pawn.equipment == null ? null : pawn.equipment.Primary)
                    .FirstOrDefault(weapon => weapon != null && weapon.def == lampDef)
                    ?? siteMap.listerThings.ThingsOfDef(lampDef).FirstOrDefault();
                if (pen == null || lamp == null || score == null || disc == null) { Finish(false); return; }
            }
            if (!entered) return;
            if (pen == null || lamp == null || score == null || disc == null
                || pen.Destroyed || lamp.Destroyed || score.Destroyed || disc.Destroyed) { Finish(false); return; }
            if (!vaultArranged && siteMap != null)
                vaultArranged = ArrangeExistingSite(siteMap);
            if (pen.MapHeld != null && pen.MapHeld.IsPlayerHome
                && lamp.MapHeld != null && lamp.MapHeld.IsPlayerHome
                && score.MapHeld != null && score.MapHeld.IsPlayerHome
                && disc.MapHeld != null && disc.MapHeld.IsPlayerHome) Finish(true);
        }

        private bool ArrangeExistingSite(Map map)
        {
            // Already generated quest maps keep their saved objects. Move only objects
            // still at the old scripted positions; leave items carried by the player alone.
            Thing vaultWall = new IntVec3(65, 0, 131).GetEdifice(map);
            if (vaultWall == null || vaultWall.def.defName != "Wall"
                || vaultWall.Stuff != DefDatabase<ThingDef>.GetNamed("Plasteel"))
                return true; // An older procedural site has no plasteel vault.
            IntVec3 penCell = new IntVec3(70, 0, 131);
            IntVec3 scoreCell = new IntVec3(72, 0, 131);
            IntVec3 lampCell = new IntVec3(74, 0, 131);
            if (!penCell.Standable(map) || penCell.GetEdifice(map) != null
                || !scoreCell.Standable(map) || scoreCell.GetEdifice(map) != null
                || !lampCell.Standable(map) || lampCell.GetEdifice(map) != null)
                return false;
            MoveFromOldPosition(pen, map, new IntVec3(100, 0, 120), penCell);
            MoveFromOldPosition(score, map, new IntVec3(89, 0, 122), scoreCell);
            Pawn carrier = map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn => pawn.equipment != null
                && pawn.equipment.Primary == lamp && pawn.Faction == Faction.OfAncientsHostile);
            if (carrier != null)
            {
                ThingWithComps dropped;
                if (!carrier.equipment.TryDropEquipment((ThingWithComps)lamp, out dropped, lampCell, false)
                    || dropped == null) return false;
                lamp = dropped;
            }
            return true;
        }

        private static void MoveFromOldPosition(Thing thing, Map map, IntVec3 oldCell, IntVec3 vaultCell)
        {
            if (thing == null || !thing.Spawned || thing.Map != map || thing.Position != oldCell)
                return;
            thing.DeSpawn();
            GenSpawn.Spawn(thing, vaultCell, map);
        }

        private void Finish(bool success)
        {
            finished = true;
            if (success && sendTranslatedLetter)
                Find.LetterStack.ReceiveLetter("SCP_CollectorRecoveredTitle".Translate(),
                    "SCP_CollectorRecoveredBody".Translate(), LetterDefOf.PositiveEvent, site);
            Find.SignalManager.SendSignal(new Signal(success ? recoveredSignal : lostSignal));
        }
    }

    public sealed class GenStep_ScpCollectorEstate : GenStep
    {
        private const int CellCount = 250 * 250;
        public override int SeedPart { get { return 67316012; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_CollectorEstateSite")
                throw new InvalidOperationException("Collector estate may only generate on its quest site.");
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            if (window.Width != 250 || window.Height != 250)
                throw new InvalidOperationException("Collector estate requires a 250x250 quest map.");
            string root = Path.Combine(def.modContentPack.RootDir, "Assets", "ScpCollectorEstate");
            string thingsPath = Path.Combine(root, "Things.csv");
            string[] gridPaths = { "Terrain.bin", "Roofs.bin", "UnderTerrain.bin", "CompressedThings.bin" };
            if (!File.Exists(thingsPath) || gridPaths.Any(path => !File.Exists(Path.Combine(root, path))))
                throw new FileNotFoundException("Collector estate layout is incomplete: " + root);
            byte[][] grids = gridPaths.Select(path => File.ReadAllBytes(Path.Combine(root, path))).ToArray();
            if (grids.Any(grid => grid.Length != CellCount * 2))
                throw new InvalidOperationException("Collector estate map grid has an invalid size.");

            Faction faction = Faction.OfAncientsHostile;
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
                if (thingDef == null) throw new InvalidOperationException("Unknown collector estate map thing hash: " + hash);
                if (ScpSavedQuestSiteLayout.ExcludeDecoration(thingDef)) continue;
                GenSpawn.Spawn(ThingMaker.MakeThing(thingDef), cell, map);
            }
            HashSet<string> failedDefs = new HashSet<string>();
            foreach (string line in File.ReadLines(thingsPath))
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
                    if (thing.def.CanHaveFaction) thing.SetFaction(faction);
                    GenSpawn.Spawn(thing, cell, map, new Rot4(rotation));
                }
                catch (Exception exception)
                {
                    if (failedDefs.Add(fields[2]))
                        Log.Warning("Collector estate could not restore " + fields[2] + ": " + exception);
                }
            }

            // The four objects belong together in the plasteel vault west of the hall.
            IntVec3 penCell = new IntVec3(window.MapX(70), 0, window.MapZ(131));
            IntVec3 discCell = new IntVec3(window.MapX(71), 0, window.MapZ(131));
            IntVec3 scoreCell = new IntVec3(window.MapX(72), 0, window.MapZ(131));
            IntVec3 lampCell = new IntVec3(window.MapX(74), 0, window.MapZ(131));
            IntVec3 collectorCell = new IntVec3(window.MapX(85), 0, window.MapZ(131));
            foreach (IntVec3 cell in new[] { penCell, discCell, scoreCell, lampCell, collectorCell })
                if (!cell.Standable(map) || cell.GetEdifice(map) != null)
                    throw new InvalidOperationException("Collector estate quest spawn cell is blocked: " + cell);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_067Pen")),
                penCell, map);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_093Disc")),
                discCell, map);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_012Score")),
                scoreCell, map);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_316Lamp")),
                lampCell, map);

            Pawn collector = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Villager"), faction);
            collector.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            collector.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("Gun_Revolver")));
            GenSpawn.Spawn(collector, collectorCell, map);
            int[,] guardSpots = {
                { 87, 135 }, { 84, 133 }, { 90, 132 },
                { 87, 125 }, { 93, 125 }, { 88, 138 }
            };
            List<Pawn> defenders = new List<Pawn> { collector };
            PawnKindDef guardKind = DefDatabase<PawnKindDef>.GetNamed("AncientSoldier");
            for (int i = 0; i < guardSpots.GetLength(0); i++)
            {
                IntVec3 preferred = new IntVec3(window.MapX(guardSpots[i, 0]), 0,
                    window.MapZ(guardSpots[i, 1]));
                IntVec3 guardCell = IntVec3.Invalid;
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(preferred, 5f, true))
                    if (candidate.InBounds(map) && candidate.Standable(map)
                        && candidate.GetEdifice(map) == null
                        && !candidate.GetThingList(map).Any(thing => thing is Pawn))
                    { guardCell = candidate; break; }
                if (!guardCell.IsValid)
                    throw new InvalidOperationException("No free collector guard cell near " + preferred);
                Pawn guard = PawnGenerator.GeneratePawn(guardKind, faction);
                GenSpawn.Spawn(guard, guardCell, map);
                defenders.Add(guard);
            }
            LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, collectorCell, 30, false), map,
                defenders);
        }

        private static ushort ReadHash(byte[] grid, int index)
        {
            return (ushort)(grid[index] | grid[index + 1] << 8);
        }
    }
}
