namespace ScpFoundation.Anomalies
{
    public sealed class AntimemeticMemoryProgress
    {
        public const int ForgetDelayTicks = 50000;
        public int lastStudyTick = -1;

        public bool Update(int now, bool studying)
        {
            if (now < 0) return false;
            if (studying)
            {
                lastStudyTick = now;
                return false;
            }
            if (lastStudyTick < 0 || (long)now - lastStudyTick < ForgetDelayTicks) return false;
            lastStudyTick = -1;
            return true;
        }
    }
}
