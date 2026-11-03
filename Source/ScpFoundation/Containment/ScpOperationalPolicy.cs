using System;

namespace ScpFoundation.Containment
{
    [Flags]
    public enum ScpOperationalFlags
    {
        None = 0,
        AtPostBlack = 1,
        WarningYellow = 2,
        EscapedRed = 4
    }

    // This is the single public state contract used by cameras, work and UI.
    // The detailed ContainmentState still owns risk and timers internally.
    public static class ScpOperationalPolicy
    {
        public static ScpOperationalFlags State(bool atPost, bool warning)
        {
            if (!atPost) return ScpOperationalFlags.EscapedRed;
            return ScpOperationalFlags.AtPostBlack
                | (warning ? ScpOperationalFlags.WarningYellow : ScpOperationalFlags.None);
        }

        public static bool IsAtPost(ScpOperationalFlags state)
        {
            return (state & ScpOperationalFlags.AtPostBlack) != 0
                && (state & ScpOperationalFlags.EscapedRed) == 0;
        }

        public static bool AllowsRemoteObservation(ScpOperationalFlags state)
        {
            return IsAtPost(state);
        }

        public static bool AllowsResearch(ScpOperationalFlags state)
        {
            return state == ScpOperationalFlags.AtPostBlack;
        }

        public static bool NeedsRecovery(ScpOperationalFlags state)
        {
            return state == (ScpOperationalFlags.AtPostBlack | ScpOperationalFlags.WarningYellow);
        }
    }
}
