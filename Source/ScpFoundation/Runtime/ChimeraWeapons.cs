using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    internal enum ChimeraGun { None, Cannon, Railgun }

    internal static class ChimeraWeapons
    {
        internal const float MinimumRange = 6f;
        internal static ThingDef ProjectileDef(ChimeraGun gun)
        {
            if (gun == ChimeraGun.None) return null;
            return DefDatabase<ThingDef>.GetNamedSilentFail(
                gun == ChimeraGun.Cannon ? "SCP_ChimeraCannonShell" : "SCP_ChimeraRailSlug");
        }
        internal static int Warmup(ChimeraGun gun) { return gun == ChimeraGun.Cannon ? 45 : 90; }
        internal static int Reload(ChimeraGun gun) { return gun == ChimeraGun.Cannon ? 180 : 360; }
        internal static float Range(ChimeraGun gun) { return gun == ChimeraGun.Cannon ? 115f : 150f; }
        internal static TargetingParameters TargetParameters()
        {
            TargetingParameters parameters = TargetingParameters.ForAttackAny();
            parameters.canTargetLocations = true;
            return parameters;
        }
    }

    // Registration coordinates belong to the approved 640x440 artwork, not world cells.
    // All upper layers, ports and muzzles therefore follow the ThingDef's draw size.
    internal static class ChimeraWeaponGeometry
    {
        internal static Vector3 Muzzle(Vector2 size, bool east, ChimeraGun gun)
        {
            float x = (150f / 640f - 0.5f) * size.x;
            float z = (0.5f - 177f / 440f) * size.y;
            if (gun == ChimeraGun.Railgun)
            {
                x = (147f / 640f - 0.5f) * size.x;
                z = (0.5f - 243f / 440f) * size.y;
            }
            return new Vector3(east ? -x : x, 0f, z);
        }

        internal static Vector3 RailChannel(Vector2 size, bool east, float progress)
        {
            // Centerline of the visible diagonal gap between the approved rails.
            progress = Mathf.Clamp01(progress);
            float pixelX = Mathf.Lerp(354f, 165f, progress);
            float pixelY = Mathf.Lerp(169f, 235f, progress);
            float x = (pixelX / 640f - 0.5f) * size.x;
            float z = (0.5f - pixelY / 440f) * size.y;
            return new Vector3(east ? -x : x, 0f, z);
        }

        internal static Vector3 TurretPivot(Vector2 size, bool east)
        {
            // BodyIdle's stationary mounting ring is centered at pixel (320,155).
            return new Vector3(0f, 0f, size.y * (0.5f - 155f / 440f));
        }

        internal static Vector3 Rotate(Vector3 point, float yaw)
        {
            float radians = yaw * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector3(point.x * cosine + point.z * sine, point.y,
                point.z * cosine - point.x * sine);
        }

        internal static Vector3 TurretPoint(Vector2 size, bool east, float yaw, Vector3 localPoint)
        {
            Vector3 pivot = TurretPivot(size, east);
            return pivot + Rotate(localPoint - pivot, yaw);
        }

        internal static Vector3 Axis(Vector2 size, bool east)
        {
            return (RailChannel(size, east, 1f) - RailChannel(size, east, 0f)).normalized;
        }

        internal static float AimYaw(Vector2 size, bool east, ChimeraGun gun, Vector3 targetOffset)
        {
            Vector3 pivot = TurretPivot(size, east);
            Vector3 target = targetOffset - pivot;
            target.y = 0f;
            if (target.sqrMagnitude < 0.001f) return 0f;
            Vector3 direction = Axis(size, east);
            Vector3 right = new Vector3(direction.z, 0f, -direction.x);
            Vector3 muzzle = Muzzle(size, east, gun) - pivot;
            float lateral = Vector3.Dot(muzzle, right);
            float correction = Mathf.Asin(Mathf.Clamp(lateral / target.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
            float targetHeading = Mathf.Atan2(target.x, target.z) * Mathf.Rad2Deg;
            float sourceHeading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            // Account for the selected gun's offset from the rotation axis, so both
            // parallel barrels aim exactly from their own muzzle, including nearby targets.
            return Mathf.DeltaAngle(0f, targetHeading - sourceHeading - correction);
        }
        internal static Vector3 RocketPort(Vector2 size, bool east, bool second)
        {
            float x = (-0.17f + (second ? 0.09f : -0.09f)) / 6f * size.x;
            return new Vector3(east ? -x : x, 0f, size.y * (1.125f / 4.125f));
        }
        internal static float LauncherLift(Vector2 size) { return size.y * (22f / 440f); }
        internal static float SupportAnchor(Vector2 size) { return size.y * (0.5f - 130f / 440f); }
    }

    // One native bullet per struck pawn keeps RimWorld's collision, armor and shields.
    public sealed class DamageWorker_ChimeraRail : DamageWorker_AddInjury
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Pawn pawn = victim as Pawn;
            if (pawn != null && pawn.Faction != null && dinfo.Instigator != null
                && dinfo.Instigator.Faction != null && !dinfo.Instigator.Faction.HostileTo(pawn.Faction))
                return new DamageResult();
            if (pawn != null && dinfo.HitPart == null && pawn.RaceProps.body != null)
                dinfo.SetHitPart(pawn.RaceProps.body.corePart);
            return base.Apply(dinfo, victim);
        }
    }

    public sealed class Projectile_ChimeraRailSlug : Bullet
    {
        private bool hasRailPath;
        private Vector3 railStart;
        private IntVec3 railEnd = IntVec3.Invalid;

        public override int UpdateRateTicks { get { return 1; } }

        protected override void ImpactSomething()
        {
            // Vanilla projectiles add aim jitter and can randomly miss a prone pawn
            // even when the shot reached its intended cell. The railgun is guided.
            Pawn target = usedTarget.HasThing ? usedTarget.Thing as Pawn : null;
            if (target != null && target.Spawned && target.Map == Map && !target.Dead
                && GenSight.LineOfSight(origin.ToIntVec3(), target.Position, Map))
            { Impact(target); return; }
            base.ImpactSomething();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hasRailPath, "chimeraRailHasPath", false);
            Scribe_Values.Look(ref railStart, "chimeraRailStart");
            Scribe_Values.Look(ref railEnd, "chimeraRailEnd", IntVec3.Invalid);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impact = hitThing is Pawn ? hitThing.DrawPos : ExactPosition;
            Thing shooter = launcher;
            Pawn hitPawn = hitThing as Pawn;
            bool shieldedPawn = hitPawn != null && HasActiveShieldBelt(hitPawn);
            if (map != null && !hasRailPath)
            {
                railStart = origin;
                // Penetrate pawns before the selected target, never beyond it.
                railEnd = destination.ToIntVec3();
                hasRailPath = true;
            }

            IntVec3 nextCell = IntVec3.Invalid;
            if (map != null && hitPawn != null && !blockedByShield && !shieldedPawn && railEnd.IsValid)
                nextCell = NextCell(hitThing.Position, railStart.ToIntVec3(), railEnd);

            base.Impact(hitThing, blockedByShield);
            if (map == null) return;
            if (nextCell.IsValid && nextCell.InBounds(map))
            {
                Projectile_ChimeraRailSlug next = null;
                try
                {
                    next = (Projectile_ChimeraRailSlug)ThingMaker.MakeThing(def);
                    next.hasRailPath = true;
                    next.railStart = railStart;
                    next.railEnd = railEnd;
                    GenSpawn.Spawn(next, nextCell, map);
                    LocalTargetInfo end = NextTarget(hitThing.Position, railStart.ToIntVec3(), railEnd, map);
                    next.Launch(shooter, nextCell.ToVector3Shifted(), end, end, ProjectileHitFlags.All, false);
                    return;
                }
                catch (System.Exception exception)
                {
                    if (next != null && !next.Destroyed) next.Destroy();
                    Log.ErrorOnce("[SCP Foundation] Vanguard rail penetration stopped: " + exception, 68217396);
                }
            }
            ChimeraRailVisuals.Draw(map, railStart, impact);
        }

        private static IntVec3 NextCell(IntVec3 hit, IntVec3 start, IntVec3 end)
        {
            List<IntVec3> cells = GenSight.BresenhamCellsBetween(start, end);
            for (int i = 0; i < cells.Count - 1; i++)
                if (cells[i] == hit) return cells[i + 1];
            return IntVec3.Invalid;
        }

        private static bool HasActiveShieldBelt(Pawn pawn)
        {
            if (pawn.apparel == null) return false;
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                CompShield shield = apparel.TryGetComp<CompShield>();
                if (shield != null && shield.ShieldState == ShieldState.Active) return true;
            }
            return false;
        }

        private static LocalTargetInfo NextTarget(IntVec3 hit, IntVec3 start, IntVec3 end, Map map)
        {
            List<IntVec3> cells = GenSight.BresenhamCellsBetween(start, end);
            bool pastHit = false;
            foreach (IntVec3 cell in cells)
            {
                if (!pastHit)
                {
                    if (cell == hit) pastHit = true;
                    continue;
                }
                if (!cell.InBounds(map)) break;
                List<Thing> things = cell.GetThingList(map);
                foreach (Thing thing in things)
                    if (thing.Spawned && thing is Building && thing.def.Fillage == FillCategory.Full)
                        return new LocalTargetInfo(thing);
                foreach (Thing thing in things)
                    if (thing.Spawned && thing is Pawn)
                        return new LocalTargetInfo(thing);
            }
            return new LocalTargetInfo(end);
        }

        protected override void TickInterval(int delta)
        {
            Map map = Map;
            Vector3 previous = ExactPosition;
            base.TickInterval(delta);
            if (map == null) return;
            // An early wall/shield impact must not paint a trail all the way to the original target.
            Vector3 current = Destroyed ? Position.ToVector3Shifted() : ExactPosition;
            int segments = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(previous, current) * 2f), 1, 20);
            for (int i = 0; i < segments; i++)
            {
                Vector3 point = Vector3.Lerp(previous, current, i / (float)segments);
                if (!point.InBounds(map)) continue;
                FleckCreationData flash = FleckMaker.GetDataStatic(point, map, FleckDefOf.ShotFlash, 1.15f);
                flash.instanceColor = new Color(0.35f, 0.82f, 1f, 0.95f);
                map.flecks.CreateFleck(flash);
            }
            if (!Destroyed && Spawned && current.InBounds(map))
                FleckMaker.Static(current, map, FleckDefOf.MicroSparks, 0.6f);
        }
    }

    internal static class ChimeraRailVisuals
    {
        internal static void Draw(Map map, Vector3 origin, Vector3 end)
        {
            if (map == null || !origin.InBounds(map) || !end.InBounds(map)) return;
            IntVec3 sourceCell = origin.ToIntVec3();
            IntVec3 endCell = end.ToIntVec3();
            TargetInfo source = new TargetInfo(sourceCell, map);
            TargetInfo destination = new TargetInfo(endCell, map);
            Vector3 sourceOffset = origin - sourceCell.ToVector3Shifted();
            Vector3 endOffset = end - endCell.ToVector3Shifted();
            ThingDef glow = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_MoteChimeraRailBeamGlow");
            ThingDef core = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_MoteChimeraRailBeamCore");
            if (glow != null) MoteMaker.MakeInteractionOverlay(glow, source, destination, sourceOffset, endOffset);
            if (core != null) MoteMaker.MakeInteractionOverlay(core, source, destination, sourceOffset, endOffset);

            float distance = Vector3.Distance(origin, end);
            int points = Mathf.Clamp(Mathf.CeilToInt(distance / 1.25f), 1, 70);
            for (int index = 0; index <= points; index++)
            {
                Vector3 point = Vector3.Lerp(origin, end, index / (float)points);
                FleckCreationData flash = FleckMaker.GetDataStatic(point, map, FleckDefOf.ShotFlash,
                    index % 3 == 0 ? 2.2f : 1.35f);
                flash.instanceColor = new Color(0.25f, 0.85f, 1f, 1f);
                map.flecks.CreateFleck(flash);
            }
            FleckMaker.Static(end, map, FleckDefOf.MicroSparks, 3f);
        }
    }
}
