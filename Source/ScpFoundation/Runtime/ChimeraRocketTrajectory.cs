using UnityEngine;

namespace ScpFoundation.Runtime
{
    // Cubic Bezier flight, independent of UF assemblies and Defs.
    internal static class ChimeraRocketTrajectory
    {
        internal static Vector3 Point(Vector3 start, Vector3 first, Vector3 second, Vector3 end, float progress)
        {
            float t = Mathf.Clamp01(progress);
            float u = 1f - t;
            return u * u * u * start + 3f * u * u * t * first
                + 3f * u * t * t * second + t * t * t * end;
        }

        internal static Vector3 Tangent(Vector3 start, Vector3 first, Vector3 second, Vector3 end, float progress)
        {
            float t = Mathf.Clamp01(progress);
            float u = 1f - t;
            return 3f * u * u * (first - start) + 6f * u * t * (second - first)
                + 3f * t * t * (end - second);
        }

        internal static float Length(Vector3 start, Vector3 first, Vector3 second, Vector3 end)
        {
            float length = 0f;
            Vector3 previous = start;
            for (int i = 1; i <= 40; i++)
            {
                Vector3 point = Point(start, first, second, end, i / 40f);
                length += Vector3.Distance(previous, point);
                previous = point;
            }
            return length;
        }
    }
}
