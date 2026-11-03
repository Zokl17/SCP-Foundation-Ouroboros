using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Debugging
{
    // Placement-only harness. Does not register anomalies or add gameplay systems.
    public static class ArtKitV1DebugActions
    {
        private const int GalleryWidth = 20, GalleryHeight = 16;
        private static IEnumerable<ThingDef> Samples
        {
            get { return DefDatabase<ThingDef>.AllDefs.Where(def => def.defName.StartsWith("SCPV1_")).OrderBy(def => def.defName); }
        }
        private static TerrainDef Floor { get { return DefDatabase<TerrainDef>.GetNamed("SCPV1_ContainmentFloor"); } }

        public static List<DebugActionNode> PlaceMenu()
        {
            var nodes = new List<DebugActionNode>();
            foreach (ThingDef sample in Samples)
            {
                ThingDef def = sample;
                var entry = new DebugActionNode(def.LabelCap + " [" + def.size.x + "x" + def.size.z + "]");
                for (int i = 0; i < (def.rotatable ? 4 : 1); i++)
                {
                    Rot4 rotation = new Rot4(i);
                    entry.AddChild(new DebugActionNode(new[] { "North", "East", "South", "West" }[i], action: () => BeginPlacement(def, rotation)));
                }
                nodes.Add(entry);
            }
            nodes.Add(new DebugActionNode(Floor.LabelCap, action: () =>
                DebugTools.curTool = new DebugTool(Floor.LabelCap, PlaceFloor, () => DrawFootprint(null, Rot4.North))));
            return nodes;
        }

        private static void BeginPlacement(ThingDef def, Rot4 rotation)
        {
            DebugTools.curTool = new DebugTool(def.LabelCap + " " + rotation,
                () =>
                {
                    if (!Prefs.DevMode || Find.CurrentMap == null) return;
                    if (!CanPlace(def, UI.MouseCell(), rotation, Find.CurrentMap)) { Reject(); return; }
                    if (!ClearArea(PlacementCells(def, UI.MouseCell(), rotation, Find.CurrentMap), Find.CurrentMap)) { Reject(); return; }
                    Spawn(def, UI.MouseCell(), rotation, Find.CurrentMap);
                }, () => DrawFootprint(def, rotation));
        }

        private static List<IntVec3> PlacementCells(ThingDef def, IntVec3 cell, Rot4 rotation, Map map)
        {
            var cells = GenAdj.CellsOccupiedBy(cell, rotation, def.size).ToList();
            if (def.hasInteractionCell) cells.Add(ThingUtility.InteractionCellWhenAt(def, cell, rotation, map));
            return cells;
        }

        private static bool CanPlace(ThingDef def, IntVec3 cell, Rot4 rotation, Map map)
        {
            return PlacementCells(def, cell, rotation, map).All(part => part.InBounds(map));
        }

        private static bool ClearArea(List<IntVec3> cells, Map map)
        {
            if (cells.Any(cell => !cell.InBounds(map))) return false;
            var area = new HashSet<IntVec3>(cells);
            var obstacles = cells.SelectMany(cell => cell.GetThingList(map)).Distinct().ToList();
            var moves = new Dictionary<Pawn, IntVec3>();
            // Preflight safe destinations before changing anything. Never wipe a pawn.
            foreach (Pawn pawn in obstacles.OfType<Pawn>())
            {
                IntVec3 destination = GenRadial.RadialCellsAround(pawn.Position, 40f, true)
                    .Where(cell => cell.InBounds(map) && !area.Contains(cell) && !moves.ContainsValue(cell)
                        && !cell.Fogged(map) && cell.Standable(map) && cell.GetEdifice(map) == null
                        && cell.GetFirstPawn(map) == null).DefaultIfEmpty(IntVec3.Invalid).First();
                if (!destination.IsValid) return false;
                moves.Add(pawn, destination);
            }
            foreach (KeyValuePair<Pawn, IntVec3> move in moves)
            {
                move.Key.Position = move.Value;
                move.Key.Notify_Teleported();
            }
            foreach (IntVec3 cell in cells) map.roofGrid.SetRoof(cell, null);
            foreach (Thing thing in obstacles)
                if (!(thing is Pawn) && thing.Spawned) thing.Destroy(DestroyMode.Vanish);
            foreach (IntVec3 cell in cells)
            {
                map.terrainGrid.SetTerrain(cell, Floor);
                map.fogGrid.Unfog(cell);
            }
            return true;
        }

        private static void Spawn(ThingDef def, IntVec3 cell, Rot4 rotation, Map map)
        {
            Thing thing = ThingMaker.MakeThing(def);
            thing.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(thing, cell, map, rotation);
        }

        private static bool CanFloor(IntVec3 cell, Map map)
        {
            return cell.InBounds(map);
        }
        private static void PlaceFloor()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            if (!ClearArea(new List<IntVec3> { UI.MouseCell() }, Find.CurrentMap)) Reject();
        }

        private static void DrawFootprint(ThingDef def, Rot4 rotation)
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            var cells = def == null ? new List<IntVec3> { cell } : GenAdj.CellsOccupiedBy(cell, rotation, def.size).ToList();
            if (cells.Any(part => !part.InBounds(map))) return;
            bool valid = def == null ? CanFloor(cell, map) : CanPlace(def, cell, rotation, map);
            GenDraw.DrawFieldEdges(cells, valid ? Color.green : Color.red);
            if (def == null) return;
            GhostDrawer.DrawGhostThing(cell, rotation, def, null, new Color(1f, 1f, 1f, 0.5f), AltitudeLayer.MetaOverlays);
            if (def.hasInteractionCell) GenDraw.DrawInteractionCells(def, cell, rotation);
        }

        public static void GalleryTool()
        {
            DebugTools.curTool = new DebugTool("Art v1 gallery 20x16", Gallery, () =>
            {
                if (!Prefs.DevMode || Find.CurrentMap == null) return;
                IntVec3 cell = UI.MouseCell();
                var cells = new CellRect(cell.x, cell.z, GalleryWidth, GalleryHeight).Cells.ToList();
                if (cells.Any(part => !part.InBounds(Find.CurrentMap))) return;
                GenDraw.DrawFieldEdges(cells, cells.All(part => CanFloor(part, Find.CurrentMap)) ? Color.green : Color.red);
            });
        }

        private static void Gallery()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            var cells = new CellRect(origin.x, origin.z, GalleryWidth, GalleryHeight).Cells.ToList();
            // Preflight the WHOLE patch before any terrain or thing is changed.
            if (cells.Any(cell => !CanFloor(cell, map))) { Reject(); return; }
            var samples = Samples.Select(def => new KeyValuePair<ThingDef, Rot4>(def, Rot4.North)).ToList();
            foreach (string name in new[] { "SCPV1_Camera", "SCPV1_MonitoringConsole" })
                for (int i = 1; i < 4; i++) samples.Add(new KeyValuePair<ThingDef, Rot4>(DefDatabase<ThingDef>.GetNamed(name), new Rot4(i)));
            if (samples.Count > 24) { Reject(); return; }
            if (!ClearArea(cells, map)) { Reject(); return; }
            for (int i = 0; i < samples.Count; i++)
                Spawn(samples[i].Key, origin + new IntVec3(2 + i % 6 * 3, 0, 2 + i / 6 * 3), samples[i].Value, map);
            // Contiguous wall/door strip for one-tile alignment and open/closed comparison.
            string[] strip = { "SCPV1_WallA", "SCPV1_DoorA_Closed", "SCPV1_WallA", "SCPV1_DoorA_Open", "SCPV1_WallA" };
            for (int i = 0; i < strip.Length; i++) Spawn(DefDatabase<ThingDef>.GetNamed(strip[i]), origin + new IntVec3(2 + i, 0, 14), Rot4.North, map);
            Messages.Message("ARTKIT_GalleryPlaced".Translate(), MessageTypeDefOf.TaskCompletion, false);
        }

        private static void Reject() { Messages.Message("ARTKIT_PlacementDenied".Translate(), MessageTypeDefOf.RejectInput, false); }
    }
}
