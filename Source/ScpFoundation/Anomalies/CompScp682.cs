using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp682 : CompProperties
    {
        public CompProperties_Scp682() { compClass = typeof(CompScp682); }
    }

    public sealed class CompScp682 : ThingComp
    {
        private const int InitialStunTicks = 120000;
        private const int StunTicks = 15000;
        private const int StunThreshold = 240;
        private static readonly IntVec3[] Directions =
            { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };
        private int stunnedUntilTick;
        private int nextMoveTick;
        private int nextAttackTick;

        public bool IsStunned { get { return Find.TickManager.TicksGame < stunnedUntilTick; } }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && stunnedUntilTick == 0)
            {
                parent.HitPoints = 150;
                stunnedUntilTick = Find.TickManager.TicksGame + InitialStunTicks;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref stunnedUntilTick, "scp682StunnedUntil", 0);
            Scribe_Values.Look(ref nextMoveTick, "scp682NextMove", 0);
            Scribe_Values.Look(ref nextAttackTick, "scp682NextAttack", 0);
        }

        public void OnDamaged()
        {
            if (parent.HitPoints <= StunThreshold && !IsStunned)
                stunnedUntilTick = Find.TickManager.TicksGame + StunTicks;
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return null;
            CompScp scp = parent.TryGetComp<CompScp>();
            string state = scp != null && scp.IsInCustody && !scp.IsBreached
                ? "SCP_682Contained".Translate().ToString()
                : IsStunned
                    ? "SCP_682Stunned".Translate(GenDate.ToStringTicksToPeriod(stunnedUntilTick - Find.TickManager.TicksGame, false)).ToString()
                    : "SCP_682Hunting".Translate().ToString();
            return state + "\n" + "SCP_682Integrity".Translate(parent.HitPoints, parent.MaxHitPoints);
        }

        public override void CompTick()
        {
            if (!parent.Spawned) return;
            int now = Find.TickManager.TicksGame;
            if (now % 90 == 0 && parent.HitPoints < parent.MaxHitPoints)
                parent.HitPoints++;
            if (IsStunned) return;
            CompScp scp = parent.TryGetComp<CompScp>();
            if (scp != null && scp.IsInCustody && !scp.IsBreached) return;
            if (now % 15 != 0) return;

            Map map = parent.Map;
            Pawn target = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Downed && pawn.RaceProps.Humanlike
                    && pawn.Position.DistanceToSquared(parent.Position) <= 900)
                .OrderBy(pawn => pawn.Position.DistanceToSquared(parent.Position)).FirstOrDefault();
            if (target != null && parent.Position.DistanceToSquared(target.Position) <= 2
                && ScpSurveillance.Sight(parent.Position, target.Position, map))
            {
                if (now >= nextAttackTick)
                {
                    nextAttackTick = now + 150;
                    target.TakeDamage(new DamageInfo(DamageDefOf.Bite, 38f, 0.6f, -1f, parent));
                }
                return;
            }
            if (now < nextMoveTick) return;
            nextMoveTick = now + (target == null ? 240 : 60);
            IntVec3 origin = parent.Position;
            IntVec3? step = Directions.Select(direction => origin + direction)
                .Where(cell => CanStep(cell, map))
                .OrderBy(cell => target == null ? Rand.Value : cell.DistanceToSquared(target.Position))
                .Select(cell => (IntVec3?)cell).FirstOrDefault();
            if (target != null && (!step.HasValue || step.Value.DistanceToSquared(target.Position)
                >= origin.DistanceToSquared(target.Position)) && BreakBlockingEdifice(origin, target.Position, map)) return;
            if (!step.HasValue) return;
            Rot4 facing = step.Value.x > origin.x ? Rot4.East
                : step.Value.x < origin.x ? Rot4.West
                : step.Value.z > origin.z ? Rot4.North : Rot4.South;
            parent.DeSpawn();
            GenSpawn.Spawn(parent, step.Value, map, facing);
        }

        private bool BreakBlockingEdifice(IntVec3 origin, IntVec3 target, Map map)
        {
            Building obstacle = Directions.SelectMany(direction => GenAdj.OccupiedRect(origin + direction, Rot4.North, parent.def.size))
                .Where(cell => cell.InBounds(map) && cell.DistanceToSquared(target) < origin.DistanceToSquared(target))
                .Select(cell => cell.GetEdifice(map))
                .Where(building => building != null && !building.Destroyed && !(building is Building_Door && ((Building_Door)building).Open))
                .OrderBy(building => building.Position.DistanceToSquared(target)).FirstOrDefault();
            if (obstacle == null) return false;
            obstacle.TakeDamage(new DamageInfo(DamageDefOf.Crush, 70f, 0f, -1f, parent));
            return true;
        }

        private bool CanStep(IntVec3 cell, Map map)
        {
            foreach (IntVec3 occupied in GenAdj.OccupiedRect(cell, Rot4.North, parent.def.size))
            {
                if (!occupied.InBounds(map) || !occupied.Standable(map) || occupied.Fogged(map)
                    || occupied.GetFirstPawn(map) != null
                    || occupied.GetThingList(map).Any(thing => thing != parent && thing.def.category == ThingCategory.Item))
                    return false;
                Building edifice = occupied.GetEdifice(map);
                Building_Door door = edifice as Building_Door;
                if (edifice != null && (door == null || !door.Open)) return false;
            }
            return true;
        }
    }
}
