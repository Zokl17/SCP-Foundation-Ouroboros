using System.Linq;
using RimWorld;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Discover882 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882Object");
            ThingDef defaultTank = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882Core");
            ThingDef fullTank = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882CoreFull");
            return map != null && map.IsPlayerHome && thingDef != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("882")
                && map.listerThings.ThingsOfDef(thingDef).Count == 0
                && (defaultTank == null || !map.listerThings.ThingsOfDef(defaultTank)
                    .Any(Contains882))
                && (fullTank == null || !map.listerThings.ThingsOfDef(fullTank)
                    .Any(Contains882));
        }

        private static bool Contains882(Thing thing)
        {
            CompScp882 comp = thing.TryGetComp<CompScp882>();
            return comp != null && comp.ContainsObject;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamed("SCP_882Object");
            IntVec3 cell;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => ScpSpawnCells.FootprintClear(candidate, map, thingDef), map, out cell)) return false;
            GenSpawn.Spawn(ThingMaker.MakeThing(thingDef), cell, map);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("882");
            return true;
        }
    }

    public sealed class IncidentWorker_Breach106 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef station = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106BaitStation");
            PawnKindDef entity = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_106Kind");
            ThingDef legacy = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106Entity");
            return map != null && map.IsPlayerHome && station != null && entity != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && map.mapPawns.FreeColonistsSpawnedCount >= 5
                && Current.Game.GetComponent<ScpArrivalRegistry>().CanArrive106(false)
                && (!QuestPart_Scp106Recovery.ActiveFor(map)
                    || Current.Game.GetComponent<ScpArrivalRegistry>().HasUsed106Route(true))
                && map.listerThings.ThingsOfDef(station).Count > 0
                && (legacy == null || map.listerThings.ThingsOfDef(legacy).Count == 0)
                && !map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp106>().Any(pawn => !pawn.Dead);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef station = DefDatabase<ThingDef>.GetNamed("SCP_106BaitStation");
            bool breached = map.listerThings.ThingsOfDef(station).Select(t => t.TryGetComp<CompLarryProtocol>())
                .Any(comp => comp != null && comp.Breach());
            if (breached) Current.Game.GetComponent<ScpArrivalRegistry>().Record106Route(false);
            return breached;
        }
    }

    public sealed class IncidentWorker_Visit049 : IncidentWorker
    {
        internal static bool HasArrivalThreshold(Map map)
        {
            if (map == null) return false;
            return map.mapPawns.FreeColonistsSpawned.Count(Pawn_Scp049.IsInjuredPatient) >= 3
                && map.mapPawns.FreeColonistsSpawned.Count(pawn => !pawn.Dead && !pawn.Downed) >= 2;
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            PawnKindDef entity = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_049Kind");
            ThingDef legacy = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_049Entity");
            return map != null && map.IsPlayerHome && ModsConfig.AnomalyActive && entity != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && HasArrivalThreshold(map)
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("049")
                && (legacy == null || map.listerThings.ThingsOfDef(legacy).Count == 0)
                && !map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp049>().Any(pawn => !pawn.Dead);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_049Kind");
            IntVec3 cell;
            if (kind == null || !CellFinder.TryFindRandomEdgeCellWith(
                candidate => ScpSpawnCells.FootprintClear(candidate, map, kind.race), map, 0f, out cell))
                return false;
            Pawn_Scp049 doctor = Pawn_Scp049.Generate();
            if (doctor == null) return false;
            GenSpawn.Spawn(doctor, cell, map);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("049");
            SendStandardLetter(parms, doctor);
            return true;
        }
    }

    public sealed class Scp049ArrivalComponent : MapComponent
    {
        private bool readyForVisit = true;
        private int lastVisitTick = -60000;

        public Scp049ArrivalComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 0 || !map.IsPlayerHome || !ModsConfig.AnomalyActive) return;
            if (Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("049")) return;
            if (!IncidentWorker_Visit049.HasArrivalThreshold(map)) { readyForVisit = true; return; }
            if (!readyForVisit || now - lastVisitTick < 60000) return;
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("SCP_Visit049");
            if (incident == null) return;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            if (!incident.Worker.TryExecute(parms)) return;
            readyForVisit = false;
            lastVisitTick = now;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref readyForVisit, "scp049ReadyForVisit", true);
            Scribe_Values.Look(ref lastVisitTick, "scp049LastVisitTick", -60000);
        }
    }
}
