using RimWorld;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Building_FoundationWall : Building
    {
        // linkType=None prevents vanilla from wrapping our 47-state graphic in its
        // 16-state renderer. It also skips vanilla's automatic LinkGrid notification,
        // so explicitly register/unregister this ordinary wall's visual connections.
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RefreshLinks(map);
        }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map oldMap = Map;
            base.DeSpawn(mode);
            // Verse.Thing.DeSpawn preserves Position, but clears Map and unregisters
            // the thing first, so the link grid now sees the remaining neighbors.
            if (oldMap != null) RefreshLinks(oldMap);
        }
        private void RefreshLinks(Map map)
        {
            map.linkGrid.Notify_LinkerCreatedOrDestroyed(this);
            map.mapDrawer.MapMeshDirty(Position,
                (ulong)MapMeshFlagDefOf.Things | (ulong)MapMeshFlagDefOf.Buildings, true, true);
        }
    }
}
