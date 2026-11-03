using HarmonyLib;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    [HarmonyPatch(typeof(Building_WorkTable), "CurrentlyUsableForBills")]
    internal static class FoundationWorkshopLinkPatch
    {
        private static void Postfix(Building_WorkTable __instance, ref bool __result)
        {
            if (!__result || (__instance.def.defName != "SCP_FoundationArmoryModule"
                && __instance.def.defName != "SCP_FoundationLabModule")) return;

            CompFacility facility = __instance.TryGetComp<CompFacility>();
            __result = facility != null && facility.LinkedBuildings.Any(bench =>
                bench != null && bench.Spawned && bench.def.defName == "SCP_FoundationCoreBench"
                && bench.TryGetComp<CompPowerTrader>() != null
                && bench.TryGetComp<CompPowerTrader>().PowerOn);
        }
    }

    [HarmonyPatch(typeof(Bill_Production), "ShouldDoNow")]
    internal static class Por7FusionChamberBillPatch
    {
        private static void Postfix(Bill_Production __instance, ref bool __result)
        {
            if (!__result || __instance.recipe == null || __instance.recipe.ProducedThingDef == null) return;
            string product = __instance.recipe.ProducedThingDef.defName;
            bool exosuit = product == "SCP_MtfExosuit" || product == "SCP_MtfExoHelmet";
            if (!exosuit && product != "SCP_Por7PowerCell" && product != "SCP_Por7PowerPack") return;

            Building_WorkTable source = __instance.billStack == null ? null : __instance.billStack.billGiver as Building_WorkTable;
            if (exosuit && (source == null || source.def.defName != "SCP_FoundationCoreBench")) return;
            Building_WorkTable lab = source;
            if (exosuit)
            {
                CompAffectedByFacilities coreFacilities = source.TryGetComp<CompAffectedByFacilities>();
                lab = coreFacilities == null ? null : coreFacilities.LinkedFacilitiesListForReading
                    .OfType<Building_WorkTable>().FirstOrDefault(bench => bench.Spawned
                        && bench.def.defName == "SCP_FoundationLabModule"
                        && bench.TryGetComp<CompPowerTrader>() != null
                        && bench.TryGetComp<CompPowerTrader>().PowerOn);
            }
            if (lab == null || lab.def.defName != "SCP_FoundationLabModule")
            {
                if (exosuit) __result = false;
                return;
            }

            CompAffectedByFacilities facilities = lab.TryGetComp<CompAffectedByFacilities>();
            __result = facilities != null && facilities.LinkedFacilitiesListForReading.Any(fusor =>
                fusor != null && fusor.Spawned && fusor.def.defName == "SCP_FoundationFusionChamber"
                && fusor.TryGetComp<CompPowerTrader>() != null
                && fusor.TryGetComp<CompPowerTrader>().PowerOn);
        }
    }
}
