namespace ScpFoundation.Storytelling
{
    public static class Scp650ReturnPolicy
    {
        public const int TrustReward = 10;

        public static bool CanOffer(bool firstAssignmentComplete, bool documented,
            bool experimentsComplete, bool returned)
        {
            return firstAssignmentComplete && documented && experimentsComplete && !returned;
        }

        public static bool CanReward(bool accepted, bool launchedSatisfied, bool correctCargo, bool returned)
        {
            return accepted && launchedSatisfied && correctCargo && !returned;
        }
    }
}
