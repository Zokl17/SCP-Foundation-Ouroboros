using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class Building_Scp354 : Building
    {
        private int nextEmergenceTick;
        private int emergences;

        public override string GetInspectString()
        {
            return "SCP_354Inspect".Translate();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            yield break;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = true;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad && nextEmergenceTick <= 0)
                nextEmergenceTick = Find.TickManager.TicksGame + Rand.RangeInclusive(2, 4) * 60000;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextEmergenceTick, "scp354NextEmergence", 0);
            Scribe_Values.Look(ref emergences, "scp354Emergences", 0);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Spawned || Map == null || Find.TickManager.TicksGame < nextEmergenceTick) return;
            if (!TryEmerge())
            {
                nextEmergenceTick = Find.TickManager.TicksGame + 2500;
                return;
            }
            nextEmergenceTick = Find.TickManager.TicksGame + Rand.RangeInclusive(8, 14) * 60000;
        }

        private bool TryEmerge()
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(emergences % 2 == 0 ? "Warg" : "Megaspider");
            if (kind == null) return false;
            CellRect footprint = GenAdj.OccupiedRect(Position, Rot4.North, def.size);
            List<IntVec3> exits = new List<IntVec3>();
            for (int x = footprint.minX - 2; x <= footprint.maxX + 2; x++)
                for (int z = footprint.minZ - 2; z <= footprint.maxZ + 2; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (footprint.Contains(cell) || !cell.InBounds(Map) || !cell.Standable(Map)
                        || cell.GetThingList(Map).Any(thing => thing is Pawn || thing.def.category == ThingCategory.Building))
                        continue;
                    exits.Add(cell);
                }
            if (exits.Count == 0) return false;
            Pawn creature = PawnGenerator.GeneratePawn(kind);
            if (creature == null) return false;
            GenSpawn.Spawn(creature, exits.RandomElement(), Map);
            creature.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            emergences++;
            string body = "SCP_354EmergenceBody".Translate();
            ScpEventRecord entry = ScpEventJournal.Add("SCP-354", this, body);
            Find.LetterStack.ReceiveLetter(ScpEventJournal.Notice(entry, "SCP_354EmergenceTitle".Translate()),
                body, LetterDefOf.ThreatBig, this);
            return true;
        }
    }
}
