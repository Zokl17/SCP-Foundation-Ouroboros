using System;

namespace ScpFoundation.Interactions
{
    public static class ShiftPolicy
    {
        public const int AllHours = (1 << 24) - 1;
        public const int HandoverTicks = 1250;
        public const int ReservationSlots = 5;
        public static bool Active(int mask, int hour)
        {
            return hour >= 0 && hour < 24 && (mask & (1 << hour)) != 0;
        }
        public static bool Due(int mask, float hour, bool allowEarly)
        {
            if (float.IsNaN(hour) || float.IsInfinity(hour) || hour < 0 || hour >= 24) return false;
            int whole = (int)hour;
            return Active(mask, whole) || allowEarly && hour - whole >= 0.5f && Active(mask, (whole + 1) % 24);
        }
        public static bool HoldForRelief(bool activeNow, bool replacementsReady, bool needsCritical, int now, int deadline)
        {
            return activeNow || !needsCritical && !replacementsReady && now < deadline;
        }
        public static int Range(int start, int end)
        {
            int result = 0;
            for (int hour = start; hour < end; hour++) if (hour >= 0 && hour < 24) result |= 1 << hour;
            return result;
        }
    }
}
