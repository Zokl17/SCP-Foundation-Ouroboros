using HarmonyLib;
using RimWorld;
using Verse;

namespace ScpFoundation.Anomalies
{
    // SCP-008 is passed by an actual wound exposed to infected body fluids, not by
    // sharing a room, breathing the same air, or using the same water source.
    public static class Scp008Infection
    {
        public static bool IsInfected(Pawn pawn)
        {
            HediffDef infection = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_008Infection");
            return infection != null && pawn != null && pawn.health != null
                && pawn.health.hediffSet.HasHediff(infection);
        }

        public static bool HasSealedSuit(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null) return false;
            bool foundationBody = false, foundationHood = false;
            bool gocBody = false, gocHelmet = false;
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (apparel.def.defName == "SCP_008BioSuit") foundationBody = true;
                if (apparel.def.defName == "SCP_008BioHelmet") foundationHood = true;
                if (apparel.def.defName == "SCP_GocWhiteExosuit") gocBody = true;
                if (apparel.def.defName == "SCP_GocWhiteHelmet") gocHelmet = true;
            }
            return (foundationBody && foundationHood) || (gocBody && gocHelmet);
        }

        public static void Expose(Pawn pawn)
        {
            HediffDef infection = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_008Infection");
            if (infection == null || pawn == null || pawn.Dead || pawn.health == null
                || HasSealedSuit(pawn) || pawn.health.hediffSet.HasHediff(infection)) return;
            pawn.health.AddHediff(infection);
            if (pawn.IsColonistPlayerControlled)
                Find.LetterStack.ReceiveLetter("SCP_008ExposureTitle".Translate(),
                    "SCP_008ExposureBody".Translate(pawn.Named("PAWN")), LetterDefOf.ThreatSmall, pawn);
        }

        public static void AfterDamage(Thing victim, DamageInfo info, DamageWorker.DamageResult result)
        {
            Pawn target = victim as Pawn;
            Pawn source = info.Instigator as Pawn;
            if (target == null || source == null || target == source || result.totalDamageDealt <= 0f
                || !IsInfected(source) || HasSealedSuit(target)) return;
            // Only an adjacent bite, cut, or blunt attack can put bodily fluid in a wound.
            // Gunfire, explosions, toxic fallout and nearby corpses cannot transmit it.
            if (target.Map == null || source.Map != target.Map || !target.Position.AdjacentTo8Way(source.Position)
                || (info.Def != DamageDefOf.Bite && info.Def != DamageDefOf.Cut && info.Def != DamageDefOf.Blunt)) return;
            if (Rand.Chance(0.35f)) Expose(target);
        }

        public static void ExposeToBrokenVial(Map map, IntVec3 position)
        {
            if (map == null) return;
            // The one-time splash can touch eyes or mouth. It is not a lingering
            // cloud, room effect, contaminated water tile, or proximity infection.
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, 1.5f, true))
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map))
                {
                    Pawn pawn = thing as Pawn;
                    if (pawn != null && Rand.Chance(0.5f)) Expose(pawn);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Thing), "TakeDamage")]
    internal static class Scp008ContactPatch
    {
        internal sealed class DamageContext
        {
            public Map map;
            public IntVec3 position;
            public bool is217;
        }

        private static void Prefix(Thing __instance, out DamageContext __state)
        {
            string name = __instance.def.defName;
            __state = (name == "SCP_008SampleCase" || name == "SCP_OuroborosSealed217Sample")
                && __instance.Spawned
                ? new DamageContext { map = __instance.Map, position = __instance.Position,
                    is217 = name == "SCP_OuroborosSealed217Sample" } : null;
        }

        private static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result,
            DamageContext __state)
        {
            Scp008Infection.AfterDamage(__instance, dinfo, __result);
            Scp217Infection.AfterDamage(__instance, dinfo, __result);
            if (__state != null && __instance.Destroyed && dinfo.Def != DamageDefOf.Burn)
            {
                if (__state.is217)
                    Scp217Infection.ExposeToBrokenSample(__state.map, __state.position);
                else Scp008Infection.ExposeToBrokenVial(__state.map, __state.position);
            }
        }
    }
}
