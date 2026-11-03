using UnityEngine;

namespace ScpFoundation.Runtime
{
    // Pixel offsets are registered to a 640x640 layer centered on the mounting ring.
    internal sealed class ChimeraTurretPose
    {
        internal readonly string Name;
        internal readonly float Heading;
        internal readonly Vector2 Cannon;
        internal readonly Vector2 Rail;
        internal readonly Vector2 ChannelRear;
        internal readonly Vector2 ChannelFront;
        internal readonly Vector2 Port;
        internal readonly float SupportFoot;

        internal ChimeraTurretPose(string name, float heading, Vector2 cannon, Vector2 rail,
            Vector2 channelRear, Vector2 channelFront, Vector2 port, float supportFoot)
        {
            Name = name; Heading = heading; Cannon = cannon; Rail = rail;
            ChannelRear = channelRear; ChannelFront = channelFront; Port = port; SupportFoot = supportFoot;
        }
    }

    internal static class ChimeraTurretArt
    {
        internal static ChimeraTurretPose Pose(float heading)
        {
            ChimeraTurretPose best = ChimeraTurretPoseData.Poses[0];
            float error = 360f;
            foreach (ChimeraTurretPose candidate in ChimeraTurretPoseData.Poses)
            {
                float difference = Mathf.Abs(Mathf.DeltaAngle(heading, candidate.Heading));
                if (difference < error) { best = candidate; error = difference; }
            }
            return best;
        }

        internal static Vector3 Pixel(Vector2 size, Vector2 offset)
        {
            float scale = size.x / 640f;
            return new Vector3(offset.x * scale, 0f, offset.y * scale);
        }

        internal static Vector3 Mount(Vector2 size)
        {
            return ChimeraWeaponGeometry.TurretPivot(size, false);
        }

        internal static Vector3 Muzzle(Vector2 size, float heading, ChimeraGun gun)
        {
            ChimeraTurretPose pose = Pose(heading);
            return Mount(size) + Pixel(size, gun == ChimeraGun.Cannon ? pose.Cannon : pose.Rail);
        }

        internal static Vector3 Channel(Vector2 size, float heading, float progress)
        {
            ChimeraTurretPose pose = Pose(heading);
            return Mount(size) + Pixel(size, Vector2.Lerp(pose.ChannelRear, pose.ChannelFront, Mathf.Clamp01(progress)));
        }

        internal static Vector3 RocketPort(Vector2 size, float heading, bool second)
        {
            ChimeraTurretPose pose = Pose(heading);
            Vector2 direction = (pose.ChannelFront - pose.ChannelRear).normalized;
            Vector2 lateral = new Vector2(direction.y, -direction.x) * (second ? 6f : -6f);
            return Mount(size) + Pixel(size, pose.Port + lateral)
                + new Vector3(0f, 0f, ChimeraWeaponGeometry.LauncherLift(size));
        }

        internal static float AimHeading(Vector2 size, ChimeraGun gun, Vector3 targetOffset)
        {
            Vector3 direction = targetOffset - Mount(size);
            float heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            // Select a perspective-correct view, then aim from that view's actual muzzle.
            direction = targetOffset - Muzzle(size, heading, gun);
            return Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
        }
    }
}
