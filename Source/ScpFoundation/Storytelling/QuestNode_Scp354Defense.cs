using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse.AI;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_Scp354DefenseSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            if (map == null || !map.IsPlayerHome || map.mapPawns.FreeColonistsSpawnedCount == 0
                || Current.Game == null) return false;
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            Faction faction = support.FoundationFaction;
            return support.progress.unlocked && faction != null && !faction.HostileTo(Faction.OfPlayer)
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("354")
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_354Defense") != null
                && DefDatabase<ThingDef>.GetNamedSilentFail("SCP_354Lake") != null;
        }

        protected override void RunInt()
        {
            QuestGen.slate.Set("siteFaction", Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_354Defense")
            });
        }
    }

    public sealed class QuestNode_Scp354DefenseClaimSite : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            Site site = QuestGen.slate.Get<Site>("site");
            Faction faction = QuestGen.slate.Get<Faction>("siteFaction");
            if (site != null && faction != null && site.Faction != faction)
                site.SetFaction(faction);
        }
    }

    public sealed class QuestNode_Scp354DefenseTrack : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp354Defense {
                site = QuestGen.slate.Get<Site>("site"),
                homeMap = QuestGen.slate.Get<Map>("map"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                completedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp354DefenseComplete"),
                failedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp354DefenseFailed")
            });
        }
    }

    public sealed class QuestPart_Scp354Defense : QuestPartActivable
    {
        private const int DurationTicks = 60000;
        private const int InitialWaveDelayTicks = 10000;
        private const int WaveIntervalTicks = 7500;
        private const int TotalWaves = 5;
        private const int AbandonGraceTicks = 2500;

        public Site site;
        public Map homeMap;
        public string completedSignal;
        public string failedSignal;
        private int startedTick = -1;
        private int wavesSpawned;
        private int nextWaveTick = -1;
        private int abandonedSinceTick = -1;
        private bool ended;
        private List<Pawn> attackers = new List<Pawn>();
        private List<Thing> tentacles = new List<Thing>();
        private Pawn iceCat;
        private Pawn magmaCat;
        private int magmaSpawnTick = -1;
        private int magmaCoolTick = -1;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scp354DefenseSite");
            Scribe_References.Look(ref homeMap, "scp354DefenseHomeMap");
            Scribe_Values.Look(ref completedSignal, "scp354DefenseCompletedSignal");
            Scribe_Values.Look(ref failedSignal, "scp354DefenseFailedSignal");
            Scribe_Values.Look(ref startedTick, "scp354DefenseStartedTick", -1);
            Scribe_Values.Look(ref wavesSpawned, "scp354DefenseWavesSpawned", 0);
            Scribe_Values.Look(ref nextWaveTick, "scp354DefenseNextWaveTick", -1);
            Scribe_Values.Look(ref abandonedSinceTick, "scp354DefenseAbandonedSince", -1);
            Scribe_Values.Look(ref ended, "scp354DefenseEnded", false);
            Scribe_Collections.Look(ref attackers, "scp354DefenseAttackers", LookMode.Reference);
            Scribe_Collections.Look(ref tentacles, "scp354DefenseTentacles", LookMode.Reference);
            Scribe_References.Look(ref iceCat, "scp354DefenseIceCat");
            Scribe_References.Look(ref magmaCat, "scp354DefenseMagmaCat");
            Scribe_Values.Look(ref magmaSpawnTick, "scp354DefenseMagmaSpawnTick", -1);
            Scribe_Values.Look(ref magmaCoolTick, "scp354DefenseMagmaCoolTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (attackers == null) attackers = new List<Pawn>();
                if (tentacles == null) tentacles = new List<Thing>();
            }
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || ended || site == null || Find.TickManager.TicksGame % 250 != 0)
                return;
            Map map = site.Map;
            int now = Find.TickManager.TicksGame;
            if (map == null)
            {
                if (startedTick >= 0)
                {
                    if (abandonedSinceTick < 0) abandonedSinceTick = now;
                    if (now - abandonedSinceTick >= AbandonGraceTicks) End(false);
                }
                return;
            }
            if (startedTick < 0)
            {
                if (map.mapPawns.FreeColonistsSpawnedCount == 0) return;
                startedTick = now;
                Find.LetterStack.ReceiveLetter("SCP_354DefenseStartedTitle".Translate(),
                    "SCP_354DefenseStartedBody".Translate(), LetterDefOf.PositiveEvent, site);
            }

            if (map.mapPawns.FreeColonistsSpawnedCount == 0)
            {
                if (abandonedSinceTick < 0) abandonedSinceTick = now;
                if (now - abandonedSinceTick >= AbandonGraceTicks) End(false);
                return;
            }
            abandonedSinceTick = -1;
            ThingDef lakeDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_354Lake");
            Building lake = lakeDef == null ? null : map.listerThings.ThingsOfDef(lakeDef).OfType<Building>().FirstOrDefault();
            if (lake == null) { End(false); return; }

            if (magmaSpawnTick >= 0 && now >= magmaSpawnTick)
            {
                magmaSpawnTick = -1;
                if (!SpawnMagmaCat(map, lake)) { End(false); return; }
            }
            UpdateMagmaCat(map, now);

            if (wavesSpawned < TotalWaves)
            {
                if (!ThreatsResolved(map)) nextWaveTick = -1;
                else
                {
                    if (nextWaveTick < 0)
                        nextWaveTick = wavesSpawned == 0
                            ? startedTick + InitialWaveDelayTicks : now + WaveIntervalTicks;
                    if (now >= nextWaveTick)
                    {
                        if (!SpawnWave(map, lake, wavesSpawned)) { End(false); return; }
                        wavesSpawned++;
                        nextWaveTick = -1;
                        Find.LetterStack.ReceiveLetter("SCP_354DefenseWaveTitle".Translate(wavesSpawned),
                            ("SCP_354DefenseWave" + wavesSpawned + "Body").Translate(), LetterDefOf.ThreatBig, lake);
                    }
                }
            }
            if (now - startedTick >= DurationTicks && wavesSpawned == TotalWaves
                && magmaSpawnTick < 0 && ThreatsResolved(map)
                && (magmaCat == null || magmaCat.Dead || !magmaCat.Spawned))
                End(true);
        }

        private bool ThreatsResolved(Map map)
        {
            return attackers.All(pawn => pawn == null || pawn.Dead || pawn.Downed
                    || !pawn.Spawned || pawn.Map != map)
                && tentacles.All(thing => thing == null || thing.Destroyed || !thing.Spawned || thing.Map != map);
        }

        private static List<IntVec3> LakeExits(Map map, Building lake)
        {
            CellRect footprint = GenAdj.OccupiedRect(lake.Position, Rot4.North, lake.def.size);
            List<IntVec3> exits = new List<IntVec3>();
            for (int x = footprint.minX - 3; x <= footprint.maxX + 3; x++)
                for (int z = footprint.minZ - 3; z <= footprint.maxZ + 3; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (footprint.Contains(cell) || !cell.InBounds(map) || !cell.Standable(map)
                        || cell.GetThingList(map).Any(thing => thing is Pawn || thing.def.category == ThingCategory.Building))
                        continue;
                    exits.Add(cell);
                }
            return exits;
        }

        private bool SpawnWave(Map map, Building lake, int wave)
        {
            List<IntVec3> exits = LakeExits(map, lake);
            if (exits.Count == 0) return false;
            if (wave == 2)
            {
                ThingDef tentacleDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_354Tentacles");
                if (tentacleDef == null) return false;
                // Each graphic contains five tentacles; spread three clusters along the shore.
                for (int i = 0; i < 3; i++)
                {
                    List<IntVec3> available = exits.Where(cell => tentacles.All(thing =>
                        thing == null || thing.Position.DistanceToSquared(cell) >= 16)).ToList();
                    if (available.Count == 0) break;
                    tentacles.Add(GenSpawn.Spawn(ThingMaker.MakeThing(tentacleDef),
                        available.RandomElement(), map));
                }
                return true;
            }
            string[] kinds = wave == 0 ? new[] { "SCP_354Bat", "SCP_354Bat" }
                : wave == 1 ? new[] { "SCP_354Spined", "SCP_354Spined" }
                : wave == 3 ? new[] { "SCP_354Reptile", "SCP_354Bat" }
                : new[] { "SCP_354IceCat" };
            List<IntVec3> used = new List<IntVec3>();
            foreach (string kindName in kinds)
            {
                List<IntVec3> available = exits.Where(exit =>
                    !exit.GetThingList(map).Any(thing => thing is Pawn)).ToList();
                if (available.Count == 0) return false;
                IntVec3 cell = used.Count == 0 ? available.RandomElement()
                    : available.OrderByDescending(candidate => used.Min(previous =>
                        candidate.DistanceToSquared(previous))).First();
                Pawn pawn = SpawnCreature(map, new List<IntVec3> { cell }, kindName, null, true);
                if (pawn == null) return false;
                attackers.Add(pawn);
                used.Add(cell);
                if (wave == 4)
                {
                    iceCat = pawn;
                    magmaSpawnTick = Find.TickManager.TicksGame + 2500;
                }
            }
            return true;
        }

        private static Pawn SpawnCreature(Map map, List<IntVec3> exits, string kindName,
            Faction faction, bool manhunter)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName);
            if (kind == null || exits.Count == 0) return null;
            Pawn pawn = faction == null ? PawnGenerator.GeneratePawn(kind) : PawnGenerator.GeneratePawn(kind, faction);
            GenSpawn.Spawn(pawn, exits.RandomElement(), map);
            if (manhunter)
                pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            return pawn;
        }

        private bool SpawnMagmaCat(Map map, Building lake)
        {
            Faction foundation = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            if (foundation == null) return false;
            magmaCat = SpawnCreature(map, LakeExits(map, lake), "SCP_354MagmaCat", foundation, false);
            if (magmaCat != null && iceCat != null && !iceCat.Dead)
                iceCat.mindState.mentalStateHandler.Reset();
            return magmaCat != null;
        }

        private void UpdateMagmaCat(Map map, int now)
        {
            if (magmaCat == null || magmaCat.Dead || !magmaCat.Spawned || magmaCat.Map != map)
                return;
            if (iceCat != null && iceCat.Spawned && !iceCat.Dead)
            {
                magmaCoolTick = -1;
                if (magmaCat.Downed) return;
                if (magmaCat.jobs != null && (magmaCat.CurJob == null
                    || magmaCat.CurJob.def != JobDefOf.AttackMelee || magmaCat.CurJob.targetA.Thing != iceCat))
                    magmaCat.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, iceCat),
                        JobCondition.InterruptForced);
                if (!iceCat.Downed && iceCat.jobs != null && (iceCat.CurJob == null
                    || iceCat.CurJob.def != JobDefOf.AttackMelee || iceCat.CurJob.targetA.Thing != magmaCat))
                    iceCat.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, magmaCat),
                        JobCondition.InterruptForced);
                if (iceCat.Position.DistanceToSquared(magmaCat.Position) <= 2)
                    iceCat.TakeDamage(new DamageInfo(DamageDefOf.Crush, 35f, 0f, -1f, magmaCat));
                return;
            }
            if (magmaCoolTick < 0) magmaCoolTick = now + 2500;
            if (now < magmaCoolTick) return;
            ThingDef statue = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_354MagmaStatue");
            IntVec3 cell = magmaCat.Position;
            magmaCat.Destroy(DestroyMode.Vanish);
            magmaCat = null;
            magmaCoolTick = -1;
            if (statue != null) GenSpawn.Spawn(ThingMaker.MakeThing(statue), cell, map);
        }

        private void End(bool success)
        {
            if (ended) return;
            ended = true;
            if (success)
            {
                Current.Game.GetComponent<ScpArrivalRegistry>()
                    .RecordContainmentResearchUnlock("SCP_262HangerResearch");
                FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
                support.progress.trust = Math.Min(FoundationSupportProgress.MaximumTrust, support.progress.trust + 10);
                Map destination = homeMap != null && homeMap.IsPlayerHome ? homeMap
                    : Find.Maps.FirstOrDefault(candidate => candidate.IsPlayerHome);
                FoundationExosuitTechprint.Deliver(destination);
                ThingDef coatDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_262Coat");
                if (destination != null && coatDef != null)
                {
                    IntVec3 dropSpot = DropCellFinder.TradeDropSpot(destination);
                    if (dropSpot.IsValid)
                        DropPodUtility.DropThingsNear(dropSpot, destination,
                            new List<Thing> { ThingMaker.MakeThing(coatDef) }, 110,
                            false, false, false, false, true, Faction.OfPlayer);
                }
            }
            Find.SignalManager.SendSignal(new Signal(success ? completedSignal : failedSignal));
        }
    }
}
