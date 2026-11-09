using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    // Combat SCPs have no faction because they can also be contained as things.
    // Treat a loose, active one as hostile so allied MTF may acquire it normally.
    [HarmonyPatch(typeof(GenHostility), "HostileTo", new System.Type[] { typeof(Thing), typeof(Thing) })]
    internal static class ScpCombatHostilityPatch
    {
        private static void Postfix(Thing __0, Thing __1, ref bool __result)
        {
            if (__result || __0 == null || __1 == null) return;
            if (__0 is Pawn && ((Pawn)__0).RaceProps.Humanlike && ActiveScp(__1, __0)) __result = true;
            else if (__1 is Pawn && ((Pawn)__1).RaceProps.Humanlike && ActiveScp(__0, __1)) __result = true;
        }

        internal static bool ActiveScp(Thing candidate, Thing searcher)
        {
            if (!(candidate is Thing_Scp173 || candidate is Thing_Scp682
                || ScpCombatTargetingPatch.IsScp939(candidate) || candidate is Thing_Scp096)) return false;
            IAttackTarget target = candidate as IAttackTarget;
            return candidate.Spawned && target != null
                && !target.ThreatDisabled(searcher as IAttackTargetSearcher);
        }
    }

    [HarmonyPatch(typeof(GenHostility), "HostileTo", new System.Type[] { typeof(Thing), typeof(Faction) })]
    internal static class ScpCombatFactionHostilityPatch
    {
        private static void Postfix(Thing __0, Faction __1, ref bool __result)
        {
            if (!__result && __1 != null && ScpCombatHostilityPatch.ActiveScp(__0, null))
                __result = true;
        }
    }
}
