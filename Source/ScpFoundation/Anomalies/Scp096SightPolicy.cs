namespace ScpFoundation.Anomalies
{
    // Direction and visibility are kept separate from Verse for deterministic tests.
    public static class Scp096SightPolicy
    {
        public static bool SeesFace(int dx, int dz, int viewerX, int viewerZ,
            int entityX, int entityZ, bool awakeAndSighted, bool lineOfSight, float light)
        {
            if (!awakeAndSighted || !lineOfSight || light < 0.15f) return false;
            int distanceSquared = dx * dx + dz * dz;
            if (distanceSquared == 0 || distanceSquared > 400) return false;
            int viewerForward = dx * viewerX + dz * viewerZ;
            if (viewerForward <= 0 || viewerForward * viewerForward * 2 < distanceSquared) return false;
            int faceForward = -dx * entityX - dz * entityZ;
            return faceForward > 0 && faceForward * faceForward * 2 >= distanceSquared;
        }
    }
}
