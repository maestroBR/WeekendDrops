using Comfort.Common;
using EFT;
using UnityEngine;

namespace WeekendDropsUI.Services
{
    public static partial class NativeAudio
    {
        public static bool ShouldSampleLoot()
        {
            var world = Singleton<GameWorld>.Instance;
            return world != null && ShouldSampleLootAt(Time.unscaledTime, world.GetInstanceID());
        }
    }
}
