using System;

namespace ScpFoundation.Interactions
{
    public static class SessionWorkPolicy
    {
        public static bool AtPost(bool objectSpawned, bool postSpawned, bool bound, bool sameMap, bool sameCell)
        {
            return objectSpawned && postSpawned && bound && sameMap && sameCell;
        }

        public static float ResearchAmount(bool study, bool conditionsMet, bool moving, float speed, int delta)
        {
            if (!study || !conditionsMet || moving || delta <= 0 || float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0) return 0;
            return speed * delta;
        }

        public static bool UseStudyJob(bool assignedAsResearcher)
        {
            return assignedAsResearcher;
        }

        public static bool ContinuousSession(bool study, int phase)
        {
            return !study || phase >= 2;
        }
    }
}
