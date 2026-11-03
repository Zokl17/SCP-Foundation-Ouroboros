namespace ScpFoundation.Storytelling
{
    public static class Scp079LinkPolicy
    {
        public const int FirstCargoTick = 10000;
        public const int SecondCargoTick = 45000;
        public const int QuarantineEndTick = 60000;

        public static int DueCargo(int now, int linkedAt, int deliveredMask)
        {
            if (linkedAt < 0 || now < linkedAt) return 0;
            int elapsed = now - linkedAt;
            int due = (elapsed >= FirstCargoTick ? 1 : 0)
                | (elapsed >= SecondCargoTick ? 2 : 0);
            return due & ~deliveredMask;
        }
    }
}
