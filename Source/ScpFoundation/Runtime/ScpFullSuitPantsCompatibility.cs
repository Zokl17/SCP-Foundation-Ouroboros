using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    internal static class ScpFullSuitPantsPolicy
    {
        internal static bool IsFullSuit(ThingDef def)
        {
            return def != null && (def.defName == "SCP_GocBlackSuit"
                || def.defName == "SCP_GocWhiteExosuit" || def.defName == "SCP_008BioSuit");
        }

        internal static bool IsPants(ThingDef def)
        {
            if (def == null || def.apparel == null || def.apparel.bodyPartGroups == null
                || def.apparel.layers == null) return false;
            bool coversLegs = false;
            foreach (BodyPartGroupDef group in def.apparel.bodyPartGroups)
            {
                if (group.defName == "Legs") coversLegs = true;
                else if (group.defName == "Torso" || group.defName == "Neck"
                    || group.defName == "Shoulders" || group.defName == "Arms") return false;
            }
            if (!coversLegs) return false;
            foreach (ApparelLayerDef layer in def.apparel.layers)
                if (layer.defName == "OnSkin") return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ApparelUtility), "CanWearTogether",
        new Type[] { typeof(ThingDef), typeof(ThingDef), typeof(BodyDef) })]
    internal static class ScpFullSuitPantsCompatibility
    {
        private static bool Prefix(ThingDef __0, ThingDef __1, ref bool __result)
        {
            if (!(ScpFullSuitPantsPolicy.IsFullSuit(__0) && ScpFullSuitPantsPolicy.IsPants(__1))
                && !(ScpFullSuitPantsPolicy.IsFullSuit(__1) && ScpFullSuitPantsPolicy.IsPants(__0)))
                return true;
            __result = false;
            return false;
        }
    }

    // Existing saves may already contain both garments, before the compatibility rule existed.
    [HarmonyPatch(typeof(Pawn_ApparelTracker), "ApparelTrackerTickRare")]
    internal static class ScpFullSuitExistingPantsCleanup
    {
        private static void Postfix(Pawn_ApparelTracker __instance, Pawn ___pawn)
        {
            if (___pawn == null || !___pawn.Spawned) return;
            if (___pawn.Faction != null && ___pawn.Faction.def.defName == "SCP_GlobalOccultCoalition")
            {
                bool recolored = false;
                foreach (Apparel worn in __instance.WornApparel)
                {
                    string name = worn.def.defName;
                    if (name != "SCP_GocBlackSuit" && name != "SCP_GocBlackHelmet"
                        && name != "SCP_GocWhiteExosuit" && name != "SCP_GocWhiteHelmet") continue;
                    Color color = worn.DrawColor;
                    if (color.r > 0.25f || color.g > 0.25f || color.b > 0.25f) continue;
                    worn.DrawColor = Color.white;
                    recolored = true;
                }
                if (recolored) ___pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            bool hasFullSuit = false;
            foreach (Apparel worn in __instance.WornApparel)
                if (ScpFullSuitPantsPolicy.IsFullSuit(worn.def)) { hasFullSuit = true; break; }
            if (!hasFullSuit) return;
            foreach (Apparel worn in __instance.WornApparel)
                if (ScpFullSuitPantsPolicy.IsPants(worn.def)) { __instance.TryDrop(worn); break; }
        }
    }
}
