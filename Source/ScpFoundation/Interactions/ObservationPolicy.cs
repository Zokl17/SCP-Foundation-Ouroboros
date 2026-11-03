namespace ScpFoundation.Interactions
{
    public enum ObservationStatus { Ready, Unavailable, Blind, Moving, Busy, NoDuty, TooFar, Blocked, Dark }

    public static class ObservationPolicy
    {
        public static ObservationStatus Evaluate(bool available, bool sighted, bool moving, bool busy,
            bool assignedDuty, bool draftedWaiting, float distanceSquared, bool lineOfSight, float light)
        {
            if (!available) return ObservationStatus.Unavailable;
            if (!sighted) return ObservationStatus.Blind;
            if (moving) return ObservationStatus.Moving;
            if (busy) return ObservationStatus.Busy;
            if (!assignedDuty && !draftedWaiting) return ObservationStatus.NoDuty;
            if (distanceSquared > 36f) return ObservationStatus.TooFar;
            if (!lineOfSight) return ObservationStatus.Blocked;
            if (light < 0.3f) return ObservationStatus.Dark;
            return ObservationStatus.Ready;
        }
    }
}
