using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp939 : CompProperties
    {
        public CompProperties_Scp939() { compClass = typeof(CompScp939); }
    }

    public sealed class CompScp939 : ThingComp
    {
        private const int SedationTicks = 120000;
        private static readonly IntVec3[] Directions =
            { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };
        private int sedatedUntilTick;
        private int nextMoveTick;
        private int nextAttackTick;
        private int nextVoiceTick;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && sedatedUntilTick == 0)
                sedatedUntilTick = Find.TickManager.TicksGame + SedationTicks;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref sedatedUntilTick, "scp939SedatedUntil", 0);
            Scribe_Values.Look(ref nextMoveTick, "scp939NextMove", 0);
            Scribe_Values.Look(ref nextAttackTick, "scp939NextAttack", 0);
            Scribe_Values.Look(ref nextVoiceTick, "scp939NextVoice", 0);
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return null;
            CompScp scp = parent.TryGetComp<CompScp>();
            if (scp != null && scp.IsInCustody && !scp.IsBreached) return "SCP_939Contained".Translate();
            int remaining = sedatedUntilTick - Find.TickManager.TicksGame;
            return remaining > 0 && (scp == null || !scp.IsBreached)
                ? "SCP_939Sedated".Translate(GenDate.ToStringTicksToPeriod(remaining, false))
                : "SCP_939Hunting".Translate();
        }

        public override void CompTick()
        {
            if (!parent.Spawned) return;
            int now = Find.TickManager.TicksGame;
            CompScp scp = parent.TryGetComp<CompScp>();
            if (scp != null && scp.IsInCustody && !scp.IsBreached) return;
            if (now < sedatedUntilTick && (scp == null || !scp.IsBreached)) return;
            if (now % 15 != 0) return;

            Map map = parent.Map;
            Pawn target = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Downed && pawn.RaceProps.Humanlike
                    && pawn.Position.DistanceToSquared(parent.Position) <= 484)
                .OrderBy(pawn => pawn.Position.DistanceToSquared(parent.Position)).FirstOrDefault();
            if (target != null && now >= nextVoiceTick && target.Position.DistanceToSquared(parent.Position) <= 144)
            {
                nextVoiceTick = now + 15000;
                Messages.Message("SCP_939VoiceMimic".Translate(), parent, MessageTypeDefOf.NeutralEvent, false);
            }
            if (target != null && parent.Position.DistanceToSquared(target.Position) <= 2
                && ScpSurveillance.Sight(parent.Position, target.Position, map))
            {
                if (now >= nextAttackTick)
                {
                    nextAttackTick = now + 120;
                    target.TakeDamage(new DamageInfo(DamageDefOf.Bite, 14f, 0.3f, -1f, parent));
                }
                return;
            }
            if (now < nextMoveTick) return;
            nextMoveTick = now + (target == null ? 240 : 45);
            IntVec3? destination = Directions.Select(direction => parent.Position + direction)
                .Where(cell => CanStep(cell, map))
                .OrderBy(cell => target == null ? Rand.Value : cell.DistanceToSquared(target.Position))
                .Select(cell => (IntVec3?)cell).FirstOrDefault();
            if (!destination.HasValue) return;
            IntVec3 origin = parent.Position;
            Rot4 facing = destination.Value.x > origin.x ? Rot4.East
                : destination.Value.x < origin.x ? Rot4.West
                : destination.Value.z > origin.z ? Rot4.North : Rot4.South;
            parent.DeSpawn();
            GenSpawn.Spawn(parent, destination.Value, map, facing);
        }

        private static bool CanStep(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)
                || cell.GetFirstPawn(map) != null) return false;
            Building edifice = cell.GetEdifice(map);
            Building_Door door = edifice as Building_Door;
            return edifice == null || (door != null && door.Open);
        }
    }
}
