using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using ScpFoundation.Definitions;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_ScpChurchCacheSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return slate.Get<Map>("map") != null && Faction.OfAncientsHostile != null
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().SignalRecoveryTick >= 0
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().SignalAnalysisTick >= 0;
        }

        protected override void RunInt()
        {
            FactionDef churchDef = DefDatabase<FactionDef>.GetNamed("SCP_ChurchBrokenGod");
            Faction church = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(faction => faction.def == churchDef && !faction.defeated);
            QuestGen.slate.Set("siteFaction", church ?? Faction.OfAncientsHostile);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_OuroborosChurchCachePart")
            });
        }
    }

    public sealed class QuestNode_ScpChurchCacheTrack : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_ScpChurchCache {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpChurchCipherRecovered"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpChurchCipherLost")
            });
        }
    }

    public sealed class QuestPart_ScpChurchCache : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal;
        public string lostSignal;
        private Thing core;
        private Thing sample217;
        private Thing components629;
        private bool coreRead;
        private bool sampleSecured;
        private bool componentsSecured;
        private bool legacyCache;
        private bool entered;
        private bool finished;
        private bool cargoUnloaded;

        public static QuestPart_ScpChurchCache ActiveOn(Map map)
        {
            if (map == null || Find.QuestManager == null) return null;
            return Find.QuestManager.QuestsListForReading
                .Where(quest => quest.State == QuestState.Ongoing && quest.root != null
                    && quest.root.defName == "SCP_OuroborosChurchCache")
                .SelectMany(quest => quest.PartsListForReading.OfType<QuestPart_ScpChurchCache>())
                .FirstOrDefault(part => part.site != null && part.site.Map == map
                    && part.State == QuestPartState.Enabled && !part.finished);
        }

        public void ReadCore(Pawn pawn, Thing source)
        {
            if (finished || pawn == null || !pawn.IsColonistPlayerControlled
                || site == null || pawn.Map != site.Map || source == null || !source.Spawned) return;
            if (source != core && source != sample217 && source != components629) return;
            if (source == core)
            {
                source.Destroy(DestroyMode.Vanish);
                coreRead = true;
            }
            else
            {
                if (pawn.inventory == null) return;
                Map sourceMap = source.Map;
                IntVec3 sourceCell = source.Position;
                source.DeSpawn();
                if (!pawn.inventory.innerContainer.TryAdd(source))
                {
                    GenSpawn.Spawn(source, sourceCell, sourceMap);
                    Messages.Message("SCP_ChurchCargoCarryFailed".Translate(), pawn,
                        MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (source == sample217) sampleSecured = true;
                else componentsSecured = true;
            }
            if (legacyCache)
            {
                FinishSuccess();
                return;
            }
            int completed = (coreRead ? 1 : 0) + (sampleSecured ? 1 : 0)
                + (componentsSecured ? 1 : 0);
            Messages.Message("SCP_ChurchObjectiveProgress".Translate(completed), pawn,
                MessageTypeDefOf.NeutralEvent, false);
        }

        private void FinishSuccess()
        {
            finished = true;
            if (!legacyCache)
            {
                bool samplePlaced = DropRecoveredCargo(sample217);
                bool partsPlaced = DropRecoveredCargo(components629);
                cargoUnloaded = samplePlaced && partsPlaced;
            }
            Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                .UnlockChurchCache(Find.TickManager.TicksGame, !legacyCache);
            Find.SignalManager.SendSignal(new Signal(recoveredSignal));
        }

        private static bool DropRecoveredCargo(Thing cargo)
        {
            if (cargo == null || cargo.Destroyed || cargo.Spawned) return true;
            foreach (Map map in Find.Maps.Where(candidate => candidate.IsPlayerHome))
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                    if (pawn.inventory != null && pawn.inventory.innerContainer.Contains(cargo))
                    {
                        Thing dropped;
                        pawn.inventory.innerContainer.TryDrop(cargo, pawn.Position, map,
                            ThingPlaceMode.Near, out dropped);
                        return dropped != null;
                    }
            return false;
        }

        public override void QuestPartTick()
        {
            if (finished && !legacyCache && !cargoUnloaded
                && Find.TickManager.TicksGame % 250 == 0)
            {
                bool samplePlaced = DropRecoveredCargo(sample217);
                bool partsPlaced = DropRecoveredCargo(components629);
                cargoUnloaded = samplePlaced && partsPlaced;
            }
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 60 != 0) return;
            Map siteMap = site == null ? null : site.Map;
            if (siteMap != null && !entered)
            {
                entered = true;
                ThingDef def = DefDatabase<ThingDef>.GetNamed("SCP_OuroborosChurchCipher");
                core = siteMap.listerThings.ThingsOfDef(def).FirstOrDefault();
                sample217 = siteMap.listerThings.ThingsOfDef(DefDatabase<ThingDef>
                    .GetNamed("SCP_OuroborosSealed217Sample")).FirstOrDefault();
                components629 = siteMap.listerThings.ThingsOfDef(DefDatabase<ThingDef>
                    .GetNamed("SCP_OuroborosParts629Cache")).FirstOrDefault();
                // Sites generated by an older version only contain the cipher.
                legacyCache = sample217 == null && components629 == null;
                if (core == null) { FinishLost(); return; }
            }
            if (entered && ((!coreRead && (core == null || core.Destroyed))
                || (!legacyCache && ((sample217 == null || sample217.Destroyed)
                    || (components629 == null || components629.Destroyed)))))
            { FinishLost(); return; }
            if (!legacyCache && coreRead && sampleSecured && componentsSecured
                && Find.Maps.Any(map => map.IsPlayerHome
                    && CargoAtHome(map, sample217) && CargoAtHome(map, components629)))
                FinishSuccess();
        }

        private static bool CargoAtHome(Map map, Thing cargo)
        {
            return cargo != null && !cargo.Destroyed
                && ((cargo.Spawned && cargo.Map == map)
                    || map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.Faction == Faction.OfPlayer
                        && pawn.inventory != null
                        && pawn.inventory.innerContainer.Any(thing => thing == cargo)));
        }

        private void FinishLost()
        {
            finished = true;
            Messages.Message("SCP_ChurchObjectiveLost".Translate(), MessageTypeDefOf.NegativeEvent, false);
            Find.SignalManager.SendSignal(new Signal(lostSignal));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scpChurchCacheSite");
            Scribe_References.Look(ref core, "scpChurchCipherCore");
            Scribe_References.Look(ref sample217, "scpChurchSealed217");
            Scribe_References.Look(ref components629, "scpChurchParts629");
            Scribe_Values.Look(ref coreRead, "scpChurchCipherRead", false);
            Scribe_Values.Look(ref sampleSecured, "scpChurch217Secured", false);
            Scribe_Values.Look(ref componentsSecured, "scpChurch629Secured", false);
            Scribe_Values.Look(ref legacyCache, "scpChurchLegacyCache", false);
            Scribe_Values.Look(ref recoveredSignal, "scpChurchCipherRecoveredSignal");
            Scribe_Values.Look(ref lostSignal, "scpChurchCipherLostSignal");
            Scribe_Values.Look(ref entered, "scpChurchCacheEntered", false);
            Scribe_Values.Look(ref finished, "scpChurchCacheFinished", false);
            Scribe_Values.Look(ref cargoUnloaded, "scpChurchCargoUnloaded", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && entered && !finished
                && sample217 == null && components629 == null
                && !sampleSecured && !componentsSecured)
                legacyCache = true;
        }
    }

    public sealed class Thing_ScpChurchCipher : ThingWithComps
    {
        private bool analyzed;

        private bool IsEvidence
        {
            get { return def.defName == "SCP_OuroborosSealed217Sample"
                || def.defName == "SCP_OuroborosParts629Cache"; }
        }

        private bool NeedsStudy
        {
            get
            {
                if (!analyzed) return true;
                if (def.defName != "SCP_OuroborosParts629Cache") return false;
                ScpDef anomaly = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly629");
                return anomaly != null && Current.Game.GetComponent<ScpKnowledgeComponent>()
                    .LevelFor(anomaly) < KnowledgeLevel.PartiallyDocumented;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref analyzed, "scpChurchEvidenceAnalyzed", false);
        }

        public override string GetInspectString()
        {
            string original = base.GetInspectString();
            if (!IsEvidence) return original;
            string state = (def.defName == "SCP_OuroborosSealed217Sample"
                ? "SCP_217SealedStatus" : "SCP_629PartsStatus").Translate();
            string status = (analyzed ? "SCP_ChurchEvidenceAnalyzed"
                : "SCP_ChurchEvidenceNotAnalyzed").Translate();
            if (def.defName == "SCP_OuroborosParts629Cache")
            {
                ScpDef anomaly = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly629");
                if (anomaly != null)
                    status += "\n" + "SCP_629KnowledgeLevel".Translate((int)Current.Game
                        .GetComponent<ScpKnowledgeComponent>().LevelFor(anomaly));
            }
            return (original.NullOrEmpty() ? "" : original + "\n") + state + "\n" + status;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Spawned && QuestPart_ScpChurchCache.ActiveOn(Map) != null)
            {
                yield return new Command_Action {
                    defaultLabel = ActionLabel,
                    defaultDesc = ActionDescription,
                    icon = def.uiIcon,
                    action = ChooseReader
                };
                yield break;
            }
            if (!IsEvidence || !Spawned || !Map.IsPlayerHome
                || Current.Game.GetComponent<ScpOuroborosArchiveComponent>().ChurchCacheTick < 0)
                yield break;
            if (NeedsStudy)
            {
                var study = new Command_Action {
                    defaultLabel = (analyzed ? "SCP_629ContinueStudy"
                        : "SCP_ChurchEvidenceAnalyze").Translate(),
                    defaultDesc = "SCP_ChurchEvidenceAnalyzeDesc".Translate(),
                    icon = def.uiIcon,
                    action = ChooseAnalyst
                };
                if (CompFoundationAccessTerminal.Online(Map) == null)
                    study.Disable("SCP_ChurchReportNeedTerminal".Translate());
                yield return study;
            }
            else
            {
                var transfer = new Command_Action {
                    defaultLabel = "SCP_ChurchEvidenceTransfer".Translate(),
                    defaultDesc = "SCP_ChurchEvidenceTransferDesc".Translate(),
                    icon = def.uiIcon,
                    action = TransferEvidence
                };
                if (CompFoundationAccessTerminal.Online(Map) == null)
                    transfer.Disable("SCP_ChurchReportNeedTerminal".Translate());
                yield return transfer;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            if (CanRead(pawn))
                yield return new FloatMenuOption(ActionLabel, () => Assign(pawn));
            else if (CanAnalyze(pawn))
                yield return new FloatMenuOption((analyzed ? "SCP_629ContinueStudy"
                    : "SCP_ChurchEvidenceAnalyze").Translate(),
                    () => AssignAnalysis(pawn));
        }

        private string ActionLabel
        {
            get { return (def.defName == "SCP_OuroborosSealed217Sample" ? "SCP_ChurchSecure217"
                : def.defName == "SCP_OuroborosParts629Cache" ? "SCP_ChurchSecure629"
                : "SCP_ChurchCipherReadAction").Translate(); }
        }

        private string ActionDescription
        {
            get { return (def.defName == "SCP_OuroborosChurchCipher" ? "SCP_ChurchCipherReadDesc"
                : "SCP_ChurchSecureDesc").Translate(); }
        }

        private bool CanRead(Pawn pawn)
        {
            return Spawned && QuestPart_ScpChurchCache.ActiveOn(Map) != null
                && pawn != null && pawn.IsColonistPlayerControlled && !pawn.Drafted
                && !pawn.Dead && !pawn.Downed && pawn.jobs != null && pawn.Map == Map
                && pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some);
        }

        private void ChooseReader()
        {
            var options = new List<FloatMenuOption>();
            foreach (Pawn candidate in Map.mapPawns.FreeColonistsSpawned)
            {
                if (!CanRead(candidate)) continue;
                Pawn reader = candidate;
                options.Add(new FloatMenuOption(reader.LabelShortCap, () => Assign(reader)));
            }
            if (options.Count == 0)
                Messages.Message("SCP_ChurchCipherNoPawn".Translate(), this,
                    MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void Assign(Pawn pawn)
        {
            if (!CanRead(pawn)) return;
            JobDef job = DefDatabase<JobDef>.GetNamedSilentFail("SCP_OuroborosChurchCipherRead");
            if (job != null) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job, this), JobTag.Misc);
        }

        private bool CanAnalyze(Pawn pawn)
        {
            return IsEvidence && NeedsStudy && Spawned && Map.IsPlayerHome
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().ChurchCacheTick >= 0
                && CompFoundationAccessTerminal.Online(Map) != null
                && pawn != null && pawn.IsColonistPlayerControlled && !pawn.Drafted
                && !pawn.Dead && !pawn.Downed && pawn.jobs != null && pawn.Map == Map
                && pawn.skills != null && pawn.skills.GetSkill(SkillDefOf.Intellectual) != null
                && pawn.skills.GetSkill(SkillDefOf.Intellectual).Level >= 4
                && pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some);
        }

        private void ChooseAnalyst()
        {
            var options = new List<FloatMenuOption>();
            foreach (Pawn candidate in Map.mapPawns.FreeColonistsSpawned)
                if (CanAnalyze(candidate))
                {
                    Pawn analyst = candidate;
                    options.Add(new FloatMenuOption(analyst.LabelShortCap,
                        () => AssignAnalysis(analyst)));
                }
            if (options.Count == 0)
                Messages.Message("SCP_ChurchEvidenceNoAnalyst".Translate(), this,
                    MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AssignAnalysis(Pawn pawn)
        {
            if (!CanAnalyze(pawn)) return;
            JobDef job = DefDatabase<JobDef>.GetNamedSilentFail("SCP_ChurchEvidenceAnalyze");
            if (job != null) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job, this), JobTag.Misc);
        }

        public void Analyze(Pawn pawn)
        {
            if (!CanAnalyze(pawn)) return;
            bool firstStudy = !analyzed;
            analyzed = true;
            pawn.skills.Learn(SkillDefOf.Intellectual, 1200f);
            if (firstStudy)
            {
                Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                    .RecordChurchEvidence(def.defName);
                Find.LetterStack.ReceiveLetter("SCP_ChurchEvidenceAnalyzedTitle".Translate(),
                    "SCP_ChurchEvidenceAnalyzedBody".Translate(LabelNoCount),
                    LetterDefOf.NeutralEvent, this);
            }
            if (def.defName == "SCP_OuroborosParts629Cache")
            {
                ScpDef anomaly = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly629");
                ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                if (anomaly == null || knowledge == null) return;
                KnowledgeLevel before = knowledge.LevelFor(anomaly);
                knowledge.AddResearch(anomaly, 1200f);
                if (before < KnowledgeLevel.PartiallyDocumented
                    && knowledge.LevelFor(anomaly) >= KnowledgeLevel.PartiallyDocumented)
                {
                    Current.Game.GetComponent<ScpOuroborosArchiveComponent>().Mark629RoutingInsight();
                    Find.LetterStack.ReceiveLetter("SCP_629RoutingInsightTitle".Translate(),
                        "SCP_629RoutingInsightBody".Translate(), LetterDefOf.NeutralEvent, this);
                }
            }
        }

        private void TransferEvidence()
        {
            if (!IsEvidence || NeedsStudy || !Spawned || !Map.IsPlayerHome) return;
            if (Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                .TransferChurchEvidence(def.defName, Map)) Destroy(DestroyMode.Vanish);
        }
    }

    public sealed class JobDriver_ScpChurchEvidenceAnalyze : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(1500, TargetIndex.A);
            yield return Toils_General.Do(() => {
                Thing_ScpChurchCipher evidence = job.targetA.Thing as Thing_ScpChurchCipher;
                if (evidence != null) evidence.Analyze(pawn);
            });
        }
    }

    public sealed class JobDriver_ScpChurchCipherRead : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(600, TargetIndex.A);
            yield return Toils_General.Do(() => {
                QuestPart_ScpChurchCache part = QuestPart_ScpChurchCache.ActiveOn(pawn.Map);
                if (part != null) part.ReadCore(pawn, job.targetA.Thing);
            });
        }
    }

    public sealed class GenStep_ScpChurchCache : GenStep
    {
        public override int SeedPart { get { return 88208; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_OuroborosChurchCacheSite")
                throw new System.InvalidOperationException("Church cache must generate only on its quest site.");
            string layout = Path.Combine(def.modContentPack.RootDir, "Assets", "ScpChurchCache");
            if (ScpSavedQuestSiteLayout.Available(layout))
            {
                GenerateSavedCache(map, layout);
                return;
            }
            IntVec3 center = IntVec3.Invalid;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(map.Center, 35f, true))
            {
                CellRect area = new CellRect(candidate.x - 4, candidate.z - 4, 9, 9);
                if (!area.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z))) continue;
                if (!area.Cells.All(cell => cell.Standable(map) && cell.GetEdifice(map) == null)) continue;
                center = candidate;
                break;
            }
            if (!center.IsValid)
                throw new System.InvalidOperationException("No clear space for the Church cache.");
            TerrainDef concrete = DefDatabase<TerrainDef>.GetNamedSilentFail("Concrete");
            CellRect ruin = new CellRect(center.x - 3, center.z - 3, 7, 7);
            if (concrete != null)
                foreach (IntVec3 cell in ruin.Cells) map.terrainGrid.SetTerrain(cell, concrete);
            ThingDef wall = DefDatabase<ThingDef>.GetNamed("Wall");
            ThingDef steel = DefDatabase<ThingDef>.GetNamed("Steel");
            foreach (IntVec3 cell in ruin.Cells)
            {
                bool edge = cell.x == ruin.minX || cell.x == ruin.maxX
                    || cell.z == ruin.minZ || cell.z == ruin.maxZ;
                bool entrance = cell.z == ruin.minZ && cell.x == center.x;
                if (edge && !entrance && (cell.x + cell.z) % 7 != 0)
                    GenSpawn.Spawn(ThingMaker.MakeThing(wall, steel), cell, map);
            }
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(
                "SCP_OuroborosChurchCipher")), center, map);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(
                "SCP_OuroborosSealed217Sample")), new IntVec3(center.x - 2, 0, center.z), map);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(
                "SCP_OuroborosParts629Cache")), new IntVec3(center.x + 2, 0, center.z), map);

            FactionDef churchDef = DefDatabase<FactionDef>.GetNamed("SCP_ChurchBrokenGod");
            Faction church = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(faction => faction.def == churchDef && !faction.defeated);
            Faction defendersFaction = church ?? Faction.OfAncientsHostile;
            if (church != null) ScpFactionRelationsComponent.EnsureHostile(church, Faction.OfPlayer);
            PawnKindDef zealot = DefDatabase<PawnKindDef>.GetNamed(
                church != null ? "SCP_BrokenGodZealot" : "AncientSoldier");
            PawnKindDef cantor = DefDatabase<PawnKindDef>.GetNamed(
                church != null ? "SCP_BrokenGodCantor" : "AncientSoldier");
            var guards = new List<Pawn>();
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                IntVec3 preferred = new IntVec3(center.x + Mathf.RoundToInt(Mathf.Cos(angle) * 8f), 0,
                    center.z + Mathf.RoundToInt(Mathf.Sin(angle) * 8f));
                IntVec3 spawnCell = IntVec3.Invalid;
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(preferred, 5f, true))
                    if (cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null
                        && !cell.GetThingList(map).Any(thing => thing is Pawn))
                    { spawnCell = cell; break; }
                if (!spawnCell.IsValid) continue;
                Pawn pawn = PawnGenerator.GeneratePawn(i < 2 ? cantor : zealot, defendersFaction);
                GenSpawn.Spawn(pawn, spawnCell, map);
                guards.Add(pawn);
            }
            if (guards.Count > 0)
                LordMaker.MakeNewLord(defendersFaction,
                    new LordJob_DefendBase(defendersFaction, center, 25, false), map, guards);
        }

        private static void GenerateSavedCache(Map map, string layout)
        {
            FactionDef churchDef = DefDatabase<FactionDef>.GetNamed("SCP_ChurchBrokenGod");
            Faction church = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(faction => faction.def == churchDef && !faction.defeated);
            Faction defendersFaction = church ?? Faction.OfAncientsHostile;
            if (church != null) ScpFactionRelationsComponent.EnsureHostile(church, Faction.OfPlayer);
            ScpSavedQuestSiteLayout.Restore(map, layout, defendersFaction);

            // The nested sleeping room in the player's layout is x61–63, z34–39.
            CellRect bedroom = new CellRect(61, 34, 3, 6);
            var reserved = new HashSet<IntVec3>();
            string[] cargo = { "SCP_OuroborosChurchCipher", "SCP_OuroborosSealed217Sample",
                "SCP_OuroborosParts629Cache" };
            IntVec3[] preferred = { new IntVec3(61, 0, 37), new IntVec3(61, 0, 35),
                new IntVec3(61, 0, 39) };
            for (int i = 0; i < cargo.Length; i++)
            {
                IntVec3 cell = ScpSavedQuestSiteLayout.FreeCell(map, preferred[i], bedroom, reserved);
                GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(cargo[i])), cell, map);
                reserved.Add(cell);
            }

            PawnKindDef zealot = DefDatabase<PawnKindDef>.GetNamed(
                church != null ? "SCP_BrokenGodZealot" : "AncientSoldier");
            PawnKindDef cantor = DefDatabase<PawnKindDef>.GetNamed(
                church != null ? "SCP_BrokenGodCantor" : "AncientSoldier");
            IntVec3[] guardSpots = { new IntVec3(62, 0, 36), new IntVec3(62, 0, 38),
                new IntVec3(63, 0, 36), new IntVec3(67, 0, 39), new IntVec3(70, 0, 40),
                new IntVec3(68, 0, 34), new IntVec3(71, 0, 34), new IntVec3(73, 0, 37) };
            CellRect outerRoom = new CellRect(65, 29, 10, 15);
            var guards = new List<Pawn>();
            for (int i = 0; i < guardSpots.Length; i++)
            {
                IntVec3 cell = ScpSavedQuestSiteLayout.FreeCell(map, guardSpots[i],
                    i < 3 ? bedroom : outerRoom, reserved, 5f);
                Pawn pawn = PawnGenerator.GeneratePawn(i < 2 ? cantor : zealot, defendersFaction);
                GenSpawn.Spawn(pawn, cell, map);
                guards.Add(pawn);
                reserved.Add(cell);
            }
            LordMaker.MakeNewLord(defendersFaction,
                new LordJob_DefendBase(defendersFaction, new IntVec3(66, 0, 37), 18, false), map, guards);
        }
    }
}
