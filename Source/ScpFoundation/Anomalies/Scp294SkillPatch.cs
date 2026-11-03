using HarmonyLib;
using RimWorld;

namespace ScpFoundation.Anomalies
{
    [HarmonyPatch(typeof(SkillRecord), "Level", MethodType.Getter)]
    internal static class Scp294SkillPatch
    {
        private static void Postfix(SkillRecord __instance, ref int __result)
        {
            if (__result >= 20 || __instance.Pawn == null || __instance.Pawn.health == null) return;
            string effect = __instance.def == SkillDefOf.Medicine ? "SCP_294MedicalKnowledge"
                : __instance.def == SkillDefOf.Construction ? "SCP_294EngineeringKnowledge"
                : __instance.def == SkillDefOf.Shooting ? "SCP_294WeaponKnowledge" : null;
            if (effect == null) return;
            foreach (var hediff in __instance.Pawn.health.hediffSet.hediffs)
                if (hediff.def.defName == effect)
                {
                    __result = System.Math.Min(20, __result + 10);
                    break;
                }
        }
    }
}
