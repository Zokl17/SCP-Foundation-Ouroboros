using System;

namespace ScpFoundation.Storytelling
{
    public enum FoundationAssignmentStage { Available, Active, Ready, Completed }

    public sealed class FoundationAssignmentProgress
    {
        public const int RequiredTicks = 60000;
        public FoundationAssignmentStage stage;
        public int stableTicks;
        public int lastTick = -1;
        public bool lastQualified;

        public bool Accept(bool contact, bool online)
        {
            if (stage != FoundationAssignmentStage.Available) return false;
            return Assign(contact, online, true);
        }
        public void ResetObservation()
        {
            stableTicks = 0; lastTick = -1; lastQualified = false;
        }
        public bool Assign(bool contact, bool online, bool eligible)
        {
            if (!contact || !online || !eligible || stage == FoundationAssignmentStage.Ready
                || stage == FoundationAssignmentStage.Completed) return false;
            stage = FoundationAssignmentStage.Active;
            ResetObservation();
            return true;
        }

        public bool Advance(int now, bool qualified)
        {
            if (stage != FoundationAssignmentStage.Active) return false;
            long elapsed = (long)now - lastTick;
            if (elapsed > 0 && elapsed <= 250 && lastTick >= 0 && lastQualified && qualified)
                stableTicks = Math.Min(RequiredTicks, stableTicks + (int)elapsed);
            lastTick = now; lastQualified = qualified;
            if (stableTicks < RequiredTicks) return false;
            stage = FoundationAssignmentStage.Ready;
            return true;
        }

        public bool Complete(bool online, bool rewardPlaced)
        {
            if (stage != FoundationAssignmentStage.Ready || !online || !rewardPlaced) return false;
            stage = FoundationAssignmentStage.Completed;
            return true;
        }
    }
}
