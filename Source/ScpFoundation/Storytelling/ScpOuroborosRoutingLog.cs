using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using ScpFoundation.Definitions;
using ScpFoundation.Knowledge;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_ScpRoutingLogSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            ScpDef parts = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly629");
            return slate.Get<Map>("map") != null && Faction.OfAncientsHostile != null
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().ChurchResolutionTick >= 0
                && parts != null && Current.Game.GetComponent<ScpKnowledgeComponent>()
                    .LevelFor(parts) >= KnowledgeLevel.PartiallyDocumented;
        }

        protected override void RunInt()
        {
            FactionDef churchDef = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_ChurchBrokenGod");
            Faction church = churchDef == null ? null : Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(faction => faction.def == churchDef && !faction.defeated);
            QuestGen.slate.Set("siteFaction", church ?? Faction.OfAncientsHostile);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_OuroborosRoutingLogPart")
            });
        }
    }

    public sealed class QuestNode_ScpRoutingLogTrack : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_ScpRoutingLog {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpRoutingLogRecovered"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("ScpRoutingLogLost")
            });
        }
    }

    public sealed class QuestPart_ScpRoutingLog : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal;
        public string lostSignal;
        private Thing recorder;
        private bool entered;
        private bool secured;
        private bool finished;

        public static QuestPart_ScpRoutingLog ActiveOn(Map map)
        {
            if (map == null || Find.QuestManager == null) return null;
            return Find.QuestManager.QuestsListForReading
                .Where(quest => quest.State == QuestState.Ongoing && quest.root != null
                    && quest.root.defName == "SCP_OuroborosRoutingLog")
                .SelectMany(quest => quest.PartsListForReading.OfType<QuestPart_ScpRoutingLog>())
                .FirstOrDefault(part => part.site != null && part.site.Map == map
                    && part.State == QuestPartState.Enabled && !part.finished);
        }

        public void Take(Pawn pawn, Thing source)
        {
            if (finished || secured || pawn == null || !pawn.IsColonistPlayerControlled
                || source != recorder || !source.Spawned || site == null
                || pawn.Map != site.Map || pawn.inventory == null) return;
            Map sourceMap = source.Map;
            IntVec3 sourceCell = source.Position;
            source.DeSpawn();
            if (!pawn.inventory.innerContainer.TryAdd(source))
            {
                GenSpawn.Spawn(source, sourceCell, sourceMap);
                Messages.Message("SCP_RoutingLogCarryFailed".Translate(), pawn,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            secured = true;
            Messages.Message("SCP_RoutingLogSecured".Translate(), pawn,
                MessageTypeDefOf.PositiveEvent, false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 60 != 0) return;
            Map siteMap = site == null ? null : site.Map;
            if (siteMap != null && !entered)
            {
                entered = true;
                recorder = siteMap.listerThings.ThingsOfDef(
                    DefDatabase<ThingDef>.GetNamed("SCP_OuroborosRoutingLogCore")).FirstOrDefault();
                if (recorder == null) { Finish(false); return; }
            }
            if (!entered || recorder == null) return;
            if (recorder.Destroyed) { Finish(false); return; }
            if (!secured) return;
            foreach (Map home in Find.Maps.Where(map => map.IsPlayerHome))
            {
                if (recorder.Spawned && recorder.Map == home) { Finish(true); return; }
                foreach (Pawn carrier in home.mapPawns.FreeColonistsSpawned)
                    if (carrier.inventory != null && carrier.inventory.innerContainer.Contains(recorder))
                    {
                        Thing dropped;
                        carrier.inventory.innerContainer.TryDrop(recorder, carrier.Position, home,
                            ThingPlaceMode.Near, out dropped);
                        if (dropped != null) Finish(true);
                        return;
                    }
            }
        }

        private void Finish(bool success)
        {
            finished = true;
            if (success)
                Current.Game.GetComponent<ScpOuroborosArchiveComponent>()
                    .UnlockRoutingLog(Find.TickManager.TicksGame);
            Find.SignalManager.SendSignal(new Signal(success ? recoveredSignal : lostSignal));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scpRoutingLogSite");
            Scribe_References.Look(ref recorder, "scpRoutingLogCore");
            Scribe_Values.Look(ref recoveredSignal, "scpRoutingLogRecoveredSignal");
            Scribe_Values.Look(ref lostSignal, "scpRoutingLogLostSignal");
            Scribe_Values.Look(ref entered, "scpRoutingLogEntered", false);
            Scribe_Values.Look(ref secured, "scpRoutingLogSecured", false);
            Scribe_Values.Look(ref finished, "scpRoutingLogFinished", false);
        }
    }

    public sealed class Thing_ScpRoutingLog : ThingWithComps
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Spawned && QuestPart_ScpRoutingLog.ActiveOn(Map) != null)
                yield return new Command_Action {
                    defaultLabel = "SCP_RoutingLogTake".Translate(),
                    defaultDesc = "SCP_RoutingLogTakeDesc".Translate(),
                    icon = def.uiIcon,
                    action = ChooseReader
                };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            if (CanRead(pawn))
                yield return new FloatMenuOption("SCP_RoutingLogTake".Translate(), () => Assign(pawn));
        }

        private bool CanRead(Pawn pawn)
        {
            return Spawned && QuestPart_ScpRoutingLog.ActiveOn(Map) != null
                && pawn != null && pawn.IsColonistPlayerControlled && !pawn.Drafted
                && !pawn.Dead && !pawn.Downed && pawn.jobs != null && pawn.Map == Map
                && pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some);
        }

        private void ChooseReader()
        {
            var options = new List<FloatMenuOption>();
            foreach (Pawn candidate in Map.mapPawns.FreeColonistsSpawned)
                if (CanRead(candidate))
                {
                    Pawn reader = candidate;
                    options.Add(new FloatMenuOption(reader.LabelShortCap, () => Assign(reader)));
                }
            if (options.Count == 0)
                Messages.Message("SCP_RoutingLogNoReader".Translate(), this,
                    MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void Assign(Pawn pawn)
        {
            if (!CanRead(pawn)) return;
            JobDef job = DefDatabase<JobDef>.GetNamedSilentFail("SCP_RoutingLogTake");
            if (job != null) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job, this), JobTag.Misc);
        }
    }

    public sealed class JobDriver_ScpRoutingLogTake : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(900, TargetIndex.A);
            yield return Toils_General.Do(() => {
                QuestPart_ScpRoutingLog part = QuestPart_ScpRoutingLog.ActiveOn(pawn.Map);
                if (part != null) part.Take(pawn, job.targetA.Thing);
            });
        }
    }

    public sealed class GenStep_ScpRoutingLogSite : GenStep
    {
        public override int SeedPart { get { return 629217; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_OuroborosRoutingLogSite")
                throw new InvalidOperationException("Routing log may generate only on its quest site.");
            IntVec3 center = IntVec3.Invalid;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(map.Center, 40f, true))
            {
                CellRect area = new CellRect(candidate.x - 6, candidate.z - 5, 13, 11);
                if (!area.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z))) continue;
                if (!area.Cells.All(cell => cell.Standable(map) && cell.GetEdifice(map) == null)) continue;
                center = candidate;
                break;
            }
            if (!center.IsValid)
                throw new InvalidOperationException("No clear space for the routing log outpost.");
            CellRect building = new CellRect(center.x - 5, center.z - 4, 11, 9);
            TerrainDef concrete = DefDatabase<TerrainDef>.GetNamedSilentFail("Concrete");
            ThingDef wall = DefDatabase<ThingDef>.GetNamed("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamed("Door");
            ThingDef steel = DefDatabase<ThingDef>.GetNamed("Steel");
            foreach (IntVec3 cell in building.Cells)
            {
                if (concrete != null) map.terrainGrid.SetTerrain(cell, concrete);
                bool edge = cell.x == building.minX || cell.x == building.maxX
                    || cell.z == building.minZ || cell.z == building.maxZ;
                bool divider = cell.z == center.z + 1;
                bool entrance = cell.x == center.x && cell.z == building.minZ;
                bool innerDoor = cell.x == center.x && divider;
                if (edge || divider)
                    GenSpawn.Spawn(ThingMaker.MakeThing(entrance || innerDoor ? door : wall, steel),
                        cell, map);
                else map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            }
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>
                .GetNamed("SCP_OuroborosRoutingLogCore")),
                new IntVec3(center.x, 0, center.z + 3), map);

            FactionDef churchDef = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_ChurchBrokenGod");
            Faction church = churchDef == null ? null : Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(faction => faction.def == churchDef && !faction.defeated);
            Faction defenders = church ?? Faction.OfAncientsHostile;
            if (church != null) ScpFactionRelationsComponent.EnsureHostile(church, Faction.OfPlayer);
            PawnKindDef zealot = DefDatabase<PawnKindDef>.GetNamed(
                church != null ? "SCP_BrokenGodZealot" : "AncientSoldier");
            var guards = new List<Pawn>();
            IntVec3[] spots = { new IntVec3(center.x - 3, 0, center.z - 2),
                new IntVec3(center.x + 3, 0, center.z - 2),
                new IntVec3(center.x - 4, 0, center.z - 6),
                new IntVec3(center.x + 4, 0, center.z - 6) };
            foreach (IntVec3 spot in spots)
            {
                IntVec3 cell = GenRadial.RadialCellsAround(spot, 4f, true).Where(
                    candidate => candidate.InBounds(map) && candidate.Standable(map)
                        && candidate.GetEdifice(map) == null
                        && !candidate.GetThingList(map).Any(thing => thing is Pawn))
                    .DefaultIfEmpty(IntVec3.Invalid).First();
                if (!cell.IsValid) continue;
                Pawn guard = PawnGenerator.GeneratePawn(zealot, defenders);
                GenSpawn.Spawn(guard, cell, map);
                guards.Add(guard);
            }
            if (guards.Count > 0)
                LordMaker.MakeNewLord(defenders,
                    new LordJob_DefendBase(defenders, center, 20, false), map, guards);
        }
    }
}
