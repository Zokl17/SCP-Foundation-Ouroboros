using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    // A static wall mesh with 47 adjacency variants; no ticking or gameplay hooks.
    // Vanilla CornerFiller only paints a flat patch at concave corners. These variants
    // include the two shaded faces and wire in that corner instead.
    public sealed class Graphic_FoundationWall : Graphic_Single
    {
        private Graphic_Linked links;
        private Graphic iconGraphic;
        private Vector2[][] tileUvs;
        private static readonly IntVec3[] Neighbors = {
            new IntVec3(0,0,1), new IntVec3(1,0,0), new IntVec3(0,0,-1), new IntVec3(-1,0,0),
            new IntVec3(1,0,1), new IntVec3(1,0,-1), new IntVec3(-1,0,-1), new IntVec3(-1,0,1)
        };

        public override void Init(GraphicRequest req)
        {
            base.Init(req);
            links = new Graphic_Linked(this);
            iconGraphic = GraphicDatabase.Get<Graphic_Single>(req.path + "_Icon", req.shader,
                req.drawSize, req.color, req.colorTwo);
            tileUvs = new Vector2[WallAtlasLayout.Masks.Length][];
            float extent = WallAtlasLayout.Columns * WallAtlasLayout.Pitch;
            for (int i = 0; i < tileUvs.Length; i++)
            {
                float left = (i % WallAtlasLayout.Columns * WallAtlasLayout.Pitch + WallAtlasLayout.Gutter) / extent;
                float top = 1f - (i / WallAtlasLayout.Columns * WallAtlasLayout.Pitch + WallAtlasLayout.Gutter) / extent;
                float size = WallAtlasLayout.Active / extent;
                // Printer_Plane's order: bottom-left, top-left, top-right, bottom-right.
                tileUvs[i] = new[] { new Vector2(left,top-size), new Vector2(left,top),
                    new Vector2(left+size,top), new Vector2(left+size,top-size) };
            }
        }

        public override Material MatSingle { get { return iconGraphic == null ? base.MatSingle : iconGraphic.MatSingle; } }
        public override Material MatAt(Rot4 rot, Thing thing = null) { return MatSingle; }
        public override Graphic GetColoredVersion(Shader shader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_FoundationWall>(path, shader, drawSize,
                newColor, newColorTwo, data, null);
        }
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            int mask = 0;
            for (int i = 0; i < Neighbors.Length; i++)
                if (links.ShouldLinkWith(thing.Position + Neighbors[i], thing)) mask |= 1 << i;
            Printer_Plane.PrintPlane(layer, thing.DrawPos, drawSize, base.MatSingle,
                extraRotation, false, tileUvs[WallAtlasLayout.Index(mask)]);
        }
    }
}
