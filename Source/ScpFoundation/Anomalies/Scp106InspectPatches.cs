using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    [StaticConstructorOnStartup]
    public static class Scp106InspectPatches
    {
        private static readonly Texture2D HealthFill = SolidColorMaterials.NewSolidColorTexture(
            new Color(0.55f, 0.22f, 0.2f));
        private static readonly Texture2D HealthBackground = SolidColorMaterials.NewSolidColorTexture(
            new Color(0.15f, 0.15f, 0.15f));

        static Scp106InspectPatches()
        {
            new Harmony("local.scp5000.foundation.scp106.inspect").PatchAll(typeof(Scp106InspectPatches).Assembly);
        }

        [HarmonyPatch(typeof(InspectPaneFiller), "DrawHealth")]
        private static class HealthPatch
        {
            private static bool Prefix(WidgetRow __0, Thing __1)
            {
                Pawn_Scp106 entity = __1 as Pawn_Scp106;
                if (entity != null)
                {
                    __0.FillableBar(115f, 17f, entity.Integrity / (float)Pawn_Scp106.MaximumIntegrity,
                        "SCP_106HealthLabel".Translate(entity.Integrity, Pawn_Scp106.MaximumIntegrity),
                        HealthFill, HealthBackground);
                    return false;
                }
                Pawn_Scp049 doctor = __1 as Pawn_Scp049;
                if (doctor == null) return true;
                __0.FillableBar(115f, 17f, doctor.Integrity / (float)Pawn_Scp049.MaximumIntegrity,
                    "SCP_049HealthLabel".Translate(doctor.Integrity, Pawn_Scp049.MaximumIntegrity),
                    HealthFill, HealthBackground);
                return false;
            }
        }

        [HarmonyPatch(typeof(InspectPaneFiller), "DrawMood")]
        private static class MoodPatch
        {
            private static bool Prefix(Pawn __1)
            {
                return !(__1 is Pawn_Scp106) && !(__1 is Pawn_Scp049);
            }
        }
    }
}
