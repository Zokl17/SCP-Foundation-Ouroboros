using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Interactions
{
    public static class ScpWallCamera
    {
        // Vanilla attachment rotation points TO its supporting wall. The lens faces away.
        public static IntVec3 Facing(Thing camera) { return camera.Rotation.FacingCell * -1; }
        public static bool Mounted(Thing camera)
        {
            if (!camera.Spawned) return false;
            IntVec3 cell = camera.Position + camera.Rotation.FacingCell;
            if (!cell.InBounds(camera.Map)) return false;
            Building support = cell.GetEdifice(camera.Map);
            return support != null && support.def.building.supportsWallAttachments && support.def.Fillage == FillCategory.Full;
        }
    }
    public sealed class CompProperties_WallCamera : CompProperties
    {
        public CompProperties_WallCamera() { compClass = typeof(CompWallCamera); }
    }
    public sealed class CompWallCamera : ThingComp
    {
        public override string CompInspectStringExtra()
        {
            return !parent.Spawned ? null : (!ScpWallCamera.Mounted(parent) ? "SCP_CameraNeedsWall"
                : !ScpSurveillance.Powered(parent) ? "SCP_CameraNoPower" : "SCP_CameraReady").Translate().ToString();
        }
        public override void PostDrawExtraSelectionOverlays()
        {
            if (!parent.Spawned || !ScpWallCamera.Mounted(parent)) return;
            var cells = new List<IntVec3>();
            Room room = parent.Position.GetRoom(parent.Map);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, SurveillancePolicy.Range, true))
            {
                IntVec3 delta = cell - parent.Position;
                if (cell.InBounds(parent.Map) && SurveillancePolicy.CameraCovers(delta.x, delta.z, cell.GetRoom(parent.Map) == room)
                    && ScpSurveillance.Sight(parent.Position, cell, parent.Map)) cells.Add(cell);
            }
            GenDraw.DrawFieldEdges(cells);
        }
    }
}
