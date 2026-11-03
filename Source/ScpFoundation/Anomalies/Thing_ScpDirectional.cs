using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    public class Thing_ScpDirectional : ThingWithComps
    {
        private readonly Graphic[] facingGraphics = new Graphic[4];

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            int facing = Rotation.AsInt;
            Graphic graphic = facingGraphics[facing];
            if (graphic == null)
            {
                string path = def.graphicData.texPath;
                Graphic multi = def.graphicData.Graphic;
                Texture texture = multi.MatAt(Rotation, this).mainTexture;
                float length = Mathf.Max(def.graphicData.drawSize.x, def.graphicData.drawSize.y);
                float scale = length / Mathf.Max(texture.width, texture.height);
                Vector2 size = new Vector2(texture.width * scale, texture.height * scale);
                graphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout, size, Color.white);
                facingGraphics[facing] = graphic;
            }
            graphic.Draw(drawLoc, Rotation, this);
            Comps_PostDraw();
        }
    }
}
