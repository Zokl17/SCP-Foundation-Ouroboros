using System;

namespace ScpFoundation.Interactions
{
    public static class EmergencyPowerPolicy
    {
        public const int MaximumReserveTicks = 30000;
        public const float ChargingPowerWatts = 200f;
        public const float StandbyPowerWatts = 50f;

        public static float PowerConsumption(int reserveTicks)
        {
            return reserveTicks >= MaximumReserveTicks ? StandbyPowerWatts : ChargingPowerWatts;
        }

        public static int Update(int reserveTicks, int elapsedTicks, bool gridPower, bool solarFlare)
        {
            reserveTicks = Math.Max(0, Math.Min(MaximumReserveTicks, reserveTicks));
            elapsedTicks = Math.Max(0, elapsedTicks);
            if (solarFlare) return Math.Max(0, reserveTicks - elapsedTicks);
            return gridPower ? Math.Min(MaximumReserveTicks, reserveTicks + elapsedTicks) : reserveTicks;
        }

        public static bool CanBackUp(int reserveTicks, bool solarFlare, bool switchedOn = true, bool connected = true)
        {
            return solarFlare && reserveTicks > 0 && switchedOn && connected;
        }
    }
}
