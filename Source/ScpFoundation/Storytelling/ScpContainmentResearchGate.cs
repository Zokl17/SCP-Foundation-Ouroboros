using HarmonyLib;
using Verse;

namespace ScpFoundation.Storytelling
{
    // Special containment plans become available with their assignments.
    internal static class ScpContainmentResearchGate
    {
        private static bool Locked(ResearchProjectDef project)
        {
            switch (project.defName)
            {
                case "SCP_106Chamber":
                case "SCP_882Reservoir":
                case "SCP_682AcidContainment":
                case "SCP_127LockerResearch":
                case "SCP_262HangerResearch":
                case "SCP_093MirrorResearch":
                    return Current.Game == null || !Current.Game.GetComponent<ScpArrivalRegistry>()
                        .IsContainmentResearchUnlocked(project.defName);
                default: return false;
            }
        }

        [HarmonyPatch(typeof(ResearchProjectDef), "get_IsHidden")]
        private static class HiddenPatch
        {
            private static void Postfix(ResearchProjectDef __instance, ref bool __result)
            {
                if (Locked(__instance)) __result = true;
            }
        }

        [HarmonyPatch(typeof(ResearchProjectDef), "get_CanStartNow")]
        private static class StartPatch
        {
            private static void Postfix(ResearchProjectDef __instance, ref bool __result)
            {
                if (Locked(__instance)) __result = false;
            }
        }
    }
}
