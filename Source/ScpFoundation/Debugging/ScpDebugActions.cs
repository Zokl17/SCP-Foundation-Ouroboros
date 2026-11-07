using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Debugging
{
    public static class ScpDebugActions
    {
        public static void OfferOuroborosOpening()
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null || !map.IsPlayerHome) return;
            bool offered = Current.Game.GetComponent<FoundationContactComponent>().OfferOuroboros(map, true);
            Messages.Message((offered ? "SCP_OuroborosOffered" : "SCP_OuroborosUnavailable").Translate(),
                offered ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, false);
        }

        public static void TestChurchCipherCache()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                .UnlockSignalRecovery(Find.TickManager.TicksGame);
            Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                .UnlockSignalAnalysis(Find.TickManager.TicksGame);
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_OuroborosChurchCache");
            if (script == null || Find.QuestManager.QuestsListForReading.Any(quest => quest.root == script)) return;
            QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
        }

        public static void TestO5GreenVisit()
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null || !map.IsPlayerHome) return;
            ScpOuroborosArchiveComponent archive = Current.Game.GetComponent<ScpOuroborosArchiveComponent>();
            archive.UnlockAnalysis(Find.TickManager.TicksGame);
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_O5GreenVisit");
            bool existing = script != null && Find.QuestManager.QuestsListForReading.Any(quest =>
                quest.root == script && (quest.State == QuestState.NotYetAccepted
                    || quest.State == QuestState.Ongoing));
            Quest offered = existing || script == null ? null
                : QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            Messages.Message((existing || offered != null ? "SCP_O5GreenDebugOffered"
                : "SCP_O5GreenDebugUnavailable").Translate(),
                existing || offered != null ? MessageTypeDefOf.PositiveEvent
                    : MessageTypeDefOf.RejectInput, false);
        }

        public static void OfferChaosSalvageRaidQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_ChaosSalvageRaid");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 4000f);
            Messages.Message((quest == null ? "SCP_ChaosRaidQuestUnavailable" : "SCP_ChaosRaidQuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp882WorkshopQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_882WorkshopRaid");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            Messages.Message((quest == null ? "SCP_882QuestUnavailable" : "SCP_882QuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp079RecoveryQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_079Recovery");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 600f);
            Messages.Message((quest == null ? "SCP_079QuestUnavailable" : "SCP_079QuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp008RecoveryQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_008Recovery");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 1200f);
            Messages.Message((quest == null ? "SCP_008QuestUnavailable" : "SCP_008QuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp035RecoveryQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_035Recovery");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 400f);
            Messages.Message((quest == null ? "SCP_035QuestUnavailable" : "SCP_035QuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp096RecoveryQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_096Recovery");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 600f);
            Messages.Message((quest == null ? "SCP_096QuestUnavailable" : "SCP_096QuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferCollectorEstateQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_CollectorEstateRecovery");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 400f);
            Messages.Message((quest == null ? "SCP_CollectorQuestUnavailable" : "SCP_CollectorQuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp330VillageQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_330VillageRecovery");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 600f);
            Messages.Message((quest == null ? "SCP_330VillageUnavailable" : "SCP_330VillageOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp354DefenseQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_354PerimeterDefense");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            Messages.Message((quest == null ? "SCP_354DefenseUnavailable" : "SCP_354DefenseOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp106RecoveryQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_106Recovery");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            Messages.Message((quest == null ? "SCP_106QuestUnavailable" : "SCP_106QuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp294CustodyQuest()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || !Find.CurrentMap.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_294Custody");
            Quest quest = script == null ? null : QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            Messages.Message((quest == null ? "SCP_294QuestUnavailable" : "SCP_294QuestOffered").Translate(),
                quest == null ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
        }

        public static void OfferScp914CustodyQuest()
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null || !map.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_914Custody");
            bool pending = script != null && Find.QuestManager.QuestsListForReading.Any(quest =>
                quest.root == script && (quest.State == QuestState.NotYetAccepted
                    || quest.State == QuestState.Ongoing));
            Quest offered = pending || script == null ? null
                : QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            Messages.Message((pending || offered != null ? "SCP_914QuestOffered"
                    : "SCP_914QuestUnavailable").Translate(),
                pending || offered != null ? MessageTypeDefOf.PositiveEvent
                    : MessageTypeDefOf.RejectInput, false);
        }

        public static void OfferScp682Transfer() { OfferCreatureTransfer("682"); }

        public static void OfferScp939Transfer() { OfferCreatureTransfer("939"); }

        private static void OfferCreatureTransfer(string species)
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null || !map.IsPlayerHome) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_" + species + "Transfer");
            Quest quest = script == null ? null : Find.QuestManager.QuestsListForReading
                .FirstOrDefault(candidate => candidate.root == script && candidate.State == QuestState.NotYetAccepted
                    && candidate.PartsListForReading.OfType<QuestPart_ScpCreatureTransfer>()
                        .Any(part => part.map == map));
            if (quest == null && script != null) quest = QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f);
            if (quest == null)
            {
                Messages.Message("SCP_CreatureTransferUnavailable".Translate(species), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message("SCP_CreatureTransferOffered".Translate(species), MessageTypeDefOf.PositiveEvent, false);
        }

        // DebugActionAttribute menu identifiers follow the game's English developer convention.
        public static void SpawnTestFixture()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            // Reject occupied cells instead of allowing GenSpawn to wipe existing objects.
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map) || cell.GetThingList(map).Count != 0)
            {
                Messages.Message("SCP_SpawnDenied".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamed("SCP_TestFixture");
            Thing fixture = ThingMaker.MakeThing(def);
            fixture.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(fixture, cell, map);
        }

        [DebugAction("SCP Foundation", "Spawn SCP-008 sample at cursor", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SpawnScp008SampleCase()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            ThingDef sampleDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_008SampleCase");
            if (!map.IsPlayerHome || sampleDef == null || !cell.InBounds(map)
                || !cell.Standable(map) || cell.Fogged(map))
            {
                Messages.Message("SCP_SpawnDenied".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Thing sample = ThingMaker.MakeThing(sampleDef);
            if (!GenPlace.TryPlaceThing(sample, cell, map, ThingPlaceMode.Near))
            {
                sample.Destroy(DestroyMode.Vanish);
                Messages.Message("SCP_SpawnDenied".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message("SCP_008DebugCaseSpawned".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }

        public static void SpawnNu7Team()
        {
            SpawnSupportTeam(true);
        }

        public static void SpawnEpsilon11Team()
        {
            SpawnSupportTeam(false);
        }

        public static void CallMarshallCarterDarkTrader()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            TraderKindDef trader = DefDatabase<TraderKindDef>.GetNamedSilentFail("SCP_Orbital_MarshallCarterDark");
            if (trader == null)
            {
                Messages.Message("SCP_McdTraderUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.CurrentMap.passingShipManager.AddShip(new TradeShip(trader, null));
            Messages.Message("SCP_McdTraderArrived".Translate(trader.LabelCap), MessageTypeDefOf.PositiveEvent, false);
        }

        public static void SpawnGocStrikeTeam()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || Current.Game == null) return;
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            GocOperationsComponent operations = Current.Game.GetComponent<GocOperationsComponent>();
            if (!cell.InBounds(map) || !operations.DebugSpawnStrikeTeam(map, cell))
            {
                Messages.Message("SCP_GocStrikeUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message("SCP_GocStrikeArrived".Translate(6), MessageTypeDefOf.PositiveEvent, false);
        }

        public static void StartGocEliminationOperation()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || Current.Game == null) return;
            bool scheduled = Current.Game.GetComponent<GocOperationsComponent>().DebugScheduleOperation(Find.CurrentMap);
            if (!scheduled)
                Messages.Message("SCP_GocOperationUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        public static void StartChaosTheftOperation()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || Current.Game == null) return;
            bool started = Current.Game.GetComponent<ChaosInsurgencyOperationsComponent>()
                .DebugStartOperation(Find.CurrentMap);
            if (!started)
                Messages.Message("SCP_ChaosTheftUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        public static void SpawnFifteenChaosRaiders()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || Current.Game == null) return;
            bool spawned = Current.Game.GetComponent<ChaosInsurgencyOperationsComponent>()
                .DebugSpawnFifteen(Find.CurrentMap, UI.MouseCell());
            Messages.Message(spawned ? "Spawned 15 Chaos Insurgency raiders." : "Cannot spawn 15 Chaos raiders here.",
                spawned ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, false);
        }

        public static void SpawnBrokenGodTrio()
        {
            Map map = Find.CurrentMap;
            IntVec3 center = UI.MouseCell();
            if (!Prefs.DevMode || map == null || !center.InBounds(map) || Find.FactionManager == null) return;
            FactionDef churchDef = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_ChurchBrokenGod");
            PawnKindDef zealot = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_BrokenGodZealot");
            PawnKindDef cantor = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_BrokenGodCantor");
            PawnKindDef guard = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_BrokenGodGuard");
            if (churchDef == null || zealot == null || cantor == null || guard == null) return;
            Faction church = Find.FactionManager.FirstFactionOfDef(churchDef);
            if (church == null)
            {
                FactionGenerator.CreateFactionAndAddToManager(churchDef);
                church = Find.FactionManager.FirstFactionOfDef(churchDef);
            }
            if (church == null) return;
            List<IntVec3> cells = GenRadial.RadialCellsAround(center, 5f, true)
                .Where(cell => cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map)
                    && !cell.GetThingList(map).Any(thing => thing is Pawn
                        || thing.def.category == ThingCategory.Building))
                .Take(3).ToList();
            if (cells.Count < 3) return;
            var pawns = new List<Pawn>(3);
            foreach (PawnKindDef kind in new[] { zealot, cantor, guard })
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, church);
                if (kind == cantor)
                {
                    HediffDef ritual = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_BrokenGodCanticle");
                    if (ritual != null) pawn.health.AddHediff(ritual);
                }
                CombatExtendedLoadoutBridge.NormalizePrimaryAmmo(pawn);
                pawns.Add(pawn);
            }
            ScpFactionRelationsComponent.EnsureHostile(church, Faction.OfPlayer);
            for (int i = 0; i < pawns.Count; i++) GenSpawn.Spawn(pawns[i], cells[i], map);
            LordMaker.MakeNewLord(church,
                new LordJob_AssaultColony(church, false, true, false, false, false, false, false), map, pawns);
        }

        public static void StartFullBrokenGodRaid()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || Current.Game == null) return;
            bool started = Current.Game.GetComponent<Scp882OuroborosSiegeComponent>()
                .DebugSpawnChurchRaid(Find.CurrentMap);
            Messages.Message((started ? "SCP_ChurchDebugRaidStarted" : "SCP_ChurchDebugRaidUnavailable").Translate(),
                started ? MessageTypeDefOf.ThreatBig : MessageTypeDefOf.RejectInput, false);
        }

        public static List<DebugActionNode> PlaceholderFactionRaidMenu()
        {
            return new List<DebugActionNode>
            {
                new DebugActionNode("Chaos Insurgency", action: () => StartPlaceholderRaid("SCP_" + "ChaosInsurgency")),
                new DebugActionNode("Church of the Broken God", action: () => StartPlaceholderRaid("SCP_" + "ChurchBrokenGod")),
                new DebugActionNode("Sarkic cults", action: () => StartPlaceholderRaid("SCP_" + "SarkicCults")),
                new DebugActionNode("Serpent's Hand", action: () => StartPlaceholderRaid("SCP_" + "SerpentsHand"))
            };
        }

        private static void StartPlaceholderRaid(string factionDefName)
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || Find.FactionManager == null) return;
            FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (definition == null) { RejectPlaceholderRaid(); return; }
            Faction faction = Find.FactionManager.FirstFactionOfDef(definition);
            if (faction == null)
            {
                FactionGenerator.CreateFactionAndAddToManager(definition);
                faction = Find.FactionManager.FirstFactionOfDef(definition);
            }
            if (faction == null) { RejectPlaceholderRaid(); return; }
            ScpFactionRelationsComponent.EnsureHostileRelations();
            ScpFactionRelationsComponent.EnsureHostile(faction, Faction.OfPlayer);
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, Find.CurrentMap);
            parms.faction = faction;
            parms.points = Math.Max(500f, StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap));
            parms.forced = true;
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms)) RejectPlaceholderRaid();
        }

        private static void RejectPlaceholderRaid()
        {
            Messages.Message("SCP_PlaceholderRaidUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        private static void SpawnSupportTeam(bool nu7)
        {
            if (!Prefs.DevMode || Find.CurrentMap == null || Current.Game == null) return;
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            bool spawned = cell.InBounds(map) && (nu7
                ? support.DebugSpawnNu7(map, cell)
                : support.DebugSpawnEpsilon11(map, cell));
            if (!spawned)
            {
                Messages.Message((nu7 ? "SCP_SupportSecurityUnavailable" : "SCP_SupportEpsilonUnavailable").Translate(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message((nu7 ? "SCP_SupportSecurityArrivedTitle" : "SCP_SupportEpsilonArrivedTitle").Translate(),
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
