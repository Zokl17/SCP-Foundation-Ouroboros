namespace ScpFoundation.Containment
{
    public static class DoorClosurePolicy
    {
        public static bool Violates(bool requireClosed, bool open, bool forcedOpen, int openTicks, int graceTicks)
        {
            if (!requireClosed || !open) return false;
            if (forcedOpen) return true;
            if (openTicks < 0) openTicks = 0;
            if (graceTicks < 0) graceTicks = 0;
            return openTicks >= graceTicks;
        }
    }
}
