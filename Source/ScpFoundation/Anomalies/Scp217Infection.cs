using RimWorld;
using Verse;

namespace ScpFoundation.Anomalies
{
    // The sealed sample has no aura or ticking exposure. Only a broken vessel or
    // actual contact with an infected pawn can start the clockwork infection.
    public static class Scp217Infection
    {
        private static HediffDef InfectionDef
        {
            get { return DefDatabase<HediffDef>.GetNamedSilentFail("SCP_217Infection"); }
        }

        public static bool IsInfected(Pawn pawn)
        {
            HediffDef infection = InfectionDef;
            return infection != null && pawn != null && pawn.health != null
                && pawn.health.hediffSet.HasHediff(infection);
        }

        public static void Expose(Pawn pawn)
        {
            HediffDef infection = InfectionDef;
            if (infection == null || pawn == null || pawn.Dead || pawn.health == null
                || !pawn.RaceProps.IsFlesh || Scp008Infection.HasSealedSuit(pawn)
                || pawn.health.hediffSet.HasHediff(infection)) return;
            pawn.health.AddHediff(infection);
            if (pawn.IsColonistPlayerControlled)
                Find.LetterStack.ReceiveLetter("SCP_217ExposureTitle".Translate(),
                    "SCP_217ExposureBody".Translate(pawn.Named("PAWN")),
                    LetterDefOf.ThreatSmall, pawn);
        }

        public static void ExposeToBrokenSample(Map map, IntVec3 position)
        {
            if (map == null) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, 1.5f, true))
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map))
                {
                    Pawn pawn = thing as Pawn;
                    if (pawn != null) Expose(pawn);
                }
            }
            Find.LetterStack.ReceiveLetter("SCP_217BreachTitle".Translate(),
                "SCP_217BreachBody".Translate(), LetterDefOf.ThreatBig,
                new LookTargets(position, map));
        }

        public static void AfterDamage(Thing victim, DamageInfo info,
            DamageWorker.DamageResult result)
        {
            Pawn target = victim as Pawn;
            Pawn source = info.Instigator as Pawn;
            if (target == null || source == null || target == source
                || result.totalDamageDealt <= 0f || target.Map == null
                || source.Map != target.Map || !target.Position.AdjacentTo8Way(source.Position)
                || (info.Def != DamageDefOf.Bite && info.Def != DamageDefOf.Cut
                    && info.Def != DamageDefOf.Blunt)) return;
            if (IsInfected(source)) Expose(target);
            if (IsInfected(target)) Expose(source);
        }
    }

    public sealed class Hediff_Scp217Infection : HediffWithComps
    {
        public override void Tick()
        {
            base.Tick();
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Dead
                || !pawn.IsHashIntervalTick(2500)) return;
            // Only an infected pawn checks adjacent cells, once per in-game hour.
            // The sealed sample itself never performs a map scan.
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, 1.5f, true))
            {
                if (!cell.InBounds(pawn.Map) || cell == pawn.Position) continue;
                foreach (Thing thing in cell.GetThingList(pawn.Map))
                {
                    Pawn other = thing as Pawn;
                    if (other != null && other != pawn) Scp217Infection.Expose(other);
                }
            }
        }
    }
}
