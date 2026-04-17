using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a specific card is drawn (e.g., from effects that draw
    /// a particular card rather than random draws from the deck).
    /// Hooks CardManager.DrawSpecificCard(CardState, ...).
    /// </summary>
    public static class DrawSpecificCardPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardMgrType = AccessTools.TypeByName("CardManager");
                if (cardMgrType == null) return;

                var method = AccessTools.Method(cardMgrType, "DrawSpecificCard");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DrawSpecificCardPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CardManager.DrawSpecificCard");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("DrawSpecificCard not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping DrawSpecificCard patch: {ex.Message}");
            }
        }

        // __0 = CardState drawCard, __result = bool success
        public static void Postfix(object __0, bool __result)
        {
            try
            {
                if (!__result || __0 == null) return;

                if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                    return;

                string cardName = CharacterStateHelper.GetCardName(__0);

                string key = $"{cardName}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.3f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew {cardName}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DrawSpecificCard patch: {ex.Message}");
            }
        }
    }
}
