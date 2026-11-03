using System.Collections.Generic;

namespace ScpFoundation.Containment
{
    public static class BriefingPolicy
    {
        public static bool ShouldNotify(bool sent, int previousLevel, int currentLevel, IEnumerable<int> requirementLevels)
        {
            if (!sent) return true;
            foreach (int level in requirementLevels)
                if (level > previousLevel && level <= currentLevel) return true;
            return false;
        }
    }
}
