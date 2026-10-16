namespace WeekendDropsUI.Services
{
    public static partial class NativeAudio
    {
        private static bool lootClockInitialized;
        private static int lootRaidId;
        private static float nextLootSample;
        private static float lastLootClock;

        // Bounded live display sampling; extraction always takes a separate uncached sample.
        public static bool ShouldSampleLootAt(float now, int raidId)
        {
            if (float.IsNaN(now) || float.IsInfinity(now)) return false;
            if (!lootClockInitialized || raidId != lootRaidId || now < lastLootClock)
            {
                lootClockInitialized = true;
                lootRaidId = raidId;
                nextLootSample = now;
            }
            lastLootClock = now;
            if (now < nextLootSample) return false;
            nextLootSample = now + 0.5f;
            return true;
        }
    }
}
