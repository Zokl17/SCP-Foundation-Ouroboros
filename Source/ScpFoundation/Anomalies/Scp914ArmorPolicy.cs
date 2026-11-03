using System;

namespace ScpFoundation.Anomalies
{
    internal static class Scp914ArmorPolicy
    {
        internal static bool ShouldRepairOnOneToOne(int setting, int hitPoints, int maxHitPoints)
        {
            return setting == 2 && maxHitPoints > 0
                && (long)hitPoints * 5 < (long)maxHitPoints * 3;
        }

        internal static bool TryMeasure(float[] source, float[] candidate, float[] multipliers,
            out int radiusTenths, out float totalError)
        {
            radiusTenths = int.MaxValue;
            totalError = float.PositiveInfinity;
            if (source == null || candidate == null || multipliers == null
                || source.Length != 3 || candidate.Length != 3 || multipliers.Length != 3)
                return false;

            float largestError = 0f;
            float sum = 0f;
            for (int i = 0; i < 3; i++)
            {
                if (source[i] < 0f || candidate[i] < 0f || multipliers[i] <= 0f) return false;
                float error = Math.Abs(candidate[i] - source[i] * multipliers[i])
                    / Math.Max(source[i], 0.05f);
                largestError = Math.Max(largestError, error);
                sum += error;
            }
            radiusTenths = (int)Math.Ceiling(Math.Max(0f, largestError - 0.00001f) * 10f);
            totalError = sum;
            return true;
        }
    }
}
