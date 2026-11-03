using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Containment;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class FoundationSupportComponent : GameComponent
    {
        public const int Nu7RequiredClearance = 3;
        public const int Nu7PawnCount = 7;
        public const int Nu7DurationTicks = 60000;
        public const int Epsilon11RequiredClearance = 4;
        public const int Epsilon11PawnCount = 12;
        public const int Epsilon11DurationTicks = 60000;
        private const string FoundationDefName = "SCP_" + "Foundation";
        private const string Nu7KindDefName = "SCP_" + "MTF_Nu7";
        private const string Nu7GunnerKindDefName = "SCP_" + "MTF_Nu7Gunner";
        private const string Epsilon11KindDefName = "SCP_" + "MTF_Epsilon11";
        private const string Epsilon11RiflemanKindDefName = "SCP_" + "MTF_Epsilon11Rifleman";
        private const string Epsilon11GunnerKindDefName = "SCP_" + "MTF_Epsilon11Gunner";
        private const string Nu7RifleDefName = "SCP_" + "Nu7HKCarbine";
        private const string Nu7MachineGunDefName = "SCP_" + "Nu7LightMachineGun";
        private const string Spas12DefName = "SCP_" + "FoundationSpasTwelve";
        private const string EmpRifleDefName = "SCP_" + "FoundationEMPRifle";
        public readonly FoundationSupportProgress progress = new FoundationSupportProgress();
        public FoundationSupportComponent(Game game) { }

        public override void LoadedGame()
        {
            foreach (Map map in Find.Maps)
                GenStep_Scp354Defense.RefreshExistingGuards(map);
            // Apparel color is saved on each item. Repair the old dark tint on
            // existing Foundation vests and Epsilon-11 gear after loading.
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn.apparel == null) continue;
                    bool epsilon11 = pawn.kindDef != null
                        && pawn.kindDef.defName.StartsWith("SCP_MTF_Epsilon11", StringComparison.Ordinal);
                    bool recolored = false;
                    foreach (Apparel apparel in pawn.apparel.WornApparel)
                    {
                        if (!epsilon11 && apparel.def.defName != "SCP_FoundationAssaultVest") continue;
                        Color color = apparel.DrawColor;
                        if (Math.Abs(color.r - 0.12f) > 0.02f || Math.Abs(color.g - 0.14f) > 0.02f
                            || Math.Abs(color.b - 0.16f) > 0.02f) continue;
                        apparel.DrawColor = Color.white;
                        recolored = true;
                    }
                    if (recolored) pawn.Drawer.renderer.SetAllGraphicsDirty();
                }
        }

        public bool EnsureUnlocked()
        {
            FoundationAssignmentComponent assignment = Current.Game.GetComponent<FoundationAssignmentComponent>();
            if (assignment.progress.stage != FoundationAssignmentStage.Completed) return progress.unlocked;
            progress.Unlock();
            return true;
        }

        private static bool Stable(CompScp scp)
        {
            return scp != null && scp.parent.Spawned && scp.parent.Map.IsPlayerHome && scp.Definition != null
                && scp.Definition.source != null && !scp.Definition.source.originalFixture && scp.IsInCustody
                && ScpOperationalPolicy.AllowsResearch(scp.OperationalFlags) && scp.CanResearchNow;
        }

        public int StableObjectCount
        {
            get
            {
                return Find.Maps.Where(map => map.IsPlayerHome)
                    .SelectMany(map => map.GetComponent<ScpStoryStateComponent>().Anomalies).Count(Stable);
            }
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 0) return;
            EnsureUnlocked();
            progress.Advance(now, StableObjectCount);
        }

        private static void AddStacks(List<Thing> things, ThingDef definition, int count)
        {
            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(definition);
                thing.stackCount = Math.Min(count, definition.stackLimit);
                things.Add(thing);
                count -= thing.stackCount;
            }
        }

        public bool RequestSupplies(Map map)
        {
            Thing terminal = CompFoundationAccessTerminal.Online(map);
            int now = Find.TickManager.TicksGame;
            if (terminal == null || !EnsureUnlocked() || !progress.CanRequestSupplies(now)) return false;

            // This is a Def name, not a keyed translation identifier.
            ThingDef concrete = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_" + "Concrete");
            if (concrete == null) return false;
            var things = new List<Thing>();
            AddStacks(things, ThingDefOf.Steel, 500);
            AddStacks(things, concrete, 300);
            AddStacks(things, ThingDefOf.ComponentIndustrial, 10);
            if (!progress.SpendOnSupplies(now))
            {
                foreach (Thing thing in things) thing.Destroy(DestroyMode.Vanish);
                return false;
            }

            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            if (!dropSpot.IsValid) dropSpot = terminal.Position;
            DropPodUtility.DropThingsNear(dropSpot, map, things, 110, false, false, false, false, true, Faction.OfPlayer);
            Find.LetterStack.ReceiveLetter("SCP_SupportDeliveredTitle".Translate(), "SCP_SupportDeliveredBody".Translate(),
                LetterDefOf.PositiveEvent, new TargetInfo(dropSpot, map));
            return true;
        }

        public bool NextAnomalyDiscovered
        {
            get
            {
                return Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(ScpInteractionDefOf.SCP_Anomaly055);
            }
        }

        public bool RequestNextAnomaly(Map map)
        {
            if (CompFoundationAccessTerminal.Online(map) == null || !EnsureUnlocked()
                || !progress.CanRequestDossier(NextAnomalyDiscovered)) return false;
            if (Scp055Delivery.ReadyPost(map) == null)
            {
                Messages.Message("SCP_055CargoNeedRoom".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            if (!ScpInteractionDefOf.SCP_Discover055.Worker.TryExecute(parms)) return false;
            if (!progress.SpendOnDossier(false)) return false;
            Messages.Message("SCP_SupportAnomalyRequested".Translate(), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public Faction FoundationFaction
        {
            get
            {
                FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(FoundationDefName);
                return definition == null || Find.FactionManager == null ? null : Find.FactionManager.FirstFactionOfDef(definition);
            }
        }

        public bool CanRequestNu7(Map map, out string reason)
        {
            reason = null;
            if (map == null || CompFoundationAccessTerminal.Online(map) == null) { reason = "SCP_SupportOffline".Translate(); return false; }
            if (!EnsureUnlocked()) { reason = "SCP_SupportLocked".Translate(); return false; }
            Faction faction = FoundationFaction;
            if (faction == null) { reason = "SCP_SupportSecurityNoFaction".Translate(); return false; }
            if (faction.HostileTo(Faction.OfPlayer)) { reason = "SCP_SupportSecurityHostile".Translate(); return false; }
            int clearance = Current.Game.GetComponent<FoundationPersonnelComponent>().HighestLevelOn(map);
            if (clearance < Nu7RequiredClearance) { reason = "SCP_SupportSecurityClearance".Translate(Nu7RequiredClearance, clearance); return false; }
            int now = Find.TickManager.TicksGame;
            int cooldown = progress.SecurityCooldownRemaining(now);
            if (cooldown > 0) { reason = "SCP_SupportSecurityCooldown".Translate((cooldown / 60000f).ToString("0.0")); return false; }
            if (progress.trust < FoundationSupportProgress.SecurityTrustCost)
            { reason = "SCP_SupportInsufficient".Translate(FoundationSupportProgress.SecurityTrustCost); return false; }
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Nu7KindDefName);
            PawnKindDef gunner = DefDatabase<PawnKindDef>.GetNamedSilentFail(Nu7GunnerKindDefName);
            if (kind == null || gunner == null
                || DefDatabase<ThingDef>.GetNamedSilentFail(Nu7RifleDefName) == null
                || DefDatabase<ThingDef>.GetNamedSilentFail(Nu7MachineGunDefName) == null
                )
            { reason = "SCP_SupportSecurityUnavailable".Translate(); return false; }
            reason = "SCP_SupportSecurityReady".Translate(FoundationSupportProgress.SecurityTrustCost, Nu7PawnCount);
            return true;
        }

        public bool RequestNu7(Map map)
        {
            string ignored;
            if (!CanRequestNu7(map, out ignored)) return false;
            IntVec3 entry;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out entry, map, 0f, false, cell => cell.Standable(map))) return false;
            int now = Find.TickManager.TicksGame;
            if (!progress.SpendOnSecurity(now)) return false;
            return SpawnNu7(map, entry, true);
        }

        public bool DebugSpawnNu7(Map map, IntVec3 entry)
        {
            return EnsureFoundationFactionForDebug() != null && SpawnNu7(map, entry, false);
        }

        private bool SpawnNu7(Map map, IntVec3 entry, bool sendLetter)
        {
            Faction faction = FoundationFaction;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Nu7KindDefName);
            PawnKindDef gunner = DefDatabase<PawnKindDef>.GetNamedSilentFail(Nu7GunnerKindDefName);
            ThingDef rifle = DefDatabase<ThingDef>.GetNamedSilentFail(Nu7RifleDefName);
            ThingDef machineGun = DefDatabase<ThingDef>.GetNamedSilentFail(Nu7MachineGunDefName);
            if (map == null || faction == null || kind == null || gunner == null || rifle == null || machineGun == null
                || !entry.InBounds(map)) return false;
            var pawns = new List<Pawn>();
            for (int i = 0; i < 6; i++) pawns.Add(GenerateEquippedPawn(kind, faction, rifle));
            pawns.Add(GenerateEquippedPawn(gunner, faction, machineGun));
            SpawnSupportTeam(map, entry, faction, pawns, Nu7DurationTicks);
            if (sendLetter)
                Find.LetterStack.ReceiveLetter("SCP_SupportSecurityArrivedTitle".Translate(),
                    "SCP_SupportSecurityArrivedBody".Translate(Nu7PawnCount), LetterDefOf.PositiveEvent,
                    new TargetInfo(entry, map));
            return true;
        }

        public bool CanRequestEpsilon11(Map map, out string reason)
        {
            reason = null;
            if (map == null || CompFoundationAccessTerminal.Online(map) == null) { reason = "SCP_SupportOffline".Translate(); return false; }
            if (!EnsureUnlocked()) { reason = "SCP_SupportLocked".Translate(); return false; }
            Faction faction = FoundationFaction;
            if (faction == null) { reason = "SCP_SupportSecurityNoFaction".Translate(); return false; }
            if (faction.HostileTo(Faction.OfPlayer)) { reason = "SCP_SupportSecurityHostile".Translate(); return false; }
            int clearance = Current.Game.GetComponent<FoundationPersonnelComponent>().HighestLevelOn(map);
            if (clearance < Epsilon11RequiredClearance) { reason = "SCP_SupportSecurityClearance".Translate(Epsilon11RequiredClearance, clearance); return false; }
            int now = Find.TickManager.TicksGame;
            int cooldown = progress.Epsilon11CooldownRemaining(now);
            if (cooldown > 0) { reason = "SCP_SupportEpsilonCooldown".Translate((cooldown / 60000f).ToString("0.0")); return false; }
            if (progress.trust < FoundationSupportProgress.Epsilon11TrustCost)
            { reason = "SCP_SupportInsufficient".Translate(FoundationSupportProgress.Epsilon11TrustCost); return false; }
            PawnKindDef specialist = DefDatabase<PawnKindDef>.GetNamedSilentFail(Epsilon11KindDefName);
            PawnKindDef rifleman = DefDatabase<PawnKindDef>.GetNamedSilentFail(Epsilon11RiflemanKindDefName);
            PawnKindDef gunner = DefDatabase<PawnKindDef>.GetNamedSilentFail(Epsilon11GunnerKindDefName);
            if (specialist == null || rifleman == null || gunner == null
                || DefDatabase<ThingDef>.GetNamedSilentFail(EmpRifleDefName) == null
                || DefDatabase<ThingDef>.GetNamedSilentFail(Nu7RifleDefName) == null
                || DefDatabase<ThingDef>.GetNamedSilentFail(Nu7MachineGunDefName) == null)
            { reason = "SCP_SupportEpsilonUnavailable".Translate(); return false; }
            reason = "SCP_SupportEpsilonReady".Translate(FoundationSupportProgress.Epsilon11TrustCost, Epsilon11PawnCount);
            return true;
        }

        public bool RequestEpsilon11(Map map)
        {
            string ignored;
            if (!CanRequestEpsilon11(map, out ignored)) return false;
            IntVec3 entry;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out entry, map, 0f, false, cell => cell.Standable(map))) return false;
            int now = Find.TickManager.TicksGame;
            if (!progress.SpendOnEpsilon11(now)) return false;
            return SpawnEpsilon11(map, entry, true);
        }

        public bool DebugSpawnEpsilon11(Map map, IntVec3 entry)
        {
            return EnsureFoundationFactionForDebug() != null && SpawnEpsilon11(map, entry, false);
        }

        public Pawn EmergencyEpsilon11(Map map)
        {
            if (map == null || EnsureFoundationFactionForDebug() == null) return null;
            IntVec3 entry;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out entry, map, 0f, false,
                cell => cell.Standable(map))) return null;
            var before = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
            if (!SpawnEpsilon11(map, entry, false)) return null;
            Pawn medic = map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn =>
                !before.Contains(pawn) && pawn.kindDef != null
                && pawn.kindDef.defName == Epsilon11KindDefName);
            ThingDef pillDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_500Pill");
            if (medic != null && medic.inventory != null && pillDef != null)
            {
                Thing pills = ThingMaker.MakeThing(pillDef);
                pills.stackCount = 3;
                if (!medic.inventory.innerContainer.TryAdd(pills)) pills.Destroy(DestroyMode.Vanish);
            }
            Find.LetterStack.ReceiveLetter("SCP_EmergencyEpsilonTitle".Translate(),
                "SCP_EmergencyEpsilonBody".Translate(), LetterDefOf.PositiveEvent,
                new TargetInfo(entry, map));
            return medic;
        }

        private Faction EnsureFoundationFactionForDebug()
        {
            Faction faction = FoundationFaction;
            if (faction != null) return faction;
            FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(FoundationDefName);
            if (definition == null || Find.FactionManager == null) return null;
            FactionGenerator.CreateFactionAndAddToManager(definition);
            return FoundationFaction;
        }

        private bool SpawnEpsilon11(Map map, IntVec3 entry, bool sendLetter)
        {
            Faction faction = FoundationFaction;
            PawnKindDef specialist = DefDatabase<PawnKindDef>.GetNamedSilentFail(Epsilon11KindDefName);
            PawnKindDef rifleman = DefDatabase<PawnKindDef>.GetNamedSilentFail(Epsilon11RiflemanKindDefName);
            PawnKindDef gunner = DefDatabase<PawnKindDef>.GetNamedSilentFail(Epsilon11GunnerKindDefName);
            ThingDef empRifle = DefDatabase<ThingDef>.GetNamedSilentFail(EmpRifleDefName);
            ThingDef rifle = DefDatabase<ThingDef>.GetNamedSilentFail(Nu7RifleDefName);
            ThingDef machineGun = DefDatabase<ThingDef>.GetNamedSilentFail(Nu7MachineGunDefName);
            if (map == null || faction == null || specialist == null || rifleman == null || gunner == null || empRifle == null
                || rifle == null || machineGun == null || !entry.InBounds(map)) return false;
            var pawns = new List<Pawn>();
            for (int i = 0; i < 6; i++) pawns.Add(GenerateEquippedPawn(rifleman, faction, rifle));
            for (int i = 0; i < 3; i++) pawns.Add(GenerateEquippedPawn(gunner, faction, machineGun));
            for (int i = 0; i < 3; i++) pawns.Add(GenerateEquippedPawn(specialist, faction, empRifle));
            SpawnSupportTeam(map, entry, faction, pawns, Epsilon11DurationTicks);
            if (sendLetter)
                Find.LetterStack.ReceiveLetter("SCP_SupportEpsilonArrivedTitle".Translate(),
                    "SCP_SupportEpsilonArrivedBody".Translate(Epsilon11PawnCount), LetterDefOf.PositiveEvent,
                    new TargetInfo(entry, map));
            return true;
        }

        internal static Pawn GenerateEquippedPawn(PawnKindDef kind, Faction faction, ThingDef weaponDef, Pawn existingPawn = null)
        {
            Pawn pawn = existingPawn ?? PawnGenerator.GeneratePawn(kind, faction);
            if (kind.defName.StartsWith("SCP_MTF_", StringComparison.Ordinal)
                && !pawn.apparel.WornApparel.Any(item => item.def.defName == "SCP_MTFNavyBackpack"
                    || item.def.defName == "SCP_MTFBlackBackpack"))
            {
                string backpackName = Rand.Range(0, 2) == 0 ? "SCP_MTFNavyBackpack" : "SCP_MTFBlackBackpack";
                ThingDef backpackDef = DefDatabase<ThingDef>.GetNamedSilentFail(backpackName);
                if (backpackDef != null)
                    pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(backpackDef));
            }
            // Exact per-role weapon tags let CE create the matching magazines/loadout.
            // Preserve that generated weapon instead of replacing it and leaving stale ammunition behind.
            if (pawn.equipment.Primary != null && pawn.equipment.Primary.def == weaponDef)
            {
                CombatExtendedLoadoutBridge.NormalizePrimaryAmmo(pawn);
                return pawn;
            }
            pawn.equipment.DestroyAllEquipment(DestroyMode.Vanish);
            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef) as ThingWithComps;
            if (weapon == null) return pawn;
            CompQuality quality = weapon.TryGetComp<CompQuality>();
            if (quality != null) quality.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
            pawn.equipment.AddEquipment(weapon);
            CombatExtendedLoadoutBridge.NormalizePrimaryAmmo(pawn);
            return pawn;
        }

        internal static void SpawnSupportTeam(Map map, IntVec3 entry, Faction faction, List<Pawn> pawns,
            int durationTicks, bool holdUntilReleased = false)
        {
            int now = Find.TickManager.TicksGame;

            foreach (Faction hostile in map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction != null && pawn.HostileTo(Faction.OfPlayer))
                .Select(pawn => pawn.Faction).Distinct().ToList())
                if (hostile != faction && !faction.HostileTo(hostile))
                    ScpFactionRelationsComponent.EnsureHostile(faction, hostile);

            foreach (Pawn pawn in pawns)
            {
                IntVec3 cell = CellFinder.RandomSpawnCellForPawnNear(entry, map, 6);
                GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
                pawn.mindState.exitMapAfterTick = now + durationTicks;
            }
            LordMaker.MakeNewLord(faction,
                holdUntilReleased ? (LordJob)new LordJob_OuroborosDefense(entry)
                    : new LordJob_AssistColony(faction, entry), map, pawns);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref progress.trust, "scpFoundationTrust", 0);
            Scribe_Values.Look(ref progress.stableObjectTicks, "scpFoundationTrustTicks", 0);
            Scribe_Values.Look(ref progress.lastTick, "scpFoundationTrustLastTick", -1);
            Scribe_Values.Look(ref progress.nextSupplyTick, "scpFoundationSupplyNextTick", -1);
            Scribe_Values.Look(ref progress.supplyRequests, "scpFoundationSupplyRequests", 0);
            Scribe_Values.Look(ref progress.unlocked, "scpFoundationSupportUnlocked", false);
            Scribe_Values.Look(ref progress.dossierRequested, "scpFoundationDossierRequested", false);
            Scribe_Values.Look(ref progress.nextSecurityTick, "scpFoundationSecurityNextTick", -1);
            Scribe_Values.Look(ref progress.securityRequests, "scpFoundationSecurityRequests", 0);
            Scribe_Values.Look(ref progress.nextEpsilon11Tick, "scpFoundationEpsilon11NextTick", -1);
            Scribe_Values.Look(ref progress.epsilon11Requests, "scpFoundationEpsilon11Requests", 0);
        }
    }
}
