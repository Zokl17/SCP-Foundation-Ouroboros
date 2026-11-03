using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class HediffCompProperties_BrokenGodCanticle : HediffCompProperties
    {
        public HediffCompProperties_BrokenGodCanticle()
        {
            compClass = typeof(HediffComp_BrokenGodCanticle);
        }
    }

    // Church cantors disable colony machinery with an EMP rite during the siege.
    public sealed class HediffComp_BrokenGodCanticle : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            Pawn caster = Pawn;
            if (caster == null || !caster.Spawned || caster.Dead || caster.Downed
                || Find.TickManager.TicksGame % 1800 != caster.thingIDNumber % 1800) return;
            Building target = caster.Map.listerBuildings.allBuildingsColonist
                .Where(building => building != null && building.Spawned
                    && building.TryGetComp<CompPowerTrader>() != null
                    && caster.Position.DistanceToSquared(building.Position) <= 400
                    && GenSight.LineOfSight(caster.Position, building.Position, caster.Map))
                .OrderBy(building => caster.Position.DistanceToSquared(building.Position)).FirstOrDefault();
            if (target == null) return;
            FleckMaker.Static(caster.Position, caster.Map, FleckDefOf.MicroSparks, 2f);
            GenExplosion.DoExplosion(target.Position, caster.Map, 2.4f, DamageDefOf.EMP, caster);
        }
    }
}
