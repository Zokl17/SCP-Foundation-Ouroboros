namespace ScpFoundation.Interactions
{
    public static class SurveillancePolicy
    {
        public const int Range = 12;
        public static bool InView(int dx, int dz, int facingX, int facingZ)
        {
            int forward = dx * facingX + dz * facingZ;
            int sideways = dx * facingZ - dz * facingX;
            return dx * dx + dz * dz <= Range * Range && forward > 0
                && System.Math.Abs(sideways) <= forward;
        }
        public static bool CameraCovers(int dx, int dz, bool sameRoom)
        {
            return sameRoom && dx * dx + dz * dz <= Range * Range;
        }
        public static bool FeedReady(bool permitted, bool atPost, bool consolePowered, bool operatorAtConsole, bool cameraSeesPost, bool withinCapacity)
        {
            return permitted && atPost && consolePowered && operatorAtConsole && cameraSeesPost && withinCapacity;
        }
    }
}
