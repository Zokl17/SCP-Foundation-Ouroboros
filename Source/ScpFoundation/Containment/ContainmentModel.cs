using System;

namespace ScpFoundation.Containment
{
    public enum ContainmentStatus { Unassessed, Contained, Grace, AtRisk, Breached }

    public static class ContainmentProtection
    {
        public static bool AllowsEscape(bool inCustody, bool preparationActive, bool breached)
        {
            return !preparationActive && (!inCustody || breached);
        }
    }

    public static class CaptureGracePeriod
    {
        public static int DurationTicks(string objectClass)
        {
            return string.Equals(objectClass, "Safe", StringComparison.OrdinalIgnoreCase) ? 30000 : 15000;
        }

        public static int Deadline(int now, string objectClass)
        {
            return (int)Math.Min(int.MaxValue, (long)now + DurationTicks(objectClass));
        }

        public static int ExposedTicks(int now, int delta, int deadline)
        {
            return Math.Min(Math.Max(0, delta), Math.Max(0, now - deadline));
        }
    }

    public static class ContainmentWarningCooldown
    {
        public const int Ticks = 7500; // Three hours.

        public static bool CanSend(int now, int lastLetterTick)
        {
            return (long)now - lastLetterTick >= Ticks;
        }
    }

    // Pure model: no game services, random generator, or UI dependencies.
    public sealed class BreachPolicy
    {
        public int graceTicks = 2500;
        public int deterministicAfterTicks = 10000;
        public float riskPerDay = 6f;
        public float recoveryPerDay = 12f;
        public float chancePerDayAtFullRisk = 0f;
        public float severity = 1f;
    }

    public sealed class ContainmentState
    {
        public ContainmentStatus status;
        public int violationTicks;
        public float risk;
        public int breachCount;
        public bool warningSent;

        // A warning starts a fresh response window, including old saves without this flag.
        public bool BeginWarning(bool violated)
        {
            if (!violated) { warningSent = false; return false; }
            if (status == ContainmentStatus.Breached || warningSent) return false;
            warningSent = true;
            violationTicks = 0;
            risk = 0f;
            return true;
        }

        // Delta is the observed interval, never time spent off-map.
        // Returns true only on the transition into a new breach.
        public bool Advance(BreachPolicy policy, bool violated, int delta, Func<double> roll)
        {
            if (status == ContainmentStatus.Breached) return false;
            delta = Math.Max(0, delta);
            if (!violated)
            {
                violationTicks = 0;
                risk = Math.Max(0f, risk - policy.recoveryPerDay * delta / 60000f);
                status = ContainmentStatus.Contained;
                return false;
            }

            int previous = violationTicks;
            violationTicks = (int)Math.Min(int.MaxValue, (long)violationTicks + delta);
            int exposedTicks = Math.Max(0, violationTicks - policy.graceTicks)
                - Math.Max(0, previous - policy.graceTicks);
            bool deterministic = policy.deterministicAfterTicks > 0
                && violationTicks >= Math.Max(policy.graceTicks, policy.deterministicAfterTicks);
            if (violationTicks <= policy.graceTicks && !deterministic)
            {
                status = ContainmentStatus.Grace;
                return false;
            }

            status = ContainmentStatus.AtRisk;
            risk = Math.Min(1f, risk + policy.riskPerDay * policy.severity * exposedTicks / 60000f);
            double chance = 1.0 - Math.Pow(1.0 - policy.chancePerDayAtFullRisk,
                risk * exposedTicks / 60000.0);
            if (!deterministic && !(chance > 0 && roll() < chance)) return false;
            return ForceBreach();
        }

        public bool ForceBreach()
        {
            if (status == ContainmentStatus.Breached) return false;
            status = ContainmentStatus.Breached;
            breachCount++;
            return true;
        }

        public void Recover()
        {
            warningSent = false;
            status = ContainmentStatus.Contained;
            violationTicks = 0;
            risk = 0f;
        }
    }
}
