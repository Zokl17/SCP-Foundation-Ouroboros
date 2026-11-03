using RimWorld;
using ScpFoundation.Interactions;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    // Vanilla door simulation; only the single sliding leaf differs from its mirrored movers.
    public sealed class Building_FoundationDoor : Building_Door
    {
        private Graphic openFrameGraphic;
        private Graphic leafGraphic;

        public override bool PawnCanOpen(Pawn pawn)
        {
            return !CompRoomDecontaminator.DoorLocked(this)
                && FoundationDoorAccessPolicy.Allows(base.PawnCanOpen(pawn), this, pawn);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DoorPreDraw();
            if (openFrameGraphic == null)
            {
                Vector2 size = new Vector2(1.8f, 1.35f);
                bool hermetic = def.defName == "SCPV1_Airlock_Closed";
                string framePath = hermetic ? "Things/Building/Door/ScpHermeticDoor_FrameOpenRaised" : "Things/Building/Door/ScpReinforcedDoorA_FrameOpenRaised";
                string leafPath = hermetic ? "Things/Building/Door/ScpHermeticDoor_LeafRaised" : "Things/Building/Door/ScpReinforcedDoorA_LeafRaised";
                openFrameGraphic = GraphicDatabase.Get<Graphic_Multi>(framePath, ShaderDatabase.Cutout, size, Color.white);
                leafGraphic = GraphicDatabase.Get<Graphic_Multi>(leafPath, ShaderDatabase.Cutout, size, Color.white);
            }

            Vector3 artLoc = drawLoc + new Vector3(0f, 0f, 0.16f);
            Graphic_Shadow shadow = Graphic.ShadowGraphic;
            if (shadow != null) shadow.Draw(artLoc, Rotation, this);
            if (CanDrawMovers)
            {
                Vector3 leafLoc = artLoc;
                leafLoc.y = AltitudeLayer.DoorMoveable.AltitudeFor();
                leafLoc += Rotation.AsQuat * new Vector3(-0.82f * OpenPct, 0f, 0f);
                leafGraphic.Draw(leafLoc, Rotation, this);
            }

            Vector3 frameLoc = artLoc;
            frameLoc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            openFrameGraphic.Draw(frameLoc, Rotation, this);
            Comps_PostDraw();
        }
    }

    // Multi-cell armored doors keep the vanilla simulation while drawing their
    // oversized frame above adjacent walls and their leaves at the door layer.
    public sealed class Building_FoundationMultiTileDoor : Building_MultiTileDoor
    {
        private Graphic openFrameGraphic;
        private Graphic leftLeafGraphic;
        private Graphic rightLeafGraphic;

        public override bool PawnCanOpen(Pawn pawn)
        {
            return !CompRoomDecontaminator.DoorLocked(this)
                && FoundationDoorAccessPolicy.Allows(base.PawnCanOpen(pawn), this, pawn);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DoorPreDraw();
            EnsureGraphics();
            Vector3 artLoc = drawLoc + new Vector3(0f, 0f, 0.18f);
            Graphic_Shadow shadow = Graphic.ShadowGraphic;
            if (shadow != null) shadow.Draw(artLoc, Rotation, this);

            Vector3 leafLoc = artLoc;
            leafLoc.y = AltitudeLayer.DoorMoveable.AltitudeFor();
            Vector3 travel = Rotation.IsHorizontal
                ? new Vector3(0f, 0f, 0.9f * OpenPct)
                : Rotation.AsQuat * new Vector3(0.9f * OpenPct, 0f, 0f);
            // Side art is split into upper and lower leaves; each must move away from the centre.
            leftLeafGraphic.Draw(Rotation.IsHorizontal ? leafLoc + travel : leafLoc - travel, Rotation, this);
            rightLeafGraphic.Draw(Rotation.IsHorizontal ? leafLoc - travel : leafLoc + travel, Rotation, this);

            Vector3 frameLoc = artLoc;
            frameLoc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            openFrameGraphic.Draw(frameLoc, Rotation, this);
            Comps_PostDraw();
        }

        private void EnsureGraphics()
        {
            if (openFrameGraphic != null) return;
            Vector2 size = new Vector2(3f, 1.5f);
            openFrameGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Building/Door/ScpArmoredDoorB_FrameOpenRaised", ShaderDatabase.Cutout, size, Color.white);
            leftLeafGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Building/Door/ScpArmoredDoorB_LeafLeft", ShaderDatabase.Cutout, size, Color.white);
            rightLeafGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Building/Door/ScpArmoredDoorB_LeafRight", ShaderDatabase.Cutout, size, Color.white);
        }
    }
}
