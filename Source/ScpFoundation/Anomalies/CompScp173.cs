using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp173 : CompProperties
    {
        public int moveIntervalTicks = 18;
        public CompProperties_Scp173() { compClass = typeof(CompScp173); }
    }

    public sealed class CompScp173 : ThingComp
    {
        private int nextMoveTick;
        private int nextAttackTick;
        private int nextResidueTick;
        private static readonly IntVec3[] Directions =
            { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && nextMoveTick == 0)
                nextMoveTick = Find.TickManager.TicksGame + 300;
            if (nextResidueTick == 0)
                nextResidueTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref nextMoveTick, "scp173NextMoveTick", 0);
            Scribe_Values.Look(ref nextAttackTick, "scp173NextAttackTick", 0);
            Scribe_Values.Look(ref nextResidueTick, "scp173NextResidueTick", 0);
        }

        public void DelayAfterFailedTransport()
        {
            nextMoveTick = System.Math.Max(nextMoveTick, Find.TickManager.TicksGame + 6000);
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return null;
            CompScp scp = parent.TryGetComp<CompScp>();
            if (scp != null && !ContainmentProtection.AllowsEscape(scp.IsInCustody,
                scp.IsCaptureGraceActive, scp.IsBreached)) return "SCP_173Secured".Translate();
            return DirectObservers().Any() || Thing_Scp131.Watches173(parent)
                ? "SCP_173Observed".Translate() : "SCP_173Unobserved".Translate();
        }

        public override void CompTick()
        {
            if (!parent.Spawned) return;
            int now = Find.TickManager.TicksGame;
            if (now >= nextResidueTick)
            {
                nextResidueTick = now + GenDate.TicksPerDay;
                ThingDef residue = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_173BrownResidue");
                if (residue != null) FilthMaker.TryMakeFilth(parent.Position, parent.Map, residue);
            }
            if (now < nextMoveTick) return;
            nextMoveTick = now + ((CompProperties_Scp173)props).moveIntervalTicks;
            CompScp scp = parent.TryGetComp<CompScp>();
            if (scp != null && !ContainmentProtection.AllowsEscape(scp.IsInCustody,
                scp.IsCaptureGraceActive, scp.IsBreached)) return;
            // Two dedicated escorts keep the statue watched while closing in;
            // otherwise it can hop away between their pathing steps forever.
            if (Scp173Transport.TeamWatching(parent) || DirectObservers().Any()
                || Thing_Scp131.Watches173(parent)) return;

            Map map = parent.Map;
            Pawn target = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Downed && pawn.RaceProps.Humanlike
                    && pawn.Position.DistanceToSquared(parent.Position) <= 625)
                .OrderBy(pawn => pawn.Position.DistanceToSquared(parent.Position)).FirstOrDefault();
            if (target == null) return;
            if (parent.Position.DistanceToSquared(target.Position) <= 2)
            {
                if (now >= nextAttackTick) Attack(target, now);
                return;
            }
            IntVec3 step = NextStep(parent.Position, target.Position, map);
            if (!step.IsValid) return;
            parent.DeSpawn();
            GenSpawn.Spawn(parent, step, map);
        }

        private IEnumerable<Pawn> DirectObservers()
        {
            Map map = parent.Map;
            int now = Find.TickManager.TicksGame;
            float light = map.glowGrid.GroundGlowAt(parent.Position);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || !pawn.RaceProps.Humanlike || pawn.Dead || pawn.Downed) continue;
                IntVec3 delta = parent.Position - pawn.Position;
                if (delta.LengthHorizontalSquared > 400) continue;
                IntVec3 facing = pawn.Rotation.FacingCell;
                if (Scp173ObservationPolicy.IsWatching(delta.x, delta.z, facing.x, facing.z,
                    pawn.Awake() && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight),
                    ScpSurveillance.Sight(pawn.Position, parent.Position, map), light, now, pawn.thingIDNumber))
                    yield return pawn;
            }
        }

        private void Attack(Pawn target, int now)
        {
            nextAttackTick = now + 90;
            BodyPartRecord neck = target.RaceProps.body.AllParts.FirstOrDefault(part => part.def.defName == "Neck");
            target.TakeDamage(new DamageInfo(DamageDefOf.Crush, 80f, 999f, -1f, parent, neck));
            if (!target.Dead)
                target.TakeDamage(new DamageInfo(DamageDefOf.Crush, 40f, 999f, -1f, parent));
        }

        private IntVec3 NextStep(IntVec3 origin, IntVec3 target, Map map)
        {
            Queue<IntVec3> pending = new Queue<IntVec3>();
            Dictionary<IntVec3, IntVec3> previous = new Dictionary<IntVec3, IntVec3>();
            pending.Enqueue(origin);
            previous.Add(origin, origin);
            while (pending.Count > 0 && previous.Count < 2400)
            {
                IntVec3 current = pending.Dequeue();
                foreach (IntVec3 direction in Directions)
                {
                    IntVec3 next = current + direction;
                    if (previous.ContainsKey(next) || !next.InBounds(map) || !next.Standable(map)
                        || System.Math.Abs(next.x - origin.x) > 28
                        || System.Math.Abs(next.z - origin.z) > 28
                        || next.GetThingList(map).Any(thing => thing is Pawn)) continue;
                    previous.Add(next, current);
                    if (next.DistanceToSquared(target) <= 2)
                    {
                        while (previous[next] != origin) next = previous[next];
                        return next;
                    }
                    pending.Enqueue(next);
                }
            }
            return IntVec3.Invalid;
        }
    }
}
