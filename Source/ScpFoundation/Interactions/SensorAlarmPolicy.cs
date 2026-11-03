namespace ScpFoundation.Interactions
{
    // Values never form part of the state key: changing temperature is not a new alarm.
    public sealed class SensorAlarmPolicy
    {
        public string confirmed, pending;
        public int pendingSince;
        public const int SettleTicks = 500;
        public void Reset() { confirmed = pending = null; pendingSince = 0; }
        public bool Advance(string state, int now)
        {
            if (state == confirmed) { pending = null; return false; }
            if (pending != state || now < pendingSince) { pending = state; pendingSince = now; return false; }
            if (now - pendingSince < SettleTicks) return false;
            bool notify = confirmed != null || state != "ok";
            confirmed = state;
            pending = null;
            return notify;
        }
    }
}
