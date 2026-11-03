using ScpFoundation.Interactions;
using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class Building_Scp682AcidChamber : Building
    {
        private Graphic occupiedGraphic;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            CompScpPost post = GetComp<CompScpPost>();
            if (post == null || post.BoundObject == null || !CompScpPost.IsObjectAtPost(post.BoundObject))
            {
                base.DrawAt(drawLoc, flip);
                return;
            }
            if (occupiedGraphic == null)
                occupiedGraphic = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Building/Scp682AcidChamberOccupied", ShaderDatabase.Cutout,
                    def.graphicData.drawSize, Color.white);
            occupiedGraphic.Draw(drawLoc, Rotation, this);
            Comps_PostDraw();
        }
    }
}
