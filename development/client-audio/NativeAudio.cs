using System;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using EFT.UI;

namespace WeekendDropsUI.Services
{
    // Compiled into WeekendDropsUI.dll by the local client build, not loaded as a plugin.
    public static partial class NativeAudio
    {
        private static ManualLogSource log;
        private static bool warned;

        public static void Initialize(ManualLogSource logger)
        {
            log = logger;
            logger.LogInfo("[WeekendDrops] Integrated native UI sounds ready.");
        }

        public static string CanonicalName(string name)
        {
            if (name == "QuestTurnedIn") return "QuestFinished";
            if (name == "QuestUpdated" || name == "TaskMarkerFound") return "QuestSubTrackComplete";
            if (name == "InsuranceItemReturnedToStash" || name == "Ready") return "TradeOperationComplete";
            if (name == "TabButton") return "ButtonBottomBarClick";
            if (name == "MenuEscape") return "MenuInspectorWindowClose";
            return name;
        }

        public static void Play(string[] candidates)
        {
            if (candidates == null) return;
            foreach (var candidate in candidates)
            {
                EUISoundType sound;
                if (!Enum.TryParse(CanonicalName(candidate), true, out sound) ||
                    !Enum.IsDefined(typeof(EUISoundType), sound)) continue;
                Emit(sound);
                return;
            }
        }

        private static void Emit(EUISoundType sound)
        {
            if (!Singleton<GUISounds>.Instantiated) return;
            try { Singleton<GUISounds>.Instance.PlayUISound(sound); }
            catch (Exception error)
            {
                if (!warned && log != null)
                {
                    warned = true;
                    log.LogWarning("[WeekendDrops] Native UI sound unavailable: " + error.GetType().Name);
                }
            }
        }

        public static void Open() { Emit(EUISoundType.MenuInspectorWindowOpen); }
        public static void Click(string[] unused) { Emit(EUISoundType.ButtonClick); }

        // These are called from the existing Unity coroutines after the server replies.
        public static void RewardResult(bool accepted)
        {
            Emit(accepted ? EUISoundType.QuestFinished : EUISoundType.ErrorMessage);
        }

        public static void RewardTask(Task<bool> task)
        {
            if (task != null && task.Status == TaskStatus.RanToCompletion) RewardResult(task.Result);
        }

        public static void CardResult(string result, bool reveal)
        {
            if (result != "ok") { Emit(EUISoundType.ErrorMessage); return; }
            Emit(reveal ? EUISoundType.MenuDropdownSelect : EUISoundType.TradeOperationComplete);
        }
    }
}
