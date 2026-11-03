using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Building_FoundationShutter : Building_MultiTileDoor
    {
        private Graphic openFrameGraphic;
        private Graphic leftLeafGraphic;
        private Graphic rightLeafGraphic;
        private Graphic warningGraphic;

        private CompFoundationShutter Controller { get { return GetComp<CompFoundationShutter>(); } }

        public override bool PawnCanOpen(Pawn pawn)
        {
            return !CompRoomDecontaminator.DoorLocked(this)
                && Controller != null && Controller.AllowsPawnOpening
                && base.PawnCanOpen(pawn);
        }

        public void SetManualOpen(bool open)
        {
            holdOpenInt = open;
            if (open) DoorOpen(120);
            else DoorTryClose();
        }

        public void ReleaseAutomaticControl()
        {
            holdOpenInt = false;
        }

        public void RequestLockdownClose()
        {
            holdOpenInt = false;
            DoorTryClose();
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
                ? new Vector3(0f, 0f, 0.93f * OpenPct)
                : Rotation.AsQuat * new Vector3(0.93f * OpenPct, 0f, 0f);
            leftLeafGraphic.Draw(Rotation.IsHorizontal ? leafLoc + travel : leafLoc - travel, Rotation, this);
            rightLeafGraphic.Draw(Rotation.IsHorizontal ? leafLoc - travel : leafLoc + travel, Rotation, this);

            Vector3 frameLoc = artLoc;
            frameLoc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            openFrameGraphic.Draw(frameLoc, Rotation, this);
            if (OpenPct > 0.001f && OpenPct < 0.999f && Find.TickManager.TicksGame % 30 < 15)
                warningGraphic.Draw(frameLoc, Rotation, this);
            Comps_PostDraw();
        }

        private void EnsureGraphics()
        {
            if (openFrameGraphic != null) return;
            Vector2 size = new Vector2(3f, 1.5f);
            openFrameGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Building/Shutter/ScpBlastBulkhead_FrameOpen", ShaderDatabase.Cutout, size, Color.white);
            leftLeafGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Building/Shutter/ScpBlastBulkhead_LeafLeft", ShaderDatabase.Cutout, size, Color.white);
            rightLeafGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Building/Shutter/ScpBlastBulkhead_LeafRight", ShaderDatabase.Cutout, size, Color.white);
            warningGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Building/Shutter/ScpBlastBulkhead_Warning", ShaderDatabase.MoteGlow, size, Color.white);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            string holdOpenLabel = "CommandHoldOpen".Translate();
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                Command_Toggle toggle = gizmo as Command_Toggle;
                if (toggle != null && toggle.defaultLabel == holdOpenLabel) continue;
                yield return gizmo;
            }
        }
    }

    public sealed class CompProperties_FoundationShutter : CompProperties
    {
        public CompProperties_FoundationShutter() { compClass = typeof(CompFoundationShutter); }
    }

    public sealed class CompFoundationShutter : ThingComp
    {
        private bool automatic = true;
        private bool manualOpen;
        private bool breachActive;

        private Building_FoundationShutter Shutter { get { return (Building_FoundationShutter)parent; } }
        private bool Powered { get { return ScpSurveillance.Powered(parent); } }
        public bool AllowsPawnOpening { get { return !ShutterPolicy.Locked(automatic, Powered, breachActive, manualOpen); } }

        private bool BreachOnMap()
        {
            return parent.Spawned && parent.Map.listerThings.AllThings.Any(thing =>
            {
                CompScp scp = thing.TryGetComp<CompScp>();
                return scp != null && scp.IsBreached;
            });
        }

        private void Refresh()
        {
            breachActive = BreachOnMap();
            if (!Powered)
            {
                manualOpen = false;
                Shutter.RequestLockdownClose();
                return;
            }
            if (automatic)
            {
                Shutter.ReleaseAutomaticControl();
                if (breachActive) Shutter.RequestLockdownClose();
            }
            else Shutter.SetManualOpen(manualOpen);
        }

        public override void CompTick()
        {
            if (parent.Spawned && parent.IsHashIntervalTick(15)) Refresh();
        }

        private void ToggleMode()
        {
            automatic = !automatic;
            if (!automatic) manualOpen = Shutter.Open;
            Refresh();
        }

        private void ToggleManualState()
        {
            manualOpen = !manualOpen;
            Refresh();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref automatic, "scpShutterAutomatic", true);
            Scribe_Values.Look(ref manualOpen, "scpShutterManualOpen", false);
            Scribe_Values.Look(ref breachActive, "scpShutterBreachActive", false);
        }

        public override string CompInspectStringExtra()
        {
            if (!Powered) return "SCP_ShutterNoPower".Translate();
            if (automatic && breachActive) return "SCP_ShutterAutomaticLockdown".Translate();
            if (automatic) return "SCP_ShutterAutomaticReady".Translate();
            return (manualOpen ? "SCP_ShutterManualOpenState" : "SCP_ShutterManualClosedState").Translate();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action
            {
                defaultLabel = (automatic ? "SCP_ShutterUseManual" : "SCP_ShutterUseAutomatic").Translate(),
                defaultDesc = "SCP_ShutterModeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Building/Shutter/ScpBlastBulkhead_Closed"),
                action = ToggleMode
            };
            if (automatic) yield break;
            Command_Action state = new Command_Action
            {
                defaultLabel = (manualOpen ? "SCP_ShutterClose" : "SCP_ShutterOpen").Translate(),
                defaultDesc = (manualOpen ? "SCP_ShutterCloseDesc" : "SCP_ShutterOpenDesc").Translate(),
                icon = ContentFinder<Texture2D>.Get(manualOpen
                    ? "Things/Building/Shutter/ScpBlastBulkhead_Closed" : "Things/Building/Shutter/ScpBlastBulkhead_FrameOpen"),
                action = ToggleManualState
            };
            if (!manualOpen && !Powered) state.Disable("SCP_ShutterNeedsPower".Translate());
            yield return state;
        }
    }
}
