using HarmonyLib;
using RimWorld;
using ScpFoundation.Definitions;
using ScpFoundation.Knowledge;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    internal static class Scp262WearPolicy
    {
        internal static bool Allowed(Pawn pawn)
        {
            if (pawn == null || Current.Game == null) return false;
            ScpDef anomaly = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly262");
            return anomaly != null
                && Current.Game.GetComponent<ScpKnowledgeComponent>().LevelFor(anomaly) >= KnowledgeLevel.Documented
                && Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(pawn) >= 3;
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), "Wear")]
    internal static class Scp262WearRestriction
    {
        private static bool Prefix(Apparel newApparel, Pawn ___pawn)
        {
            if (newApparel == null || newApparel.def.defName != "SCP_262Coat" || Scp262WearPolicy.Allowed(___pawn))
                return true;
            if (___pawn != null && ___pawn.IsColonistPlayerControlled)
                Messages.Message("SCP_262WearLocked".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), "ApparelTrackerTickRare")]
    internal static class Scp262WornRestriction
    {
        private static void Postfix(Pawn_ApparelTracker __instance, Pawn ___pawn)
        {
            if (___pawn == null || !___pawn.Spawned || Scp262WearPolicy.Allowed(___pawn)) return;
            foreach (Apparel worn in __instance.WornApparel)
                if (worn.def.defName == "SCP_262Coat")
                {
                    if (__instance.TryDrop(worn) && ___pawn.IsColonistPlayerControlled)
                        Messages.Message("SCP_262WearLocked".Translate(), MessageTypeDefOf.RejectInput, false);
                    break;
                }
        }
    }
}
