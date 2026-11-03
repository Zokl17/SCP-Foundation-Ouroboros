using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;
using UnityEngine;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_ScpSignalResearch : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            return map != null && map.IsPlayerHome
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().FinalReportTick >= 0
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().SignalRecoveryTick >= 0
                && (Find.QuestManager == null || !Find.QuestManager.QuestsListForReading.Any(quest =>
                    quest.root != null && quest.root.defName == "SCP_OuroborosSignalResearch"));
        }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_ScpSignalResearch {
                map = QuestGen.slate.Get<Map>("map"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                completedSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpSignalAnalyzed")
            });
        }
    }

    public sealed class QuestPart_ScpSignalResearch : QuestPartActivable
    {
        public Map map;
        public string completedSignal;
        private int researchTicks;
        private bool completed;

        public static QuestPart_ScpSignalResearch ActiveOn(Map home)
        {
            if (home == null || Find.QuestManager == null) return null;
            return Find.QuestManager.QuestsListForReading
                .Where(quest => quest.State == QuestState.Ongoing && quest.root != null
                    && quest.root.defName == "SCP_OuroborosSignalResearch")
                .SelectMany(quest => quest.PartsListForReading.OfType<QuestPart_ScpSignalResearch>())
                .FirstOrDefault(part => part.map == home && part.State == QuestPartState.Enabled && !part.completed);
        }

        public void Advance(Pawn researcher)
        {
            if (completed || researcher == null || researcher.Map != map
                || CompFoundationAccessTerminal.Online(map) == null) return;
            researchTicks++;
            researcher.skills.Learn(SkillDefOf.Intellectual, 0.03f);
            if (researchTicks < 15000) return;
            completed = true;
            Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                .UnlockSignalAnalysis(Find.TickManager.TicksGame);
            Find.LetterStack.ReceiveLetter("SCP_SignalAnalysisTitle".Translate(),
                "SCP_SignalAnalysisLetter".Translate(), LetterDefOf.NeutralEvent);
            Find.SignalManager.SendSignal(new Signal(completedSignal));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scpSignalResearchMap");
            Scribe_Values.Look(ref completedSignal, "scpSignalResearchCompletedSignal");
            Scribe_Values.Look(ref researchTicks, "scpSignalResearchTicks", 0);
            Scribe_Values.Look(ref completed, "scpSignalResearchCompleted", false);
        }
    }

    public sealed class JobDriver_ScpSignalResearch : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil research = ToilMaker.MakeToil();
            research.defaultCompleteMode = ToilCompleteMode.Never;
            research.activeSkill = () => SkillDefOf.Intellectual;
            research.tickAction = () =>
            {
                QuestPart_ScpSignalResearch part = QuestPart_ScpSignalResearch.ActiveOn(pawn.Map);
                if (part == null || CompFoundationAccessTerminal.Online(pawn.Map) != job.targetA.Thing
                    || Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(pawn) < 4
                    || pawn.skills.GetSkill(SkillDefOf.Intellectual).Level < 12)
                { EndJobWith(JobCondition.Incompletable); return; }
                part.Advance(pawn);
                if (QuestPart_ScpSignalResearch.ActiveOn(pawn.Map) == null)
                    EndJobWith(JobCondition.Succeeded);
            };
            yield return research;
        }
    }

    public sealed class QuestNode_ScpSignalSiteSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return slate.Get<Map>("map") != null && Faction.OfAncientsHostile != null
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().FinalReportTick >= 0;
        }

        protected override void RunInt()
        {
            QuestGen.slate.Set("siteFaction", Faction.OfAncientsHostile);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_OuroborosSignalSitePart")
            });
        }
    }

    public sealed class QuestNode_ScpSignalSiteTrack : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_ScpSignalSite {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpSignalRecorderRecovered"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpSignalRecorderLost")
            });
        }
    }

    public sealed class QuestPart_ScpSignalSite : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal;
        public string lostSignal;
        private Thing recorder;
        private bool entered;
        private bool finished;
        private bool siteVisualsUpdated;
        private bool readInstructionShown;

        public static QuestPart_ScpSignalSite ActiveOn(Map map)
        {
            if (map == null || Find.QuestManager == null) return null;
            return Find.QuestManager.QuestsListForReading
                .Where(quest => quest.State == QuestState.Ongoing && quest.root != null
                    && quest.root.defName == "SCP_OuroborosSignalSite")
                .SelectMany(quest => quest.PartsListForReading.OfType<QuestPart_ScpSignalSite>())
                .FirstOrDefault(part => part.site != null && part.site.Map == map
                    && part.State == QuestPartState.Enabled && !part.finished);
        }

        public void ReadRecord(Pawn pawn, Thing source)
        {
            if (finished || pawn == null || !pawn.IsColonistPlayerControlled
                || pawn.Map == null || site == null || pawn.Map != site.Map
                || source == null || !source.Spawned) return;
            if (recorder == null) recorder = source;
            if (recorder != source) return;
            if (Current.Game.GetComponent<ScpOuroborosArchiveComponent>().SignalAnalysisTick < 0)
            {
                ThingDef transponderDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_OuroborosTransponder");
                if (transponderDef == null || pawn.inventory == null) return;
                Thing transponder = ThingMaker.MakeThing(transponderDef);
                if (!pawn.inventory.innerContainer.TryAdd(transponder))
                {
                    transponder.Destroy(DestroyMode.Vanish);
                    Messages.Message("SCP_SignalCarryFailed".Translate(), pawn,
                        MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }
            source.Destroy(DestroyMode.Vanish);
            Finish(true);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 60 != 0) return;
            Map siteMap = site == null ? null : site.Map;
            if (siteMap != null && !siteVisualsUpdated)
            {
                siteVisualsUpdated = true;
                // Old saves already hold the flat 100x100 map; new sites use natural terrain.
                if (siteMap.Size.x == 100 && siteMap.Size.z == 100)
                {
                    TerrainDef gravel = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel");
                    if (gravel != null)
                        foreach (IntVec3 cell in new CellRect(0, 0, siteMap.Size.x, siteMap.Size.z).Cells)
                            if (siteMap.terrainGrid.TerrainAt(cell) == TerrainDefOf.Soil
                                && cell.GetEdifice(siteMap) == null
                                && Mathf.PerlinNoise(cell.x * 0.085f + 2.1f,
                                    cell.z * 0.085f + 7.3f) > 0.73f)
                                siteMap.terrainGrid.SetTerrain(cell, gravel);
                    GenStepDef plants = DefDatabase<GenStepDef>.GetNamedSilentFail("Plants");
                    if (plants != null)
                        try { plants.genStep.Generate(siteMap, new GenStepParams()); }
                        catch (System.Exception exception)
                        { Log.Warning("SCP signal site vegetation could not be restored: " + exception); }
                }
            }
            if (siteMap != null && !entered)
            {
                entered = true;
                ThingDef def = DefDatabase<ThingDef>.GetNamed("SCP_OuroborosSignalRecorder");
                recorder = siteMap.listerThings.ThingsOfDef(def).FirstOrDefault();
                if (recorder == null) { Finish(false); return; }
            }
            if (recorder != null && recorder.Spawned && !(recorder is Thing_ScpSignalRecorder))
            {
                Map map = recorder.Map;
                IntVec3 cell = recorder.Position;
                Thing replacement = ThingMaker.MakeThing(recorder.def);
                replacement.HitPoints = recorder.HitPoints;
                recorder.Destroy(DestroyMode.Vanish);
                recorder = GenSpawn.Spawn(replacement, cell, map);
            }
            if (recorder != null && recorder.Spawned && !readInstructionShown)
            {
                readInstructionShown = true;
                Messages.Message("SCP_SignalReadInstruction".Translate(), recorder,
                    MessageTypeDefOf.NeutralEvent, false);
            }
            if (recorder == null) { if (entered) Finish(false); return; }
            if (recorder.Destroyed) { Finish(false); return; }
        }

        private void Finish(bool success)
        {
            finished = true;
            if (success)
            {
                Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                    .UnlockSignalRecovery(Find.TickManager.TicksGame);
                Find.LetterStack.ReceiveLetter("SCP_SignalRecoveryTitle".Translate(),
                    "SCP_SignalRecoveryLetter".Translate(), LetterDefOf.NeutralEvent);
            }
            Find.SignalManager.SendSignal(new Signal(success ? recoveredSignal : lostSignal));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scpSignalSite");
            Scribe_References.Look(ref recorder, "scpSignalRecorder");
            Scribe_Values.Look(ref recoveredSignal, "scpSignalRecoveredSignal");
            Scribe_Values.Look(ref lostSignal, "scpSignalLostSignal");
            Scribe_Values.Look(ref entered, "scpSignalSiteEntered", false);
            Scribe_Values.Look(ref finished, "scpSignalSiteFinished", false);
            Scribe_Values.Look(ref siteVisualsUpdated, "scpSignalSiteVisualsUpdated", false);
            Scribe_Values.Look(ref readInstructionShown, "scpSignalReadInstructionShown", false);
        }
    }

    public sealed class Thing_ScpSignalRecorder : ThingWithComps
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Spawned && QuestPart_ScpSignalSite.ActiveOn(Map) != null)
                yield return new Command_Action {
                    defaultLabel = "SCP_SignalReadAction".Translate(),
                    defaultDesc = "SCP_SignalReadDesc".Translate(),
                    icon = def.uiIcon,
                    action = ChooseReader
                };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            if (CanRead(pawn))
                yield return new FloatMenuOption("SCP_SignalReadAction".Translate(), () => Assign(pawn));
        }

        private bool CanRead(Pawn pawn)
        {
            return Spawned && QuestPart_ScpSignalSite.ActiveOn(Map) != null
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
                Messages.Message("SCP_SignalReadNoPawn".Translate(), this,
                    MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void Assign(Pawn pawn)
        {
            if (!CanRead(pawn)) return;
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("SCP_OuroborosSignalRead");
            if (jobDef != null) pawn.jobs.TryTakeOrderedJob(
                JobMaker.MakeJob(jobDef, this), JobTag.Misc);
        }
    }

    public sealed class JobDriver_ScpSignalRead : JobDriver
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
                Thing source = job.targetA.Thing;
                QuestPart_ScpSignalSite part = QuestPart_ScpSignalSite.ActiveOn(pawn.Map);
                if (part != null) part.ReadRecord(pawn, source);
            });
        }
    }

    public sealed class GenStep_ScpSignalSite : GenStep
    {
        public override int SeedPart { get { return 88207; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_OuroborosSignalSite")
                throw new System.InvalidOperationException("Signal relay must generate only on its quest site.");
            string layout = Path.Combine(def.modContentPack.RootDir, "Assets", "ScpSignalRelay");
            if (ScpSavedQuestSiteLayout.Available(layout))
            {
                ScpSavedQuestSiteLayout.Restore(map, layout, Faction.OfAncientsHostile);
                IntVec3 recorderCell = ScpSavedQuestSiteLayout.FreeCell(map,
                    new IntVec3(149, 0, 192), new CellRect(146, 188, 9, 8));
                GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(
                    "SCP_OuroborosSignalRecorder")), recorderCell, map);
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
                throw new System.InvalidOperationException("No clear space for the signal relay ruin.");
            TerrainDef concrete = DefDatabase<TerrainDef>.GetNamedSilentFail("Concrete");
            if (concrete != null)
                foreach (IntVec3 cell in new CellRect(center.x - 3, center.z - 3, 7, 7).Cells)
                    map.terrainGrid.SetTerrain(cell, concrete);
            ThingDef wall = DefDatabase<ThingDef>.GetNamed("Wall");
            ThingDef steel = DefDatabase<ThingDef>.GetNamed("Steel");
            CellRect ruin = new CellRect(center.x - 3, center.z - 3, 7, 7);
            foreach (IntVec3 cell in ruin.Cells)
            {
                bool edge = cell.x == ruin.minX || cell.x == ruin.maxX
                    || cell.z == ruin.minZ || cell.z == ruin.maxZ;
                bool gap = cell.z == ruin.minZ && (cell.x == center.x || cell.x == center.x + 1);
                if (edge && !gap && (cell.x + cell.z) % 5 != 0)
                    GenSpawn.Spawn(ThingMaker.MakeThing(wall, steel), cell, map);
            }
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(
                "SCP_OuroborosSignalRecorder")), center, map);
        }
    }
}
