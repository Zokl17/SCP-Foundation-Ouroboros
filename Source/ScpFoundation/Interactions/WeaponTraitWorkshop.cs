using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Interactions
{
    // Ordinary guns need the Odyssey component to persist traits, but must not
    // roll a random unique trait (or regenerate their quality) when created.
    public sealed class CompWorkshopWeaponTraits : CompUniqueWeapon
    {
        public override void PostPostMake() { }
    }

    // Odyssey requires positive commonality even for workshop-only traits.
    // Its random roll asks CanAddTrait; manual installation uses AddTrait.
    [HarmonyPatch(typeof(CompUniqueWeapon), "CanAddTrait")]
    internal static class WorkshopOnlyWeaponTraitRollPatch
    {
        private static void Postfix(WeaponTraitDef trait, ref bool __result)
        {
            if (trait != null && trait.defName.StartsWith("SCP_", StringComparison.Ordinal))
                __result = false;
        }
    }

    public sealed class RecipeWorker_InstallWeaponTrait : RecipeWorker
    {
        internal const string Prefix = "SCP_InstallWeaponTrait_";

        internal static WeaponTraitDef TraitFor(RecipeDef def)
        {
            return def != null && def.defName.StartsWith(Prefix, StringComparison.Ordinal)
                ? DefDatabase<WeaponTraitDef>.GetNamedSilentFail(def.defName.Substring(Prefix.Length)) : null;
        }

        private static List<WeaponCategoryDef> CategoriesFor(ThingDef def, CompUniqueWeapon comp)
        {
            if (!(comp is CompWorkshopWeaponTraits)) return comp.Props.weaponCategories;

            string analogue = null;
            switch (def.defName)
            {
                case "SCP_Nu7HKCarbine":
                case "SCP_ChaosAKGrenadier": analogue = "Gun_AssaultRifle_Unique"; break;
                case "SCP_Nu7LightMachineGun":
                case "SCP_ChaosLightMachineGun": analogue = "Gun_LMG_Unique"; break;
                case "SCP_FoundationSpasTwelve": analogue = "Gun_ChainShotgun_Unique"; break;
                case "Bow_Short":
                case "Bow_Recurve": analogue = "Bow_Great_Unique"; break;
                default: analogue = def.defName + "_Unique"; break;
            }

            ThingDef uniqueDef = DefDatabase<ThingDef>.GetNamedSilentFail(analogue);
            CompProperties_UniqueWeapon props = uniqueDef == null ? null : uniqueDef.GetCompProperties<CompProperties_UniqueWeapon>();
            return props == null ? null : props.weaponCategories;
        }

        internal static bool CategoryMatches(Thing thing, WeaponTraitDef trait)
        {
            if (thing == null || trait == null) return false;
            CompUniqueWeapon comp = thing.TryGetComp<CompUniqueWeapon>();
            List<WeaponCategoryDef> categories = comp == null ? null : CategoriesFor(thing.def, comp);
            return categories != null && categories.Contains(trait.weaponCategory);
        }

        internal static bool CanInstall(Thing thing, WeaponTraitDef trait)
        {
            if (thing == null || trait == null || thing.def == null) return false;
            CompUniqueWeapon comp = thing.TryGetComp<CompUniqueWeapon>();
            if (comp == null || comp.TraitsListForReading == null || comp.TraitsListForReading.Count >= 5) return false;
            if (!CategoryMatches(thing, trait)) return false;

            // The Chaos AK already has a fixed underbarrel launcher. Odyssey's
            // equippable component has only one ability slot.
            if (trait.abilityProps != null && thing.def.defName == "SCP_ChaosAKGrenadier") return false;
            foreach (WeaponTraitDef existing in comp.TraitsListForReading)
                if (existing == trait || existing.Overlaps(trait)) return false;
            return true;
        }

        public override void ConsumeIngredient(Thing ingredient, RecipeDef recipeDef, Map map)
        {
            WeaponTraitDef trait = TraitFor(recipeDef);
            CompUniqueWeapon comp = ingredient == null ? null : ingredient.TryGetComp<CompUniqueWeapon>();
            if (comp == null)
            {
                base.ConsumeIngredient(ingredient, recipeDef, map);
                return;
            }

            if (!CanInstall(ingredient, trait)) return;
            comp.AddTrait(trait);
            comp.Setup(false);
            ingredient.Notify_ColorChanged();
            // Toils_Recipe places ingredients at the bench. The gun is the
            // finished item, so keep the very same instance and all its comps.
            if (!ingredient.Spawned && map != null)
                GenPlace.TryPlaceThing(ingredient, ingredient.PositionHeld, map, ThingPlaceMode.Near);
        }
    }

    // Odyssey's unique guns have alternate silhouettes. Keep the choice on the
    // individual weapon, rather than changing a shared ThingDef graphic.
    [HarmonyPatch(typeof(Thing), "get_Graphic")]
    internal static class WorkshopWeaponGraphicPatch
    {
        private static void Postfix(Thing __instance, ref Graphic __result)
        {
            if (__instance == null || __instance.def == null || !__instance.def.IsWeapon ||
                !__instance.def.IsRangedWeapon) return;
            CompUniqueWeapon comp = __instance.TryGetComp<CompUniqueWeapon>();
            if (comp == null || comp.TraitsListForReading == null) return;
            bool hasAttachment = false;
            foreach (WeaponTraitDef trait in comp.TraitsListForReading)
                if (trait.weaponCategory != null && trait.weaponCategory.defName == "Attachable")
                { hasAttachment = true; break; }
            if (!hasAttachment) return;

            string defName = __instance.def.defName;
            ThingDef unique = DefDatabase<ThingDef>.GetNamedSilentFail(
                defName.EndsWith("_Unique", StringComparison.Ordinal) ? defName : defName + "_Unique");
            Graphic_Random variants = unique == null || unique.graphicData == null
                ? null : unique.graphicData.GraphicColoredFor(__instance) as Graphic_Random;
            if (variants != null && variants.SubGraphicsCount > 0)
                __result = variants.SubGraphicAtIndex(variants.SubGraphicsCount - 1);
        }
    }

    // AddTrait does not update the color stored when a unique gun was created.
    // Use the installed trait's Odyssey color for both existing and new guns.
    [HarmonyPatch(typeof(CompUniqueWeapon), "ForceColor")]
    internal static class WorkshopWeaponColorPatch
    {
        private static void Postfix(CompUniqueWeapon __instance, ref Color? __result)
        {
            if (__instance.TraitsListForReading == null) return;
            string attachmentColor = null;
            foreach (WeaponTraitDef trait in __instance.TraitsListForReading)
            {
                if (trait.forcedColor != null)
                {
                    __result = trait.forcedColor.color;
                    return;
                }
                if (trait.defName == "EMPLauncher") attachmentColor = "UniqueWeapon_EMP";
                else if (trait.defName == "IncendiaryLauncher" || trait.defName == "BioferriteBurner")
                    attachmentColor = "UniqueWeapon_Fire";
            }
            ColorDef color = attachmentColor == null ? null : DefDatabase<ColorDef>.GetNamedSilentFail(attachmentColor);
            if (color != null) __result = color.color;
        }
    }

    [HarmonyPatch(typeof(Bill), "IsFixedOrAllowedIngredient", new Type[] { typeof(Thing) })]
    internal static class WeaponTraitBillIngredientPatch
    {
        private static void Postfix(Bill __instance, Thing thing, ref bool __result)
        {
            if (!__result || !(__instance is Bill_Production)) return;
            WeaponTraitDef trait = RecipeWorker_InstallWeaponTrait.TraitFor(__instance.recipe);
            if (trait != null && thing.def.IsWeapon && thing.def.IsRangedWeapon)
                __result = RecipeWorker_InstallWeaponTrait.CanInstall(thing, trait);
        }
    }
}
