using System.Linq;
using RimWorld;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Scp131 : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            ThingDef a = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_131APawn");
            ThingDef b = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_131BPawn");
            return map != null && map.IsPlayerHome && a != null && b != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && map.mapPawns.FreeColonistsSpawnedCount >= 3
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("131")
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("173")
                && !map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.def == a || pawn.def == b)
                && !map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("SCP_131A")).Any()
                && !map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("SCP_131B")).Any();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef a = DefDatabase<ThingDef>.GetNamed("SCP_131APawn");
            ThingDef b = DefDatabase<ThingDef>.GetNamed("SCP_131BPawn");
            IntVec3 first;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                cell => ScpSpawnCells.FootprintClear(cell, map, a), map, 0f, out first)) return false;
            IntVec3 second = IntVec3.Invalid;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(first, 3f, true))
                if (cell != first && ScpSpawnCells.FootprintClear(cell, map, b)) { second = cell; break; }
            if (!second.IsValid) return false;
            Thing pod = GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("SCP_131APawn"), Faction.OfPlayer), first, map);
            GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("SCP_131BPawn"), Faction.OfPlayer), second, map);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("131");
            map.GetComponent<Scp131ArrivalComponent>().Schedule173();
            SendStandardLetter(parms, pod);
            return true;
        }
    }

    public sealed class Scp131ArrivalComponent : MapComponent
    {
        private int scp173DueTick = -1;

        public Scp131ArrivalComponent(Map map) : base(map) { }

        public void Schedule173() { scp173DueTick = Find.TickManager.TicksGame + 12500; }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (scp173DueTick < 0 || now < scp173DueTick || now % 250 != 0 || !map.IsPlayerHome)
                return;
            if (Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("173"))
            { scp173DueTick = -1; return; }
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("SCP_Discover173");
            if (incident == null) return;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            if (incident.Worker.TryExecute(parms)) scp173DueTick = -1;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref scp173DueTick, "scp131Warned173DueTick", -1);
        }
    }
}
