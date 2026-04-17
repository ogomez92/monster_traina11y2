using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when the card draw count modifier changes.
    /// Hooks CardManager.AdjustDrawCountModifier(int amount, CardState, Action).
    /// </summary>
    public static class DrawCountModifiedPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardMgrType = AccessTools.TypeByName("CardManager");
                if (cardMgrType == null) return;

                var method = AccessTools.Method(cardMgrType, "AdjustDrawCountModifier");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DrawCountModifiedPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CardManager.AdjustDrawCountModifier");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("AdjustDrawCountModifier not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping draw count modifier patch: {ex.Message}");
            }
        }

        public static void Postfix(int __0)
        {
            try
            {
                if (__0 == 0) return;

                string key = $"draw_{__0}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.3f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnDrawCountModified(__0);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in draw count modifier patch: {ex.Message}");
            }
        }
    }
}
