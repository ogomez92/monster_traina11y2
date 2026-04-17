using HarmonyLib;
using System;
using MonsterTrainAccessibility.Patches;
using MonsterTrainAccessibility.Utilities;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect healing via CharacterState.ApplyHeal.
    /// Uses prefix to capture the heal amount before it's applied.
    /// </summary>
    public static class HealAppliedPatch
    {
        private static float _lastHealTime = 0f;
        private static string _lastHealKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var charStateType = AccessTools.TypeByName("CharacterState");
                if (charStateType != null)
                {
                    var method = AccessTools.Method(charStateType, "ApplyHeal");
                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(HealAppliedPatch).GetMethod(nameof(Prefix)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo("Patched CharacterState.ApplyHeal");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ApplyHeal: {ex.Message}");
            }
        }

        // __instance is the CharacterState being healed, __0 is the heal amount
        public static void Prefix(object __instance, int __0)
        {
            try
            {
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                int amount = __0;
                if (amount <= 0 || __instance == null)
                    return;

                // Check if unit is alive before announcing heal
                var charType = __instance.GetType();
                var isAliveProperty = charType.GetProperty("IsAlive");
                if (isAliveProperty != null)
                {
                    var alive = isAliveProperty.GetValue(__instance);
                    if (alive is bool b && !b)
                        return;
                }

                string targetName = CharacterStateHelper.GetUnitName(__instance);

                // Deduplicate rapid heals on same unit
                float currentTime = UnityEngine.Time.unscaledTime;
                string healKey = $"{targetName}_{amount}";
                if (healKey == _lastHealKey && currentTime - _lastHealTime < 0.3f)
                    return;

                _lastHealKey = healKey;
                _lastHealTime = currentTime;

                MonsterTrainAccessibility.ScreenReader?.Speak(ModLocalization.Phrase("Healed", targetName, amount));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in heal patch: {ex.Message}");
            }
        }
    }
}
