namespace ScpFoundation.Storytelling
{
    public sealed class ScpAnnualQuestProgress
    {
        public const int TicksPerDay = 60000;
        public const int TicksPerYear = 60 * TicksPerDay;
        public const int InitialDelayTicks = 75 * TicksPerDay;
        public int accepted650Tick = -1;
        public long nextQuestTick = -1;
        public long nextAttemptTick;
        public int issuedQuests;

        public void Initialize(int acceptedTick)
        {
            if (accepted650Tick < 0 && acceptedTick >= 0) accepted650Tick = acceptedTick;
            if (accepted650Tick < 0) return;
            long firstQuestTick = (long)accepted650Tick + InitialDelayTicks;
            if (nextQuestTick < 0) nextQuestTick = firstQuestTick;
            else if (issuedQuests == 0 && nextQuestTick > firstQuestTick)
            {
                // Shorten the old 120-day first deadline in existing saves only.
                nextQuestTick = firstQuestTick;
                nextAttemptTick = 0;
            }
        }

        public bool Due(long now)
        {
            return accepted650Tick >= 0 && nextQuestTick >= 0 && now >= nextQuestTick && now >= nextAttemptTick;
        }

        public bool BeginAttempt(long now)
        {
            if (!Due(now)) return false;
            // Blocked or failed generation retains the due assignment, with a daily retry.
            nextAttemptTick = now + TicksPerDay;
            return true;
        }

        public bool Offered(long now)
        {
            if (accepted650Tick < 0 || nextQuestTick < 0 || now < nextQuestTick) return false;
            // A late first offer never causes a burst of backdated quests.
            nextQuestTick = now + TicksPerYear;
            nextAttemptTick = nextQuestTick;
            issuedQuests++;
            return true;
        }
    }
}
