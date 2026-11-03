using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ScpFoundation.Runtime
{
    // Foundation prototype: manual movement and self-contained weapon tests.
    [StaticConstructorOnStartup]
    public sealed class Pawn_FoundationChimera : Pawn
    {
        private const int FrameCount = 6;
        private const int FrameTicks = 13;
        private const int SalvoSize = 8;
        private const int LaunchInterval = 8;
        private const int ReloadTicks = 600;
        private const int WarmupTicks = 60;
        private const float MinimumRange = 12f;
        private const float MaximumRange = 130f;
        private const float TurretDegreesPerTick = 4f;
        private static readonly Material[] Materials = new Material[FrameCount + 1];
        private static readonly Material[,] UpperMaterials = new Material[8, 3];
        private static readonly Material[] CannonFlashMaterials = new Material[10];
        private static readonly string[] UpperTextures = { "Turret", "Lift", "Cassette" };
        private int walkTicks;
        private int launcherTicks;
        private bool facingEast;
        private float turretHeading = 225f;
        private int rocketsRemaining;
        private int nextRocketTick;
        private int nextSalvoTick;
        private IntVec3 salvoTarget = IntVec3.Invalid;
        private ChimeraGun pendingGun;
        private LocalTargetInfo gunTarget = LocalTargetInfo.Invalid;
        private int gunWarmupEnd;
        private int nextCannonTick;
        private int nextRailgunTick;
        private int lastCannonShotTick = -100;
        private int escortEndTick;
        private int retreatEndTick;
        private IntVec3 retreatCell = IntVec3.Invalid;
        private Vector3 cannonFlashDirection;
        private Vector3 cannonFlashOrigin;

        private bool Firing { get { return rocketsRemaining > 0 || pendingGun != ChimeraGun.None; } }

        public override string Label { get { return def.label; } }
        public override string LabelNoCount { get { return def.label; } }
        public override string LabelShort { get { return def.label; } }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref walkTicks, "chimeraWalkTicks", 0);
            Scribe_Values.Look(ref launcherTicks, "chimeraLauncherTicks", 0);
            Scribe_Values.Look(ref facingEast, "chimeraFacingEast", false);
            Scribe_Values.Look(ref turretHeading, "chimeraTurretHeading", 225f);
            if (Scribe.mode == LoadSaveMode.LoadingVars && Scribe.loader.curXmlParent["chimeraTurretHeading"] == null)
                turretHeading = facingEast ? 135f : 225f;
            Scribe_Values.Look(ref rocketsRemaining, "chimeraRocketsRemaining", 0);
            Scribe_Values.Look(ref nextRocketTick, "chimeraNextRocketTick", 0);
            Scribe_Values.Look(ref nextSalvoTick, "chimeraNextSalvoTick", 0);
            Scribe_Values.Look(ref salvoTarget, "chimeraSalvoTarget", IntVec3.Invalid);
            Scribe_Values.Look(ref pendingGun, "chimeraPendingGun", ChimeraGun.None);
            Scribe_TargetInfo.Look(ref gunTarget, "chimeraGunTarget");
            Scribe_Values.Look(ref gunWarmupEnd, "chimeraGunWarmupEnd", 0);
            Scribe_Values.Look(ref nextCannonTick, "chimeraNextCannonTick", 0);
            Scribe_Values.Look(ref nextRailgunTick, "chimeraNextRailgunTick", 0);
            Scribe_Values.Look(ref escortEndTick, "chimeraEscortEndTick", 0);
            Scribe_Values.Look(ref retreatEndTick, "chimeraRetreatEndTick", 0);
            Scribe_Values.Look(ref retreatCell, "chimeraRetreatCell", IntVec3.Invalid);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                rocketsRemaining = Mathf.Clamp(rocketsRemaining, 0, SalvoSize);
                launcherTicks = Mathf.Clamp(launcherTicks, 0, WarmupTicks);
                if (!salvoTarget.IsValid) rocketsRemaining = 0;
                if (!gunTarget.IsValid || (pendingGun != ChimeraGun.Cannon && pendingGun != ChimeraGun.Railgun))
                    pendingGun = ChimeraGun.None;
                // An old or edited save must not run two weapon sequences together.
                if (rocketsRemaining > 0) pendingGun = ChimeraGun.None;
                if (float.IsNaN(turretHeading) || float.IsInfinity(turretHeading)) turretHeading = 225f;
                turretHeading = Mathf.Repeat(turretHeading, 360f);
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (Spawned && !Dead && !Downed && pather != null && pather.MovingNow)
            {
                walkTicks = (walkTicks + 1) % (FrameCount * FrameTicks);
                int dx = pather.nextCell.x - Position.x;
                if (dx != 0 && facingEast != (dx > 0))
                {
                    facingEast = dx > 0;
                }
            }
            TickTurret();
            TickLauncher(rocketsRemaining > 0 && Spawned && !Dead && !Downed);
            TickSalvo();
            TickGun();
            if (retreatEndTick > 0) { TickRetreat(); return; }
            TickEscort();
        }

        public void EnableAutonomousEscort(int durationTicks)
        {
            escortEndTick = Find.TickManager.TicksGame + durationTicks;
        }

        public void BeginRetreat()
        {
            if (!Spawned || Dead || retreatEndTick > 0) return;
            escortEndTick = 0;
            retreatEndTick = Find.TickManager.TicksGame + 60000;
            CancelFire();
            jobs.StopAll();
            pather.StopDead();
            TryStartRetreat();
        }

        private void TickRetreat()
        {
            if (!Spawned || Dead) return;
            int now = Find.TickManager.TicksGame;
            if (now >= retreatEndTick || retreatCell.IsValid && Position == retreatCell)
            { Destroy(DestroyMode.Vanish); return; }
            if (now % 120 == thingIDNumber % 120 && (pather == null || !pather.MovingNow))
                TryStartRetreat();
        }

        private void TryStartRetreat()
        {
            if (!Spawned) return;
            IntVec3 exit;
            if (!CellFinder.TryFindRandomPawnExitCell(this, out exit)
                || !this.CanReach(exit, PathEndMode.OnCell, Danger.Deadly)) return;
            retreatCell = exit;
            Job job = JobMaker.MakeJob(JobDefOf.Goto, exit);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 3000;
            jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.Misc);
        }

        private void TickEscort()
        {
            if (escortEndTick <= 0 || retreatEndTick > 0 || !Spawned || Dead) return;
            int now = Find.TickManager.TicksGame;
            if (now >= escortEndTick) { Destroy(DestroyMode.Vanish); return; }
            if (Downed || Firing || now % 120 != thingIDNumber % 120) return;
            bool cannonReady = nextCannonTick <= now && ChimeraWeapons.ProjectileDef(ChimeraGun.Cannon) != null;
            bool railgunReady = nextRailgunTick <= now && ChimeraWeapons.ProjectileDef(ChimeraGun.Railgun) != null;
            Pawn target = null;
            Pawn nearestHostile = null;
            ChimeraGun gun = ChimeraGun.None;
            float distance = float.MaxValue;
            float nearestDistance = float.MaxValue;
            foreach (Pawn candidate in Map.mapPawns.AllPawnsSpawned)
            {
                if (candidate == this || candidate.Dead || candidate.Downed || candidate.Faction == null
                    || Faction == null || !Faction.HostileTo(candidate.Faction)) continue;
                float current = Position.DistanceToSquared(candidate.Position);
                if (current < nearestDistance)
                {
                    nearestHostile = candidate;
                    nearestDistance = current;
                }
                if (current >= distance) continue;
                ChimeraGun candidateGun = cannonReady && ValidGunTarget(ChimeraGun.Cannon, candidate)
                    ? ChimeraGun.Cannon : railgunReady && ValidGunTarget(ChimeraGun.Railgun, candidate)
                        ? ChimeraGun.Railgun : ChimeraGun.None;
                if (candidateGun == ChimeraGun.None) continue;
                target = candidate;
                gun = candidateGun;
                distance = current;
            }
            if (target != null) { BeginGun(gun, target); return; }
            if (nearestHostile == null || pather == null || pather.MovingNow
                || (!cannonReady && !railgunReady && nearestDistance <=
                    ChimeraWeapons.Range(ChimeraGun.Railgun) * ChimeraWeapons.Range(ChimeraGun.Railgun))
                || !this.CanReach(nearestHostile, PathEndMode.Touch, Danger.Deadly)) return;
            Job approach = JobMaker.MakeJob(JobDefOf.Goto, nearestHostile);
            approach.locomotionUrgency = LocomotionUrgency.Jog;
            approach.expiryInterval = 1200;
            jobs.StartJob(approach, JobCondition.InterruptForced, tag: JobTag.Misc);
        }

        private void TickTurret()
        {
            if (!Spawned || Dead || Downed) return;
            float desired = turretHeading;
            if (pendingGun != ChimeraGun.None && gunTarget.IsValid)
                desired = AimYaw(pendingGun, gunTarget);
            else if (rocketsRemaining > 0 && salvoTarget.IsValid)
                desired = AimYaw(ChimeraGun.Railgun, new LocalTargetInfo(salvoTarget));
            else if (pather != null && pather.MovingNow) desired = facingEast ? 135f : 225f;
            turretHeading = Mathf.Repeat(Mathf.MoveTowardsAngle(turretHeading, desired, TurretDegreesPerTick), 360f);
        }

        private float AimYaw(ChimeraGun gun, LocalTargetInfo target)
        {
            return ChimeraTurretArt.AimHeading(def.graphicData.drawSize, gun,
                target.Cell.ToVector3Shifted() - DrawPos);
        }

        private Vector3 GunMuzzle(ChimeraGun gun, float yaw)
        {
            Vector2 size = def.graphicData.drawSize;
            return DrawPos + ChimeraTurretArt.Muzzle(size, yaw, gun);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            int frame = !Dead && !Downed && pather != null && pather.MovingNow
                ? 1 + walkTicks / FrameTicks : 0;
            if (Materials[frame] == null)
            {
                string texture = frame == 0 ? "BodyIdle" : "BodyWalk" + frame.ToString("00");
                Materials[frame] = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Pawn/Foundation/Chimera/" + texture,
                    ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white).MatSingle;
            }
            Vector2 size = def.graphicData.drawSize;
            Mesh mesh = facingEast ? MeshPool.plane10Flip : MeshPool.plane10;
            Graphics.DrawMesh(mesh,
                Matrix4x4.TRS(drawLoc, Quaternion.identity, new Vector3(size.x, 1f, size.y)),
                Materials[frame], 0);

            // Choose an upright 3D yaw view. Never rotate the perspective drawing in the map plane.
            ChimeraTurretPose pose = ChimeraTurretArt.Pose(turretHeading);
            int poseIndex = Array.IndexOf(ChimeraTurretPoseData.Poses, pose);
            for (int layer = 0; layer < UpperTextures.Length; layer++)
                if (UpperMaterials[poseIndex, layer] == null)
                    UpperMaterials[poseIndex, layer] = GraphicDatabase.Get<Graphic_Single>(
                        "Things/Pawn/Foundation/Chimera/Directions/" + UpperTextures[layer] + "_" + pose.Name,
                        ShaderDatabase.Cutout, new Vector2(size.x, size.x), Color.white).MatSingle;
            Vector3 mount = drawLoc + ChimeraTurretArt.Mount(size);
            DrawTurretLayer(mount + new Vector3(0f, 0.01f, 0f), size.x, 1f, UpperMaterials[poseIndex, 0]);

            float lift = Mathf.SmoothStep(0f, 1f, launcherTicks / (float)WarmupTicks);
            Vector3 supportLoc = mount + new Vector3(0f, 0.02f, pose.SupportFoot * size.x / 640f * (1f - lift));
            if (lift > 0f)
                DrawTurretLayer(supportLoc, size.x, lift, UpperMaterials[poseIndex, 1]);
            DrawTurretLayer(mount + new Vector3(0f, 0.03f, ChimeraWeaponGeometry.LauncherLift(size) * lift),
                size.x, 1f, UpperMaterials[poseIndex, 2]);
            DrawCannonFlash(size);
        }

        private void DrawTurretLayer(Vector3 center, float size, float heightScale, Material material)
        {
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size, 1f, size * heightScale)),
                material, 0);
        }

        private void DrawCannonFlash(Vector2 size)
        {
            int frame = Find.TickManager.TicksGame - lastCannonShotTick;
            if (frame < 0 || frame >= CannonFlashMaterials.Length || cannonFlashDirection.sqrMagnitude < 0.01f) return;
            if (CannonFlashMaterials[frame] == null)
                CannonFlashMaterials[frame] = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Mote/ChimeraCannonFlash" + (frame + 1).ToString("00"),
                    ShaderDatabase.TransparentPostLight, Vector2.one, Color.white).MatSingle;
            float length = size.x * 0.5f;
            Vector3 location = cannonFlashOrigin + cannonFlashDirection * (length * 0.3f);
            location.y = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);
            float angle = Mathf.Atan2(-cannonFlashDirection.z, cannonFlashDirection.x) * Mathf.Rad2Deg;
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(location, Quaternion.Euler(0f, angle, 0f), new Vector3(length, 1f, length)),
                CannonFlashMaterials[frame], 0);
        }

        private void TickLauncher(bool opening)
        {
            launcherTicks = Mathf.Clamp(launcherTicks + (opening ? 1 : -1), 0, WarmupTicks);
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            // Use the same custom pawn render entry point as the SCP pawns.
            if (phase == DrawPhase.Draw) DrawAt(drawLoc, flip);
        }

        public override string GetInspectString()
        {
            string result = "SCP_ChimeraInspect".Translate();
            if (rocketsRemaining > 0) result += "\n" + "SCP_ChimeraLaunching".Translate(rocketsRemaining);
            if (pendingGun != ChimeraGun.None)
                result += "\n" + "SCP_ChimeraGunPreparing".Translate(GunLabel(pendingGun));
            int tick = Find.TickManager.TicksGame;
            if (nextCannonTick > tick)
                result += "\n" + "SCP_ChimeraGunReloading".Translate(GunLabel(ChimeraGun.Cannon), Mathf.CeilToInt((nextCannonTick - tick) / 60f));
            if (nextRailgunTick > tick)
                result += "\n" + "SCP_ChimeraGunReloading".Translate(GunLabel(ChimeraGun.Railgun), Mathf.CeilToInt((nextRailgunTick - tick) / 60f));
            int reload = nextSalvoTick - Find.TickManager.TicksGame;
            if (reload > 0) result += "\n" + "SCP_ChimeraReloading".Translate(Mathf.CeilToInt(reload / 60f));
            return result;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!Prefs.DevMode || !Spawned || Dead || Downed) yield break;
            yield return new Gizmo_ChimeraController(this);
            yield return new Command_Action
            {
                defaultLabel = "SCP_ChimeraStop".Translate(),
                defaultDesc = "SCP_ChimeraStopDesc".Translate(),
                action = () => { CancelFire(); jobs.StopAll(); pather.StopDead(); }
            };
        }

        private sealed class Gizmo_ChimeraController : Gizmo
        {
            private readonly Pawn_FoundationChimera chimera;
            private readonly Texture2D chimeraIcon;
            private readonly Texture2D cannonIcon;
            private readonly Texture2D railgunIcon;
            private readonly Texture2D rocketIcon;

            public Gizmo_ChimeraController(Pawn_FoundationChimera chimera)
            {
                this.chimera = chimera;
                Order = -90f;
                chimeraIcon = ContentFinder<Texture2D>.Get("Things/Pawn/Foundation/Chimera/Idle");
                cannonIcon = ContentFinder<Texture2D>.Get("UI/ChimeraCannon");
                railgunIcon = ContentFinder<Texture2D>.Get("UI/ChimeraRailgun");
                rocketIcon = ContentFinder<Texture2D>.Get("UI/ChimeraMissileSalvo");
            }

            public override float GetWidth(float maxWidth) { return 555f; }

            public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
            {
                Rect panel = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
                GameFont oldFont = Text.Font;
                TextAnchor oldAnchor = Text.Anchor;
                Color oldColor = GUI.color;
                try
                {
                    GUI.DrawTexture(panel, Command.BGTex);
                    Widgets.DrawHighlightIfMouseover(panel);
                    GUI.DrawTexture(new Rect(panel.x + 9f, panel.y + 5f, 66f, 51f), chimeraIcon, ScaleMode.ScaleToFit);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(new Rect(panel.x + 5f, panel.y + 57f, 75f, 18f),
                        "SCP_ChimeraPanelTitle".Translate());
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                    Widgets.DrawLineVertical(panel.x + 84f, panel.y + 10f, 58f);
                    Widgets.DrawLineVertical(panel.x + 343f, panel.y + 10f, 58f);
                    GUI.color = Color.white;

                    int tick = Find.TickManager.TicksGame;
                    DrawButton(panel, 94f, chimeraIcon, "SCP_ChimeraPanelMove".Translate(),
                        "SCP_ChimeraWalkDesc".Translate(),
                        chimera.Firing ? "SCP_ChimeraWeaponBusy".Translate().ToString() : null,
                        chimera.pather != null && chimera.pather.MovingNow, chimera.BeginWalkSelection);
                    DrawButton(panel, 158f, cannonIcon, "SCP_ChimeraCannon".Translate(),
                        "SCP_ChimeraCannonDesc".Translate(), chimera.GunDisabledReason(ChimeraGun.Cannon, tick),
                        chimera.pendingGun == ChimeraGun.Cannon,
                        () => chimera.BeginGunSelection(ChimeraGun.Cannon));
                    DrawButton(panel, 222f, railgunIcon, "SCP_ChimeraRailgun".Translate(),
                        "SCP_ChimeraRailgunDesc".Translate(), chimera.GunDisabledReason(ChimeraGun.Railgun, tick),
                        chimera.pendingGun == ChimeraGun.Railgun,
                        () => chimera.BeginGunSelection(ChimeraGun.Railgun));
                    DrawButton(panel, 286f, rocketIcon, "SCP_ChimeraPanelRockets".Translate(),
                        "SCP_ChimeraSalvoDesc".Translate(), chimera.SalvoDisabledReason(tick),
                        chimera.rocketsRemaining > 0, chimera.BeginSalvoSelection);

                    Widgets.Label(new Rect(panel.x + 353f, panel.y + 7f, 195f, 20f),
                        chimera.GunStatus(ChimeraGun.Cannon, tick));
                    Widgets.Label(new Rect(panel.x + 353f, panel.y + 27f, 195f, 20f),
                        chimera.GunStatus(ChimeraGun.Railgun, tick));
                    Widgets.Label(new Rect(panel.x + 353f, panel.y + 47f, 195f, 20f),
                        chimera.RocketStatus(tick));
                }
                finally
                {
                    Text.Font = oldFont;
                    Text.Anchor = oldAnchor;
                    GUI.color = oldColor;
                }
                return new GizmoResult(GizmoState.Clear);
            }

            private static void DrawButton(Rect panel, float offset, Texture2D icon, string label,
                string description, string disabledReason, bool active, Action action)
            {
                Rect button = new Rect(panel.x + offset, panel.y + 9f, 48f, 48f);
                GUI.DrawTexture(button, Command.BGTex);
                Widgets.DrawHighlightIfMouseover(button);
                if (active)
                {
                    Widgets.DrawBoxSolid(button, new Color(1f, 0.5f, 0f, 0.3f));
                    GUI.color = new Color(1f, 0.5f, 0f);
                    Widgets.DrawBox(button, 2);
                    GUI.color = Color.white;
                }
                if (disabledReason == null)
                {
                    if (Widgets.ButtonImage(button, icon))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        action();
                    }
                }
                else
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    GUI.DrawTexture(button, icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                    description += "\n" + disabledReason;
                }
                TooltipHandler.TipRegion(button, label + "\n" + description);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(button.x - 9f, panel.y + 57f, 66f, 18f), label);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void BeginWalkSelection()
        {
            if (!Prefs.DevMode || !Spawned || Dead || Downed) return;
            Messages.Message("SCP_ChimeraChooseDestination".Translate(), MessageTypeDefOf.NeutralEvent, false);
            Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target => MoveTo(target.Cell));
        }

        private void MoveTo(IntVec3 cell)
        {
            if (!Prefs.DevMode || !Spawned || Dead || Downed || Map != Find.CurrentMap || Firing) return;
            if (!cell.InBounds(Map) || cell.Fogged(Map) || !cell.Standable(Map)
                || !this.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
            {
                Messages.Message("SCP_ChimeraNoPath".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            job.playerForced = true;
            // TryTakeOrderedJob only queues orders. The prototype's idle tree
            // has no ThinkNode_QueuedJob, so start this debug move directly.
            // Discard pending moves left in saves by the former implementation.
            jobs.ClearQueuedJobs(true);
            jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.Misc);
        }

        private void BeginSalvoSelection()
        {
            if (!Prefs.DevMode || !Spawned || Dead || Downed || ChimeraMissiles.RocketDef == null) return;
            Messages.Message("SCP_ChimeraChooseSalvoTarget".Translate(), MessageTypeDefOf.NeutralEvent, false);
            Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target => BeginSalvo(target.Cell),
                target =>
                {
                    if (!Spawned) return;
                    GenDraw.DrawCircleOutline(Position.ToVector3Shifted(), MaximumRange);
                    GenDraw.DrawTargetHighlight(target);
                });
        }

        private bool ValidSalvoCell(IntVec3 cell)
        {
            if (!Spawned || !cell.InBounds(Map) || cell.Fogged(Map) || cell.Roofed(Map)) return false;
            float distance = cell.DistanceTo(Position);
            return distance >= MinimumRange && distance <= MaximumRange;
        }

        private void BeginSalvo(IntVec3 cell)
        {
            if (!Prefs.DevMode || !Spawned || Dead || Downed || Map != Find.CurrentMap
                || Firing || nextSalvoTick > Find.TickManager.TicksGame
                || ChimeraMissiles.RocketDef == null) return;
            if (Position.Roofed(Map) || !ValidSalvoCell(cell))
            {
                Messages.Message("SCP_ChimeraSalvoInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            jobs.StopAll();
            pather.StopDead();
            salvoTarget = cell;
            rocketsRemaining = SalvoSize;
            nextRocketTick = Find.TickManager.TicksGame + WarmupTicks;
            nextSalvoTick = nextRocketTick + (SalvoSize - 1) * LaunchInterval + ReloadTicks;
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            wait.expiryInterval = WarmupTicks + SalvoSize * LaunchInterval + 30;
            jobs.StartJob(wait, JobCondition.InterruptForced, tag: JobTag.Misc);
        }

        private void TickSalvo()
        {
            if (rocketsRemaining <= 0) return;
            if (!Spawned || Dead || Downed || Position.Roofed(Map)
                || ChimeraMissiles.RocketDef == null || !ValidSalvoCell(salvoTarget))
            {
                rocketsRemaining = 0;
                return;
            }
            if (Find.TickManager.TicksGame < nextRocketTick || launcherTicks < WarmupTicks) return;
            Projectile projectile = null;
            try
            {
                List<IntVec3> cells = new List<IntVec3>();
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(salvoTarget, 3f, true))
                    if (ValidSalvoCell(cell)) cells.Add(cell);
                IntVec3 aim = cells.RandomElement();
                // Two neighboring ports under the raised roof cassette, mirrored with the sprite.
                Vector2 size = def.graphicData.drawSize;
                Vector3 origin = DrawPos + ChimeraTurretArt.RocketPort(size, turretHeading, rocketsRemaining % 2 != 0);
                origin.x = Mathf.Clamp(origin.x, 0.1f, Map.Size.x - 0.1f);
                origin.z = Mathf.Clamp(origin.z, 0.1f, Map.Size.z - 0.1f);
                projectile = (Projectile)ThingMaker.MakeThing(ChimeraMissiles.RocketDef);
                GenSpawn.Spawn(projectile, Position, Map);
                projectile.Launch(this, origin, new LocalTargetInfo(aim), new LocalTargetInfo(aim),
                    ProjectileHitFlags.All, true);
                SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("InfernoCannon_Fire");
                if (sound != null) sound.PlayOneShot(new TargetInfo(Position, Map));
                FleckMaker.Static(origin, Map, FleckDefOf.ShotFlash, 1.2f);
                rocketsRemaining--;
                nextRocketTick = Find.TickManager.TicksGame + LaunchInterval;
                nextSalvoTick = Find.TickManager.TicksGame + ReloadTicks;
            }
            catch (Exception exception)
            {
                rocketsRemaining = 0;
                if (projectile != null && !projectile.Destroyed) projectile.Destroy();
                Log.ErrorOnce("[SCP Foundation] Vanguard missile salvo stopped: " + exception, 68217394);
            }
        }

        private static string GunLabel(ChimeraGun gun)
        {
            return (gun == ChimeraGun.Cannon ? "SCP_ChimeraCannon" : "SCP_ChimeraRailgun").Translate().ToString();
        }

        private int NextGunTick(ChimeraGun gun) { return gun == ChimeraGun.Cannon ? nextCannonTick : nextRailgunTick; }

        private string GunDisabledReason(ChimeraGun gun, int tick)
        {
            if (ChimeraWeapons.ProjectileDef(gun) == null)
                return "SCP_ChimeraGunUnavailable".Translate();
            if (Firing) return "SCP_ChimeraWeaponBusy".Translate();
            if (NextGunTick(gun) > tick)
                return "SCP_ChimeraGunReloading".Translate(GunLabel(gun),
                    Mathf.CeilToInt((NextGunTick(gun) - tick) / 60f));
            return null;
        }

        private string SalvoDisabledReason(int tick)
        {
            if (ChimeraMissiles.RocketDef == null) return "SCP_ChimeraRocketUnavailable".Translate();
            if (Firing) return "SCP_ChimeraWeaponBusy".Translate();
            if (nextSalvoTick > tick)
                return "SCP_ChimeraReloading".Translate(Mathf.CeilToInt((nextSalvoTick - tick) / 60f));
            if (Position.Roofed(Map)) return "SCP_ChimeraSalvoRoofed".Translate();
            return null;
        }

        private string GunStatus(ChimeraGun gun, int tick)
        {
            string label = GunLabel(gun);
            if (pendingGun == gun)
                return "SCP_ChimeraPanelPreparing".Translate(label,
                    Mathf.Max(0, Mathf.CeilToInt((gunWarmupEnd - tick) / 60f)));
            int remaining = Mathf.Max(0, Mathf.CeilToInt((NextGunTick(gun) - tick) / 60f));
            int full = ChimeraWeapons.Reload(gun) / 60;
            return (remaining > 0 ? "SCP_ChimeraPanelReload".Translate(label, remaining, full)
                : "SCP_ChimeraPanelReady".Translate(label, full)).ToString();
        }

        private string RocketStatus(int tick)
        {
            string label = "SCP_ChimeraPanelRockets".Translate();
            if (rocketsRemaining > 0)
                return "SCP_ChimeraPanelLaunching".Translate(label, SalvoSize - rocketsRemaining, SalvoSize);
            int remaining = Mathf.Max(0, Mathf.CeilToInt((nextSalvoTick - tick) / 60f));
            int full = ReloadTicks / 60;
            return (remaining > 0 ? "SCP_ChimeraPanelReload".Translate(label, remaining, full)
                : "SCP_ChimeraPanelReady".Translate(label, full)).ToString();
        }

        private void BeginGunSelection(ChimeraGun gun)
        {
            if (!Prefs.DevMode || !Spawned || Dead || Downed || Firing) return;
            Messages.Message("SCP_ChimeraChooseGunTarget".Translate(GunLabel(gun)), MessageTypeDefOf.NeutralEvent, false);
            Find.Targeter.BeginTargeting(ChimeraWeapons.TargetParameters(), target => BeginGun(gun, target),
                target =>
                {
                    if (!Spawned) return;
                    // DrawRadiusRing uses GenRadial's precomputed cells and throws above
                    // ~79.8 tiles. The railgun reaches 85, so use the unrestricted
                    // geometric outline for the outer boundary and a normal inner ring.
                    GenDraw.DrawCircleOutline(Position.ToVector3Shifted(), ChimeraWeapons.Range(gun));
                    GenDraw.DrawRadiusRing(Position, ChimeraWeapons.MinimumRange);
                    GenDraw.DrawTargetHighlight(target);
                });
        }

        private bool ValidGunTarget(ChimeraGun gun, LocalTargetInfo target)
        {
            if (!Spawned || !target.IsValid || (target.HasThing &&
                (target.Thing == this || !target.Thing.Spawned || target.Thing.Destroyed || target.Thing.Map != Map))) return false;
            IntVec3 cell = target.Cell;
            if (!cell.InBounds(Map) || cell.Fogged(Map)) return false;
            float distance = Position.DistanceTo(cell);
            if (distance < ChimeraWeapons.MinimumRange || distance > ChimeraWeapons.Range(gun)) return false;
            IntVec3 muzzle = GunMuzzle(gun, AimYaw(gun, target)).ToIntVec3();
            return muzzle.InBounds(Map) && GenSight.LineOfSight(muzzle, cell, Map);
        }

        private void BeginGun(ChimeraGun gun, LocalTargetInfo target)
        {
            if ((!Prefs.DevMode && escortEndTick <= Find.TickManager.TicksGame)
                || !Spawned || Dead || Downed || (escortEndTick <= Find.TickManager.TicksGame && Map != Find.CurrentMap) || Firing
                || NextGunTick(gun) > Find.TickManager.TicksGame || ChimeraWeapons.ProjectileDef(gun) == null) return;
            if (!target.IsValid) return;
            if (!ValidGunTarget(gun, target))
            {
                Messages.Message("SCP_ChimeraGunInvalid".Translate(ChimeraWeapons.MinimumRange, ChimeraWeapons.Range(gun)),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            jobs.StopAll();
            pather.StopDead();
            pendingGun = gun;
            gunTarget = target;
            int turnTicks = Mathf.CeilToInt(Mathf.Abs(Mathf.DeltaAngle(turretHeading, AimYaw(gun, target))) / TurretDegreesPerTick);
            gunWarmupEnd = Find.TickManager.TicksGame + turnTicks + ChimeraWeapons.Warmup(gun);
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            wait.expiryInterval = turnTicks + ChimeraWeapons.Warmup(gun) + 30;
            jobs.StartJob(wait, JobCondition.InterruptForced, tag: JobTag.Misc);
        }

        private void CancelFire()
        {
            rocketsRemaining = 0;
            pendingGun = ChimeraGun.None;
            gunTarget = LocalTargetInfo.Invalid;
            // Completed shots keep their cooldowns. A cancelled warmup consumes no shot.
        }

        private void TickGun()
        {
            if (pendingGun == ChimeraGun.None) return;
            if (!Spawned || Dead || Downed || ChimeraWeapons.ProjectileDef(pendingGun) == null
                || !ValidGunTarget(pendingGun, gunTarget))
            {
                if (Spawned && !Dead && !Downed && escortEndTick <= 0)
                    Messages.Message("SCP_ChimeraGunLostTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                pendingGun = ChimeraGun.None;
                gunTarget = LocalTargetInfo.Invalid;
                return;
            }
            int tick = Find.TickManager.TicksGame;
            bool aligned = Mathf.Abs(Mathf.DeltaAngle(turretHeading, AimYaw(pendingGun, gunTarget))) <= 0.1f;
            Vector3 origin = GunMuzzle(pendingGun, turretHeading);
            if (tick < gunWarmupEnd)
            {
                if (pendingGun == ChimeraGun.Railgun && aligned) DrawRailCharge(tick);
                return;
            }
            // Moving targets can change heading during warmup. Never fire across the barrel axis.
            if (!aligned) return;
            Projectile projectile = null;
            try
            {
                projectile = (Projectile)ThingMaker.MakeThing(ChimeraWeapons.ProjectileDef(pendingGun));
                GenSpawn.Spawn(projectile, origin.ToIntVec3(), Map);
                bool cannon = pendingGun == ChimeraGun.Cannon;
                projectile.Launch(this, origin, gunTarget, gunTarget, ProjectileHitFlags.All, cannon);
                SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(cannon ? "SCP_ChimeraCannonFire" : "SCP_ChimeraRailgunFire");
                if (sound != null) sound.PlayOneShot(new TargetInfo(Position, Map));
                FleckMaker.Static(origin, Map, FleckDefOf.ShotFlash, cannon ? 1.5f : 2f);
                if (cannon)
                {
                    FleckMaker.ThrowSmoke(origin, Map, 2.4f);
                    nextCannonTick = tick + ChimeraWeapons.Reload(pendingGun);
                    lastCannonShotTick = tick;
                    cannonFlashOrigin = origin;
                    cannonFlashDirection = gunTarget.Cell.ToVector3Shifted() - origin;
                    cannonFlashDirection.y = 0f;
                    cannonFlashDirection.Normalize();
                }
                else
                {
                    nextRailgunTick = tick + ChimeraWeapons.Reload(pendingGun);
                }
            }
            catch (Exception exception)
            {
                if (projectile != null && !projectile.Destroyed) projectile.Destroy();
                Log.ErrorOnce("[SCP Foundation] Vanguard gun shot stopped: " + exception, 68217395);
            }
            pendingGun = ChimeraGun.None;
            gunTarget = LocalTargetInfo.Invalid;
        }

        private void DrawRailCharge(int tick)
        {
            if (tick % 3 != 0) return;
            float charge = 1f - Mathf.Clamp01((gunWarmupEnd - tick) / (float)ChimeraWeapons.Warmup(ChimeraGun.Railgun));
            const int nodes = 9;
            int activeNodes = Mathf.Clamp(1 + Mathf.FloorToInt(charge * nodes), 1, nodes);
            Vector2 size = def.graphicData.drawSize;
            for (int index = 0; index < activeNodes; index++)
            {
                float along = index / (float)(nodes - 1);
                Vector3 point = DrawPos + ChimeraTurretArt.Channel(size, turretHeading, along);
                EmitRailFleck(FleckDefOf.ShotFlash, point, 0.45f + charge * 0.6f,
                    new Color(0.15f, 0.75f, 1f, 0.7f + charge * 0.3f));
                if ((tick + index) % 6 == 0)
                    EmitRailFleck(FleckDefOf.MicroSparks, point, 0.35f + charge * 0.4f,
                        new Color(0.65f, 0.95f, 1f, 1f));
            }
        }

        private void EmitRailFleck(FleckDef fleck, Vector3 point, float scale, Color color)
        {
            if (!point.InBounds(Map)) return;
            FleckCreationData data = FleckMaker.GetDataStatic(point, Map, fleck, scale);
            data.instanceColor = color;
            Map.flecks.CreateFleck(data);
        }
    }
}
