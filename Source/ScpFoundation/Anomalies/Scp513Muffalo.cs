using System.Linq;
using RimWorld;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_Scp513Muffalo : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && map.IsPlayerHome && Current.Game != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && map.mapPawns.FreeColonistsSpawnedCount >= 2
                && DefDatabase<PawnKindDef>.GetNamedSilentFail("Muffalo") != null
                && DefDatabase<ThingDef>.GetNamedSilentFail("SCP_513Bell") != null
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("513");
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("Muffalo");
            IntVec3 cell;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                candidate => ScpSpawnCells.FootprintClear(candidate, map, kind.race),
                map, 0f, out cell)) return false;
            Pawn muffalo = PawnGenerator.GeneratePawn(kind);
            Thing bell = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SCP_513Bell"));
            if (muffalo.inventory == null || !muffalo.inventory.innerContainer.TryAdd(bell))
            {
                bell.Destroy(DestroyMode.Vanish);
                muffalo.Destroy(DestroyMode.Vanish);
                return false;
            }
            GenSpawn.Spawn(muffalo, cell, map);
            muffalo.mindState.exitMapAfterTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
            map.GetComponent<Scp513MuffaloComponent>().Begin(muffalo);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("513");
            SendStandardLetter(parms, muffalo);
            return true;
        }
    }

    public sealed class Scp513MuffaloComponent : MapComponent
    {
        private Pawn carrier;
        private int nextRingTick;

        public Scp513MuffaloComponent(Map map) : base(map) { }

        public void Begin(Pawn muffalo)
        {
            carrier = muffalo;
            nextRingTick = Find.TickManager.TicksGame + 12500;
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (carrier == null || now % 250 != 0) return;
            if (!carrier.Spawned || carrier.Dead || carrier.Map != map || carrier.inventory == null)
            { carrier = null; return; }
            Thing bell = carrier.inventory.innerContainer
                .FirstOrDefault(thing => thing.def.defName == "SCP_513Bell");
            if (bell == null) { carrier = null; return; }
            if (now < nextRingTick) return;
            CompScp513 comp = bell.TryGetComp<CompScp513>();
            if (comp != null) comp.RingAt(map, carrier.Position);
            nextRingTick = now + 12500;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref carrier, "scp513Muffalo");
            Scribe_Values.Look(ref nextRingTick, "scp513NextRingTick", 0);
        }
    }
}
