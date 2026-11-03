using System;

namespace ScpFoundation.Knowledge
{
    public static class ResearchModel
    {
        public static float Normalize(float work, float maximum)
        {
            if (float.IsNaN(work)) return 0;
            return Math.Min(maximum, Math.Max(0, work));
        }
        public static float Advance(float work, float amount, float maximum)
        {
            work = Normalize(work, maximum);
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0) return work;
            return Normalize(work + amount, maximum);
        }
        public static int Level(float work, float observed, float partial, float full)
        {
            return work >= full ? 3 : work >= partial ? 2 : work >= observed ? 1 : 0;
        }

        public static float MinimumWorkForLevel(int level, float observed, float partial, float full)
        {
            return level >= 3 ? full : level >= 2 ? partial : level >= 1 ? observed : 0f;
        }

        public static float CurrentStageWork(float work, int level, float observed, float partial, float full)
        {
            if (level >= 3) return 0f;
            float start = level >= 2 ? partial : level >= 1 ? observed : 0f;
            float required = NextStageWork(level, observed, partial, full);
            return Math.Min(required, Math.Max(0f, Normalize(work, full) - start));
        }

        public static float NextStageWork(int level, float observed, float partial, float full)
        {
            return level >= 3 ? 0f : level >= 2 ? full - partial : level >= 1 ? partial - observed : observed;
        }

        public static float StageProgress(float work, int level, float observed, float partial, float full)
        {
            if (level >= 3) return 1f;
            float required = NextStageWork(level, observed, partial, full);
            if (required <= 0f || float.IsNaN(required) || float.IsInfinity(required)) return 0f;
            return Math.Min(1f, Math.Max(0f, CurrentStageWork(work, level, observed, partial, full) / required));
        }

        public static int ExperimentStage(float work, float stageWork)
        {
            if (stageWork <= 0f || float.IsNaN(stageWork) || float.IsInfinity(stageWork)) return 0;
            return Math.Min(3, (int)(Normalize(work, stageWork * 3f) / stageWork));
        }

        public static float ExperimentStageWork(float work, float stageWork)
        {
            int stage = ExperimentStage(work, stageWork);
            return stage >= 3 ? stageWork : Normalize(work, stageWork * 3f) - stage * stageWork;
        }

        public static float ExperimentProgress(float work, float stageWork)
        {
            return stageWork > 0f ? ExperimentStageWork(work, stageWork) / stageWork : 0f;
        }

        public static int ExperimentTrust(string objectClass, int completedStage)
        {
            if (completedStage < 1 || completedStage > 3) return 0;
            if (objectClass == "Safe") return new[] { 3, 2, 1 }[completedStage - 1];
            if (objectClass == "Euclid") return new[] { 5, 3, 1 }[completedStage - 1];
            if (objectClass == "Keter") return new[] { 15, 11, 8 }[completedStage - 1];
            return 0;
        }
    }
}
