using System;
using CombatExtended;
using HarmonyLib;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.CombatExtendedCompat
{
    [StaticConstructorOnStartup]
    internal static class CeWeaponTraitImpactInstaller
    {
        static CeWeaponTraitImpactInstaller()
        {
            new Harmony("ScpFoundation.CombatExtended.WeaponTraitImpact")
                .PatchAll(typeof(CeWeaponTraitImpactInstaller).Assembly);
        }
    }

    [HarmonyPatch(typeof(ProjectileCE), "Impact")]
    internal static class CeProjectileTraitImpactPatch
    {
        private static void Prefix(ProjectileCE __instance, out ScpExternalImpactState __state)
        {
            __state = ScpExternalProjectileContext.Begin(__instance.equipment);
        }

        private static Exception Finalizer(Exception __exception, ScpExternalImpactState __state)
        {
            ScpExternalProjectileContext.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BulletCE), "Impact")]
    internal static class CeBulletTraitImpactPatch
    {
        private static void Prefix(BulletCE __instance, out ScpExternalImpactState __state)
        {
            __state = ScpExternalProjectileContext.Begin(__instance.equipment);
        }

        private static Exception Finalizer(Exception __exception, ScpExternalImpactState __state)
        {
            ScpExternalProjectileContext.End(__state);
            return __exception;
        }
    }
}
