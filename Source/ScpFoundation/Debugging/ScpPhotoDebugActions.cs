#if SCP_DEVELOPMENT_TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Debugging
{
    // Opt-in photo/testing tools. Public builds do not contain these actions.
    public static class ScpPhotoDebugActions
    {
        [DebugAction("SCP Foundation / Testing", "Spawn all SCP mod items", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SpawnAllModItems()
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null) return;
            List<ThingDef> definitions = ModItems().ToList();
            IntVec3 origin = UI.MouseCell();
            if (!origin.InBounds(map)) return;
            List<IntVec3> cells = GenRadial.RadialCellsAround(origin, 35f, true)
                .Where(cell => cell.InBounds(map) && !cell.Fogged(map) && cell.Standable(map)
                    && cell.GetThingList(map).Count == 0).Take(definitions.Count).ToList();
            if (cells.Count < definitions.Count)
            {
                Messages.Message("Not enough empty space for all mod items.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            for (int i = 0; i < definitions.Count; i++) SpawnItem(definitions[i], cells[i], map);
            Messages.Message("Spawned " + definitions.Count + " SCP mod items.", MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("SCP Foundation / Testing", "Spawn SCP mod item...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static List<DebugActionNode> ItemMenu()
        {
            return ModItems().Select(def => new DebugActionNode(def.LabelCap + " [" + def.defName + "]",
                DebugActionType.ToolMap, () => SpawnSingleItem(def))).ToList();
        }

        [DebugAction("SCP Foundation / Testing", "Spawn faction squad...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static List<DebugActionNode> SquadMenu()
        {
            var nodes = new List<DebugActionNode>();
            AddSquad(nodes, "GOC - black suits (6)", "SCP_GlobalOccultCoalition", "SCP_GocHkOperative", "SCP_GocHkOperative");
            AddSquad(nodes, "GOC - white exosuits (6)", "SCP_GlobalOccultCoalition", "SCP_GocHeavyOperative", "SCP_GocHeavyOperative");
            AddSquad(nodes, "GOC - mixed (6)", "SCP_GlobalOccultCoalition", "SCP_GocHkOperative", "SCP_GocHeavyOperative");
            AddSquad(nodes, "Chaos - assault (6)", "SCP_ChaosInsurgency", "SCP_ChaosInsurgent", "SCP_ChaosInsurgent");
            AddSquad(nodes, "Chaos - exosuits (6)", "SCP_ChaosInsurgency", "SCP_ChaosHeavy", "SCP_ChaosHeavy");
            AddSquad(nodes, "Chaos - mixed (6)", "SCP_ChaosInsurgency", "SCP_ChaosInsurgent", "SCP_ChaosHeavy");
            AddSquad(nodes, "Foundation Nu-7 (6)", "SCP_Foundation", "SCP_MTF_Nu7", "SCP_MTF_Nu7Gunner");
            AddSquad(nodes, "Foundation Epsilon-11 (6)", "SCP_Foundation", "SCP_MTF_Epsilon11Rifleman", "SCP_MTF_Epsilon11Gunner");
            AddSquad(nodes, "Foundation Omega-7 (6)", "SCP_Foundation", "SCP_MTF_Omega7Rifleman", "SCP_MTF_Omega7Gunner");
            nodes.Add(new DebugActionNode("Foundation Omega-7 + Vanguard (6+1)", DebugActionType.ToolMap,
                () => SpawnSquad("SCP_Foundation", "SCP_MTF_Omega7Rifleman", "SCP_MTF_Omega7Gunner", true)));
            AddSquad(nodes, "Church - mixed (6)", "SCP_ChurchBrokenGod", "SCP_BrokenGodZealot", "SCP_BrokenGodGuard");
            return nodes;
        }

        [DebugAction("SCP Foundation / Testing", "Spawn Vanguard at cursor", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SpawnVanguard() { ChimeraDebugActions.SpawnChimera(); }

        [DebugAction("SCP Foundation / Testing", "Offer SCP quest...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static List<DebugActionNode> QuestMenu()
        {
            return new List<DebugActionNode>
            {
                new DebugActionNode("SCP-079 recovery", action: ScpDebugActions.OfferScp079RecoveryQuest),
                new DebugActionNode("SCP-008 recovery", action: ScpDebugActions.OfferScp008RecoveryQuest),
                new DebugActionNode("SCP-035 recovery", action: ScpDebugActions.OfferScp035RecoveryQuest),
                new DebugActionNode("SCP-096 recovery", action: ScpDebugActions.OfferScp096RecoveryQuest),
                new DebugActionNode("SCP-106 recovery", action: ScpDebugActions.OfferScp106RecoveryQuest),
                new DebugActionNode("SCP-294 custody", action: ScpDebugActions.OfferScp294CustodyQuest),
                new DebugActionNode("SCP-914 custody", action: ScpDebugActions.OfferScp914CustodyQuest),
                new DebugActionNode("SCP-354 defense", action: ScpDebugActions.OfferScp354DefenseQuest),
                new DebugActionNode("SCP-882 workshop", action: ScpDebugActions.OfferScp882WorkshopQuest),
                new DebugActionNode("O5 Green visit", action: ScpDebugActions.TestO5GreenVisit),
                new DebugActionNode("Church cache", action: ScpDebugActions.TestChurchCipherCache),
                new DebugActionNode("Collector estate", action: ScpDebugActions.OfferCollectorEstateQuest)
            };
        }

        [DebugAction("SCP Foundation / Testing", "Add 10 Foundation trust", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void AddTrust() { ScpSliceDebugActions.AddFoundationTrust(); }

        [DebugAction("SCP Foundation / Testing", "Trigger SCP arrival...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static List<DebugActionNode> ArrivalMenu() { return ScpSliceDebugActions.TriggerArrivalMenu(); }

        private static IEnumerable<ThingDef> ModItems()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.category == ThingCategory.Item
                && !def.IsCorpse && def.thingClass != null && !def.thingClass.IsAbstract
                && typeof(Thing).IsAssignableFrom(def.thingClass)
                && def.modContentPack != null && def.modContentPack.PackageIdPlayerFacing == "local.scp5000.foundation")
                .OrderBy(def => def.defName);
        }

        private static void SpawnSingleItem(ThingDef def)
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null) return;
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map) || !ScpSpawnCells.FootprintClear(cell, map, def)) return;
            SpawnItem(def, cell, map);
        }

        private static void SpawnItem(ThingDef def, IntVec3 cell, Map map)
        {
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            if (def.MadeFromStuff && stuff == null) return;
            Thing item = ThingMaker.MakeThing(def, stuff);
            item.stackCount = 1;
            CompQuality quality = item.TryGetComp<CompQuality>();
            if (quality != null) quality.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
            GenSpawn.Spawn(item, cell, map);
            item.SetForbidden(false, false);
        }

        private static void AddSquad(List<DebugActionNode> nodes, string label, string faction, string regular, string heavy)
        {
            nodes.Add(new DebugActionNode(label, DebugActionType.ToolMap, () => SpawnSquad(faction, regular, heavy)));
        }

        private static void SpawnSquad(string factionName, string regularName, string heavyName,
            bool withVanguard = false)
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null || Find.FactionManager == null) return;
            IntVec3 center = UI.MouseCell();
            if (!center.InBounds(map)) return;
            PawnKindDef regular = DefDatabase<PawnKindDef>.GetNamedSilentFail(regularName);
            PawnKindDef heavy = DefDatabase<PawnKindDef>.GetNamedSilentFail(heavyName);
            PawnKindDef vanguard = withVanguard
                ? DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_FoundationChimera") : null;
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionName);
            if (regular == null || heavy == null || factionDef == null || withVanguard && vanguard == null) return;
            IntVec3 vanguardCell = IntVec3.Invalid;
            if (withVanguard)
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 8f, true))
                    if (ScpSpawnCells.FootprintClear(cell, map, vanguard.race))
                    { vanguardCell = cell; break; }
            if (withVanguard && !vanguardCell.IsValid) return;
            List<IntVec3> cells = GenRadial.RadialCellsAround(center, 8f, true)
                .Where(cell => cell != vanguardCell && ScpSpawnCells.FootprintClear(cell, map, regular.race)
                    && ScpSpawnCells.FootprintClear(cell, map, heavy.race)).Take(6).ToList();
            if (cells.Count < 6) return;
            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null)
            {
                FactionGenerator.CreateFactionAndAddToManager(factionDef);
                faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            }
            if (faction == null) return;
            Pawn_FoundationChimera machine = withVanguard
                ? PawnGenerator.GeneratePawn(vanguard, faction) as Pawn_FoundationChimera : null;
            if (withVanguard && machine == null) return;
            var pawns = new List<Pawn>();
            for (int i = 0; i < 6; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(i < 4 ? regular : heavy, faction);
                CombatExtendedLoadoutBridge.NormalizePrimaryAmmo(pawn);
                GenSpawn.Spawn(pawn, cells[i], map);
                pawns.Add(pawn);
            }
            if (machine != null)
            {
                GenSpawn.Spawn(machine, vanguardCell, map);
                machine.EnableAutonomousEscort(60000);
            }
            // A local defend lord keeps photo squads together without advancing any story operation.
            LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(center), map, pawns);
        }
    }
}
#endif
