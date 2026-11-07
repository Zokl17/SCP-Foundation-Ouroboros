using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ScpFoundation.Anomalies
{
    // SCP-682 occupies a 2x2 footprint and uses the containment transport system.
    // It cannot own a faction, so expose its hostility to vanilla target searches instead.
    [HarmonyPatch(typeof(GenHostility), "HostileTo",
        new Type[] { typeof(Thing), typeof(Faction) })]
    internal static class Scp682FactionHostilityPatch
    {
        private static void Postfix(Thing __0, Faction __1, ref bool __result)
        {
            if (__0 is Thing_Scp682 && __1 != null)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(GenHostility), "HostileTo",
        new Type[] { typeof(Thing), typeof(Thing) })]
    internal static class Scp682ThingHostilityPatch
    {
        private static void Postfix(Thing __0, Thing __1, ref bool __result)
        {
            if (__0 != __1 && (__0 is Thing_Scp682 && __1 != null && __1.Faction != null
                || __1 is Thing_Scp682 && __0 != null && __0.Faction != null))
                __result = true;
        }
    }
}
