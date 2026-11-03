namespace ScpFoundation.Runtime
{
    public static class ShutterPolicy
    {
        public static bool Locked(bool automatic, bool powered, bool breachActive, bool manualOpen)
        {
            if (!powered) return true;
            return automatic ? breachActive : !manualOpen;
        }
    }
}
