using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Pawn_Scp354Creature : Pawn
    {
        public override IEnumerable<Gizmo> GetGizmos() { yield break; }

        // Projectiles never connect with SCP-354-15. The other cat can still kill it in melee.
        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (def.defName == "SCP_354IceCat" && dinfo.Def != null && dinfo.Def.isRanged)
            {
                absorbed = true;
                return;
            }
            Pawn_Scp354Creature attacker = dinfo.Instigator as Pawn_Scp354Creature;
            if (def.defName == "SCP_354MagmaCat" && attacker != null
                && attacker.def.defName == "SCP_354IceCat")
            {
                absorbed = true;
                return;
            }
            base.PreApplyDamage(ref dinfo, out absorbed);
        }

        protected override void Tick()
        {
            base.Tick();
            if (def.defName != "SCP_354Reptile" || !Spawned || Dead || Downed
                || Find.TickManager.TicksGame % 250 != 0) return;
            Building target = null;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, 8f, true))
            {
                if (!cell.InBounds(Map) || cell == Position) continue;
                Building wall = cell.GetEdifice(Map);
                if (wall != null && wall.def.defName.Contains("Wall"))
                {
                    target = wall;
                    break;
                }
            }
            if (target == null) return;
            if (target.Position.DistanceToSquared(Position) <= 2)
                target.TakeDamage(new DamageInfo(DamageDefOf.Crush, 42f, 0f, -1f, this));
            else if (jobs != null && (CurJob == null || CurJob.def != JobDefOf.AttackMelee
                || CurJob.targetA.Thing != target))
                jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, target),
                    JobCondition.InterruptForced);
        }
    }

    // Keep vanilla hunting/taming designators from issuing orders for quest entities.
    [HarmonyPatch(typeof(Designator_Hunt), "CanDesignateThing")]
    internal static class Scp354HuntPatch
    {
        private static void Postfix(Thing t, ref AcceptanceReport __result)
        {
            if (t is Pawn_Scp354Creature) __result = false;
        }
    }

    [HarmonyPatch(typeof(Designator_Tame), "CanDesignateThing")]
    internal static class Scp354TamePatch
    {
        private static void Postfix(Thing t, ref AcceptanceReport __result)
        {
            if (t is Pawn_Scp354Creature) __result = false;
        }
    }

    public sealed class Building_Scp354Tentacles : Building
    {
        private const int RetreatTicks = 8000;
        private int emergedTick = -1;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad && emergedTick < 0)
                emergedTick = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref emergedTick, "scp354TentaclesEmergedTick", -1);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Spawned || Map == null) return;
            if (emergedTick >= 0 && Find.TickManager.TicksGame - emergedTick >= RetreatTicks)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            Pawn victim = Map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && !p.Dead && !p.Downed
                    && p.Position.DistanceToSquared(Position) <= 25)
                .OrderBy(p => p.Position.DistanceToSquared(Position)).FirstOrDefault();
            if (victim != null)
                victim.TakeDamage(new DamageInfo(DamageDefOf.Crush, 12f, 0f, -1f, this));
        }
    }
}
