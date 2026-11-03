using System;

namespace ScpFoundation.Storytelling
{
    public enum FoundationRole { None, MtfCaptain, Scientist }

    public enum FoundationPromotionResult
    {
        Allowed,
        InvalidPawn,
        MaximumLevel,
        StoryOnly,
        InsufficientTrust,
        TooYoung,
        ResearchDisabled,
        InsufficientResearchSkill,
        InsufficientSpecialistSkill,
        InsufficientDirectorSkills
    }

    public static class FoundationAuthorityPolicy
    {
        public const int MaximumEarnedLevel = 4;
        public const int StoryLevel = 5;
        private static readonly int[] PromotionCosts = { 10, 20, 35, 45 };

        public static int PromotionCost(int currentLevel)
        {
            return currentLevel >= 0 && currentLevel < PromotionCosts.Length ? PromotionCosts[currentLevel] : 0;
        }

        public static FoundationPromotionResult CanPromote(int currentLevel, int trust, int biologicalAge,
            int intellectual, int shooting, bool researchDisabled = false)
        {
            if (currentLevel < 0) return FoundationPromotionResult.InvalidPawn;
            if (currentLevel >= StoryLevel) return FoundationPromotionResult.MaximumLevel;
            if (currentLevel >= MaximumEarnedLevel) return FoundationPromotionResult.StoryOnly;
            if (biologicalAge < 18) return FoundationPromotionResult.TooYoung;
            if (currentLevel == 0 && researchDisabled) return FoundationPromotionResult.ResearchDisabled;
            if (currentLevel == 0 && intellectual < 2) return FoundationPromotionResult.InsufficientResearchSkill;
            int nextLevel = currentLevel + 1;
            int specialist = Math.Max(intellectual, shooting);
            if (nextLevel == 2 && specialist < 6 || nextLevel == 3 && specialist < 10)
                return FoundationPromotionResult.InsufficientSpecialistSkill;
            if (nextLevel == 4 && shooting < 13 && intellectual < 14)
                return FoundationPromotionResult.InsufficientDirectorSkills;
            return trust < PromotionCost(currentLevel)
                ? FoundationPromotionResult.InsufficientTrust
                : FoundationPromotionResult.Allowed;
        }

        public static bool QualifiesForLevelFour(FoundationRole role, int intellectual, int shooting)
        {
            return role == FoundationRole.MtfCaptain && shooting >= 13
                || role == FoundationRole.Scientist && intellectual >= 14;
        }
    }
}
