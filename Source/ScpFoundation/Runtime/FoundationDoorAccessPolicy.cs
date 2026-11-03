using Verse;

namespace ScpFoundation.Runtime
{
    public static class FoundationDoorAccessPolicy
    {
        public static bool Allows(bool vanillaAllows, bool restricted, bool emergencyAccess, bool listed)
        {
            return vanillaAllows && (!restricted || emergencyAccess || listed);
        }

        public static bool Allows(bool vanillaAllows, ThingWithComps door, Pawn pawn)
        {
            CompFoundationDoorAccess access = door.TryGetComp<CompFoundationDoorAccess>();
            return access == null
                ? vanillaAllows
                : Allows(vanillaAllows, access.Restricted, access.EmergencyAccess, access.IsAllowed(pawn));
        }
    }
}
