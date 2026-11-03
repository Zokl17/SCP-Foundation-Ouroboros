namespace ScpFoundation.Knowledge
{
    public static class DiscoveryPolicy
    {
        public static int Clearance(bool informationUnlocked, int knowledge)
        {
            return informationUnlocked ? System.Math.Max(0, System.Math.Min(3, knowledge)) : 0;
        }
        public static bool ShouldAnnounce(bool alreadyAnnounced, bool spawned, bool visible, bool fixture)
        {
            return !alreadyAnnounced && spawned && visible && !fixture;
        }
    }
}
