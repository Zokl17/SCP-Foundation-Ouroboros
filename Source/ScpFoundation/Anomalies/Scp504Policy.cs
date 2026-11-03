namespace ScpFoundation.Anomalies
{
    // The social outcome is deliberately narrow: ordinary conversation cannot spend a tomato.
    public static class Scp504Policy
    {
        public const int HearingRadius = 8;
        public const int FruitCapacity = 5;
        public const int GrowthTicksPerFruit = 60000;

        public static int Grow(int ripe, ref int progress, int elapsed, bool atPost)
        {
            ripe = System.Math.Max(0, System.Math.Min(FruitCapacity, ripe));
            progress = System.Math.Max(0, System.Math.Min(GrowthTicksPerFruit - 1, progress));
            if (!atPost || elapsed <= 0 || ripe == FruitCapacity) return ripe;
            progress += elapsed;
            while (progress >= GrowthTicksPerFruit && ripe < FruitCapacity)
            {
                progress -= GrowthTicksPerFruit;
                ripe++;
            }
            if (ripe == FruitCapacity) progress = 0;
            return ripe;
        }

        public static bool Provokes(string interaction)
        {
            return interaction == "Insult" || interaction == "Slight" || interaction == "SCP_504BadJoke";
        }

        public static string TestInteraction(float roll)
        {
            if (roll < 0.20f) return "SCP_504BadJoke";
            return roll < 0.55f ? "KindWords" : "Chitchat";
        }

        public static float SafeBluntDamage(float partHealth)
        {
            return partHealth > 1f ? System.Math.Min(8f, partHealth - 1f) : 0f;
        }
    }
}
