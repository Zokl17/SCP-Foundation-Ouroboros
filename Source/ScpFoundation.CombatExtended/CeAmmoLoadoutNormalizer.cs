using System;
using System.Collections.Generic;
using CombatExtended;
using Verse;

namespace ScpFoundation.CombatExtendedCompat
{
    public static class CeAmmoLoadoutNormalizer
    {
        private const int SpareMagazineCount = 3;

        public static void Normalize(Pawn pawn)
        {
            ThingWithComps weapon = pawn == null || pawn.equipment == null ? null : pawn.equipment.Primary;
            if (weapon != null && weapon.def.defName == "SCP_127Gun") return;
            CompAmmoUser ammoUser = weapon == null ? null : weapon.TryGetComp<CompAmmoUser>();
            if (ammoUser == null || ammoUser.Props == null || ammoUser.Props.ammoSet == null) return;

            HashSet<AmmoDef> compatibleAmmo = AmmoFor(ammoUser.Props.ammoSet);
            CompUnderBarrel underBarrel = weapon.TryGetComp<CompUnderBarrel>();
            if (underBarrel != null && underBarrel.CompPropsAmmo != null && underBarrel.CompPropsAmmo.ammoSet != null)
                compatibleAmmo.UnionWith(AmmoFor(underBarrel.CompPropsAmmo.ammoSet));

            AmmoDef selectedAmmo = ammoUser.CurrentAmmo;
            if (selectedAmmo == null || !compatibleAmmo.Contains(selectedAmmo))
                selectedAmmo = ammoUser.SelectedAmmo;
            if (selectedAmmo == null || !compatibleAmmo.Contains(selectedAmmo))
            {
                foreach (AmmoLink link in ammoUser.Props.ammoSet.ammoTypes)
                {
                    if (link != null && link.ammo != null)
                    {
                        selectedAmmo = link.ammo;
                        break;
                    }
                }
            }
            if (selectedAmmo == null) return;

            ThingOwner inventory = pawn.inventory == null ? null : pawn.inventory.innerContainer;
            if (inventory != null)
            {
                for (int index = inventory.Count - 1; index >= 0; index--)
                {
                    Thing thing = inventory[index];
                    AmmoDef ammo = thing.def as AmmoDef;
                    if (ammo == null || compatibleAmmo.Contains(ammo)) continue;
                    inventory.Remove(thing);
                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            ammoUser.SelectedAmmo = selectedAmmo;
            ammoUser.ResetAmmoCount(selectedAmmo);
            AddSpareAmmo(inventory, selectedAmmo, ammoUser.MagSize * SpareMagazineCount);
        }

        private static HashSet<AmmoDef> AmmoFor(AmmoSetDef ammoSet)
        {
            var result = new HashSet<AmmoDef>();
            if (ammoSet == null || ammoSet.ammoTypes == null) return result;
            foreach (AmmoLink link in ammoSet.ammoTypes)
                if (link != null && link.ammo != null) result.Add(link.ammo);
            return result;
        }

        private static void AddSpareAmmo(ThingOwner inventory, AmmoDef ammoDef, int desiredCount)
        {
            if (inventory == null || ammoDef == null || desiredCount <= 0) return;
            int currentCount = 0;
            foreach (Thing thing in inventory)
                if (thing.def == ammoDef) currentCount += thing.stackCount;

            int missing = desiredCount - currentCount;
            while (missing > 0)
            {
                Thing ammo = ThingMaker.MakeThing(ammoDef);
                ammo.stackCount = Math.Min(missing, ammoDef.stackLimit);
                int added = ammo.stackCount;
                if (!inventory.TryAdd(ammo))
                {
                    ammo.Destroy(DestroyMode.Vanish);
                    return;
                }
                missing -= added;
            }
        }
    }
}
