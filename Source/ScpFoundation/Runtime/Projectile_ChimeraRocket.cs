using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    internal static class ChimeraMissiles
    {
        internal static ThingDef RocketDef
        {
            get { return DefDatabase<ThingDef>.GetNamedSilentFail("SCP_ChimeraRocket"); }
        }
    }

    // UF-style curved flight and layered exhaust; native RimWorld handles collisions,
    // shields and the explosion. Only the two attributed PNGs were copied from UF.
    public sealed class Projectile_ChimeraRocket : Projectile_Explosive
    {
        private bool initialized;
        private Vector3 firstControl;
        private Vector3 secondControl;
        private int flightDuration;
        private int lastTrailTick;

        public override int UpdateRateTicks { get { return 1; } }

        private float Progress
        {
            get { return flightDuration > 0 ? Mathf.Clamp01(1f - ticksToImpact / (float)flightDuration) : 0f; }
        }

        public override Vector3 ExactPosition
        {
            get
            {
                if (!initialized) return base.ExactPosition;
                Vector3 point = ChimeraRocketTrajectory.Point(origin, firstControl, secondControl, destination, Progress);
                point.y = def.Altitude;
                return point;
            }
        }

        public override Quaternion ExactRotation
        {
            get
            {
                if (!initialized) return base.ExactRotation;
                Vector3 direction = ChimeraRocketTrajectory.Tangent(origin, firstControl, secondControl, destination, Progress);
                direction.y = 0f;
                return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : base.ExactRotation;
            }
        }

        public override void Launch(Thing launcher, Vector3 launchOrigin, LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false,
            Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, launchOrigin, usedTarget, intendedTarget, hitFlags,
                preventFriendlyFire, equipment, targetCoverDef);
            firstControl = ClampControl((origin + destination) / 2f + Rand.InsideAnnulusVector3(7f, 12f));
            secondControl = ClampControl(origin + Rand.InsideAnnulusVector3(7f, 12f));
            float length = ChimeraRocketTrajectory.Length(origin, firstControl, secondControl, destination);
            flightDuration = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.001f, def.projectile.SpeedTilesPerTick)));
            ticksToImpact = flightDuration;
            lastTrailTick = Find.TickManager.TicksGame;
            initialized = true;
        }

        private Vector3 ClampControl(Vector3 control)
        {
            control.x = Mathf.Clamp(control.x, 0.1f, Map.Size.x - 0.1f);
            control.z = Mathf.Clamp(control.z, 0.1f, Map.Size.z - 0.1f);
            control.y = 0f;
            return control;
        }

        protected override void TickInterval(int delta)
        {
            Vector3 previous = ExactPosition;
            base.TickInterval(delta);
            if (!initialized || !Spawned || Destroyed || landed) return;
            int tick = Find.TickManager.TicksGame;
            if (tick == lastTrailTick) return;
            lastTrailTick = tick;
            Vector3 current = ExactPosition;
            int elapsed = flightDuration - ticksToImpact;
            if (elapsed % 3 == 0)
            {
                Emit(FleckDefOf.MicroSparks, current, 1f, Color.white);
                Emit(FleckDefOf.Smoke, current + new Vector3(0f, 0f, -0.5f), 1.5f,
                    new Color(200f / 255f, 200f / 255f, 200f / 255f, 100f / 255f));
            }
            int segments = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(previous, current)), 1, 20);
            for (int i = 0; i < segments; i++)
                Emit(FleckDefOf.ShotFlash, Vector3.Lerp(previous, current, i / (float)segments), 1.5f,
                    new Color(1f, 200f / 255f, 150f / 255f, 100f / 255f));
        }

        private void Emit(FleckDef effect, Vector3 position, float scale, Color color)
        {
            if (!position.InBounds(Map)) return;
            FleckCreationData data = FleckMaker.GetDataStatic(position, Map, effect, scale);
            data.instanceColor = color;
            Map.flecks.CreateFleck(data);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialized, "chimeraFlightInitialized", false);
            Scribe_Values.Look(ref firstControl, "chimeraFirstControl");
            Scribe_Values.Look(ref secondControl, "chimeraSecondControl");
            Scribe_Values.Look(ref flightDuration, "chimeraFlightDuration", 0);
            Scribe_Values.Look(ref lastTrailTick, "chimeraLastTrailTick", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !initialized
                && Scribe.loader.curXmlParent["estimatedTotalTicks"] != null)
            {
                // Restore only this projectile's former UF flight data. The save
                // file is not edited and an active salvo does not restart from zero.
                int elapsed = 0;
                Scribe_Values.Look(ref initialized, "initialized", false);
                Scribe_Values.Look(ref firstControl, "controlPoint1");
                Scribe_Values.Look(ref secondControl, "controlPoint2");
                Scribe_Values.Look(ref flightDuration, "estimatedTotalTicks", 0);
                Scribe_Values.Look(ref elapsed, "ticksFlying", 0);
                if (!initialized || flightDuration < 2)
                {
                    firstControl = Vector3.Lerp(origin, destination, 1f / 3f);
                    secondControl = Vector3.Lerp(origin, destination, 2f / 3f);
                    flightDuration = Mathf.Max(2, Mathf.CeilToInt(StartingTicksToImpact));
                    elapsed = 0;
                }
                ticksToImpact = Mathf.Clamp(flightDuration - elapsed, 0, flightDuration);
                lastTrailTick = -1;
                initialized = true;
            }
        }
    }
}
