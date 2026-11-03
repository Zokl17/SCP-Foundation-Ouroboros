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
    public sealed class QuestNode_Scp079SiteSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            return map != null && map.IsPlayerHome && Faction.OfAncientsHostile != null
                && Current.Game != null && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && Current.Game.GetComponent<FoundationAssignmentComponent>().progress.stage == FoundationAssignmentStage.Completed
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("079")
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_079RecoveryLab") != null;
        }

        protected override void RunInt()
        {
            QuestGen.slate.Set("siteFaction", Faction.OfAncientsHostile);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_079RecoveryLab")
            });
        }
    }

    public sealed class QuestNode_Scp079TrackRecovery : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp079Recovery {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp079Recovered"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp079Lost")
            });
        }
    }

    public sealed class QuestPart_Scp079Recovery : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal;
        public string lostSignal;
        private Thing computer;
        private bool entered;
        private bool finished;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scp079Site");
            Scribe_References.Look(ref computer, "scp079RecoveredComputer");
            Scribe_Values.Look(ref recoveredSignal, "scp079RecoveredSignal");
            Scribe_Values.Look(ref lostSignal, "scp079LostSignal");
            Scribe_Values.Look(ref entered, "scp079SiteEntered", false);
            Scribe_Values.Look(ref finished, "scp079RecoveryFinished", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 60 != 0) return;
            Map siteMap = site == null ? null : site.Map;
            if (siteMap != null && !entered)
            {
                entered = true;
                ThingDef def = DefDatabase<ThingDef>.GetNamed("SCP_079Computer");
                computer = siteMap.listerThings.ThingsOfDef(def).FirstOrDefault();
                if (computer == null) { Finish(false); return; }
            }
            if (computer == null) { if (entered) Finish(false); return; }
            if (computer.Destroyed) { Finish(false); return; }
            if (computer.MapHeld != null && computer.MapHeld.IsPlayerHome)
            {
                Finish(true);
                return;
            }
        }

        private void Finish(bool success)
        {
            finished = true;
            if (success)
                foreach (QuestPart_Letter part in quest.PartsListForReading.OfType<QuestPart_Letter>())
                    if (part.inSignal == recoveredSignal && part.letter != null)
                    {
                        part.letter.Label = "SCP_079RecoveredTitle".Translate();
                        ChoiceLetter letter = part.letter as ChoiceLetter;
                        if (letter != null) letter.Text = "SCP_079RecoveredBody".Translate();
                    }
            Find.SignalManager.SendSignal(new Signal(success ? recoveredSignal : lostSignal));
        }
    }

    public sealed class GenStep_Scp079RecoveryLab : GenStep
    {
        private const int CellCount = 250 * 250;
        public override int SeedPart { get { return 79; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_079RecoverySite")
                throw new InvalidOperationException("SCP-079 lab must only generate on its quest site.");
            Scp882MapWindow window = new Scp882MapWindow(map.Size.x, map.Size.z);
            if (window.Width != 250 || window.Height != 250)
                throw new InvalidOperationException("SCP-079 site requires a 250x250 map.");
            string root = Path.Combine(def.modContentPack.RootDir, "Assets", "Scp079DataCenter");
            string thingsPath = Path.Combine(root, "Things.csv");
            string[] gridPaths = { "Terrain.bin", "Roofs.bin", "UnderTerrain.bin", "CompressedThings.bin" };
            if (!File.Exists(thingsPath) || gridPaths.Any(path => !File.Exists(Path.Combine(root, path))))
                throw new FileNotFoundException("SCP-079 map layout is incomplete: " + root);
            byte[][] grids = gridPaths.Select(path => File.ReadAllBytes(Path.Combine(root, path))).ToArray();
            if (grids.Any(grid => grid.Length != CellCount * 2))
                throw new InvalidOperationException("SCP-079 map grid has an invalid size.");

            Faction faction = Faction.OfAncientsHostile;
            foreach (Thing existing in new List<Thing>(map.listerThings.AllThings))
                if (existing.Spawned) existing.Destroy(DestroyMode.Vanish);
            foreach (IntVec3 cell in new CellRect(0, 0, map.Size.x, map.Size.z).Cells)
            {
                int index = window.SourceByteIndex(cell.x, cell.z);
                ushort groundHash = ReadHash(grids[0], index);
                TerrainDef ground = DefDatabase<TerrainDef>.GetByShortHash(groundHash);
                if (ground != null) map.terrainGrid.SetTerrain(cell, ground);
                ushort underHash = ReadHash(grids[2], index);
                GenStep_Scp882Workshop.RestoreUnderTerrain(map.terrainGrid, cell,
                    underHash == 0 ? null : DefDatabase<TerrainDef>.GetByShortHash(underHash));
                ushort roofHash = ReadHash(grids[1], index);
                map.roofGrid.SetRoof(cell, roofHash == 0 ? null : DefDatabase<RoofDef>.GetByShortHash(roofHash));
            }
            foreach (IntVec3 cell in new CellRect(0, 0, map.Size.x, map.Size.z).Cells)
            {
                ushort hash = ReadHash(grids[3], window.SourceByteIndex(cell.x, cell.z));
                if (hash == 0) continue;
                ThingDef thingDef = DefDatabase<ThingDef>.GetByShortHash(hash);
                if (thingDef == null) throw new InvalidOperationException("Unknown SCP-079 map thing hash: " + hash);
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
                if (!GenAdj.OccupiedRect(cell, new Rot4(rotation), thingDef.size)
                    .FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z))) continue;
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
                        Log.Warning("SCP-079 map could not restore " + fields[2] + ": " + exception);
                }
            }
            IntVec3 center = new IntVec3(window.MapX(117), 0, window.MapZ(90));
            if (!center.Standable(map) || center.GetThingList(map).Any(thing => thing.def.category == ThingCategory.Building))
                throw new InvalidOperationException("SCP-079 computer cell is blocked: " + center);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_079Computer")), center, map);
            PawnKindDef guardKind = DefDatabase<PawnKindDef>.GetNamed("SCP_079LabGuard");
            List<Pawn> guards = new List<Pawn>();
            IntVec3[] positions = {
                new IntVec3(115, 0, 82), new IntVec3(120, 0, 83),
                new IntVec3(114, 0, 94), new IntVec3(121, 0, 96)
            };
            foreach (IntVec3 preferred in positions)
            {
                IntVec3 cell = IntVec3.Invalid;
                foreach (IntVec3 candidate in GenRadial.RadialCellsAround(preferred, 5f, true))
                    if (candidate.InBounds(map) && candidate.Standable(map)
                        && !candidate.GetThingList(map).Any(thing => thing is Pawn))
                    { cell = candidate; break; }
                if (!cell.IsValid) continue;
                Pawn guard = PawnGenerator.GeneratePawn(guardKind, faction);
                GenSpawn.Spawn(guard, cell, map);
                guards.Add(guard);
            }
            if (guards.Count > 0)
                LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, center, 30, false), map, guards);
        }

        private static ushort ReadHash(byte[] grid, int index)
        {
            return (ushort)(grid[index] | grid[index + 1] << 8);
        }
    }
}
