using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when the entire hand is discarded (end of turn).
    /// Hooks CardManager.DiscardHand() (IEnumerator, so prefix).
    /// </summary>
    public static class DiscardHandPatch
    {
        private static float _lastTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardMgrType = AccessTools.TypeByName("CardManager");
                if (cardMgrType == null) return;

                var method = AccessTools.Method(cardMgrType, "DiscardHand");
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(DiscardHandPatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CardManager.DiscardHand");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CardManager.DiscardHand not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping DiscardHand patch: {ex.Message}");
            }
        }

        public static void Prefix()
        {
            try
            {
                float now = UnityEngine.Time.unscaledTime;
                if (now - _lastTime < 0.5f) return;
                _lastTime = now;

                MonsterTrainAccessibility.ScreenReader?.Queue("Hand discarded");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DiscardHand patch: {ex.Message}");
            }
        }
    }
}
