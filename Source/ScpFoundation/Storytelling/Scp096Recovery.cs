using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_Scp096SiteSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            return map != null && map.IsPlayerHome && Current.Game != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && Faction.OfAncientsHostile != null
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("096")
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_096HoldingCell") != null;
        }

        protected override void RunInt()
        {
            QuestGen.slate.Set("siteFaction", Faction.OfAncientsHostile);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_096HoldingCell")
            });
        }
    }

    public sealed class QuestNode_Scp096TrackRecovery : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp096Recovery {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp096Recovered"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp096Lost")
            });
        }
    }

    public sealed class QuestPart_Scp096Recovery : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal;
        public string lostSignal;
        private Thing subject;
        private bool entered;
        private bool finished;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scp096Site");
            Scribe_References.Look(ref subject, "scp096Subject");
            Scribe_Values.Look(ref recoveredSignal, "scp096RecoveredSignal");
            Scribe_Values.Look(ref lostSignal, "scp096LostSignal");
            Scribe_Values.Look(ref entered, "scp096SiteEntered", false);
            Scribe_Values.Look(ref finished, "scp096RecoveryFinished", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 60 != 0) return;
            Map siteMap = site == null ? null : site.Map;
            if (siteMap != null && !entered)
            {
                entered = true;
                subject = siteMap.listerThings.ThingsOfDef(
                    DefDatabase<ThingDef>.GetNamed("SCP_096Object")).FirstOrDefault();
                if (subject == null) { Finish(false); return; }
            }
            if (subject == null) { if (entered) Finish(false); return; }
            if (subject.Destroyed) { Finish(false); return; }
            if (subject.MapHeld != null && subject.MapHeld.IsPlayerHome) Finish(true);
        }

        private void Finish(bool success)
        {
            finished = true;
            Find.SignalManager.SendSignal(new Signal(success ? recoveredSignal : lostSignal));
        }
    }

    public sealed class GenStep_Scp096HoldingCell : GenStep
    {
        public override int SeedPart { get { return 96; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map.Parent == null || map.Parent.def.defName != "SCP_096RecoverySite")
                throw new InvalidOperationException("SCP-096 cell may only generate on its quest site.");
            string layout = Path.Combine(def.modContentPack.RootDir, "Assets", "Scp096Breach");
            if (ScpSavedQuestSiteLayout.Available(layout))
            {
                GenerateSavedBreach(map, layout);
                return;
            }
            IntVec3 midpoint = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            IntVec3 center = IntVec3.Invalid;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(midpoint, 50f, true))
            {
                CellRect area = new CellRect(candidate.x - 4, candidate.z - 4, 9, 9);
                if (!area.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z))) continue;
                if (!area.Cells.All(cell => cell.Standable(map) && cell.GetEdifice(map) == null)) continue;
                center = candidate;
                break;
            }
            if (!center.IsValid) throw new InvalidOperationException("No clear space for SCP-096 holding cell.");
            ThingDef wall = DefDatabase<ThingDef>.GetNamed("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamed("Door");
            ThingDef steel = DefDatabase<ThingDef>.GetNamed("Steel");
            CellRect room = new CellRect(center.x - 3, center.z - 3, 7, 7);
            foreach (IntVec3 cell in room.Cells)
            {
                if (cell.x != room.minX && cell.x != room.maxX
                    && cell.z != room.minZ && cell.z != room.maxZ) continue;
                bool entrance = cell.x == center.x && cell.z == room.minZ;
                GenSpawn.Spawn(ThingMaker.MakeThing(entrance ? door : wall, steel), cell, map);
            }
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_096Object")),
                center, map, Rot4.South);
        }

        private static void GenerateSavedBreach(Map map, string layout)
        {
            ScpSavedQuestSiteLayout.Restore(map, layout, Faction.OfAncientsHostile);
            CellRect facility = new CellRect(202, 101, 20, 18);
            var used = new HashSet<IntVec3>();
            IntVec3[] victims = { new IntVec3(206, 0, 116), new IntVec3(209, 0, 112),
                new IntVec3(205, 0, 106), new IntVec3(217, 0, 116),
                new IntVec3(219, 0, 111) };
            ThingDef blood = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Blood");
            for (int i = 0; i < victims.Length; i++)
            {
                IntVec3 cell = ScpSavedQuestSiteLayout.FreeCell(map, victims[i], facility, used, 4f);
                Pawn civilian = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    PawnKindDefOf.Villager, null, forceGenerateNewPawn: true));
                GenSpawn.Spawn(civilian, cell, map);
                civilian.Kill(null);
                if (blood != null) FilthMaker.TryMakeFilth(cell, map, blood);
                used.Add(cell);
            }
            IntVec3 subjectCell = ScpSavedQuestSiteLayout.FreeCell(map,
                new IntVec3(216, 0, 115), facility, used, 4f);
            GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_096Object")),
                subjectCell, map, Rot4.South);
        }
    }
}
