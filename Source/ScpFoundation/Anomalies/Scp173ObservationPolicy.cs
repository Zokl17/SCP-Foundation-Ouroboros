namespace ScpFoundation.Anomalies
{
    // Kept independent of Verse so facing, distance and blink timing can be tested directly.
    public static class Scp173ObservationPolicy
    {
        public static bool IsWatching(int dx, int dz, int facingX, int facingZ,
            bool awakeAndSighted, bool lineOfSight, float light, int tick, int observerId)
        {
            if (!awakeAndSighted || !lineOfSight || light < 0.15f) return false;
            int distanceSquared = dx * dx + dz * dz;
            if (distanceSquared == 0 || distanceSquared > 400) return false;
            int forward = dx * facingX + dz * facingZ;
            if (forward <= 0 || forward * forward * 2 < distanceSquared) return false;
            // Stagger blinks so two watchers rarely close their eyes together.
            int phase = (int)(((long)tick + (long)observerId * 73) % 360);
            return phase >= 18;
        }
    }
}
