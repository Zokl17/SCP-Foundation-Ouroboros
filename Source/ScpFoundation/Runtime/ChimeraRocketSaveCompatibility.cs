using System;
using System.Xml;
using HarmonyLib;
using Verse;

namespace ScpFoundation.Runtime
{
    // PatchAll is already called by the existing SCP bootstrap. Only our old
    // UF-backed projectile is redirected; actual UF weapons are left untouched.
    [HarmonyPatch(typeof(BackCompatibility), "GetBackCompatibleType")]
    internal static class ChimeraRocketSaveCompatibility
    {
        private static bool Prefix(Type baseType, string providedClassName, XmlNode node, ref Type __result)
        {
            if (providedClassName != "TurbojetBackpack.Projectile_BezierMissile" || node == null
                || node["def"] == null || node["def"].InnerText != "SCP_ChimeraRocket"
                || !baseType.IsAssignableFrom(typeof(Projectile_ChimeraRocket))) return true;
            __result = typeof(Projectile_ChimeraRocket);
            return false;
        }
    }

    // Saved church-cache items used def names ending in digits before 0.4.0.
    // Scribe resolves them through BackCompatibility before looking up ThingDefs.
    [HarmonyPatch(typeof(BackCompatibility), "BackCompatibleDefName")]
    internal static class ChurchCacheDefNameSaveCompatibility
    {
        private static bool Prefix(Type defType, string defName, ref string __result)
        {
            if (defType != typeof(ThingDef)) return true;
            if (defName == "SCP_OuroborosSealed217")
                __result = "SCP_OuroborosSealed217Sample";
            else if (defName == "SCP_OuroborosParts629")
                __result = "SCP_OuroborosParts629Cache";
            else return true;
            return false;
        }
    }
}
