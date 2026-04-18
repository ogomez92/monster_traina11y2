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

        // CharacterState.ApplyHeal(int amount, bool triggerOnHeal = true, CardState responsibleCard = null,
        //                          RelicState relicState = null, bool fromMaxHPChange = false, ...)
        // __instance = target CharacterState, __0..__4 = the visible args we care about.
        public static void Prefix(object __instance, int __0, bool __1, object __2, object __3, bool __4)
        {
            try
            {
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                int amount = __0;
                if (amount <= 0 || __instance == null)
                    return;

                // Heals caused by max HP increases are already announced by MaxHPBuffPatch;
                // skip them here to avoid the "gains N max health" + "healed N health" combo.
                if (__4) return;

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
                string sourceName = GetHealSourceName(__2, __3);

                // Deduplicate rapid heals on same unit
                float currentTime = UnityEngine.Time.unscaledTime;
                string healKey = $"{targetName}_{amount}_{sourceName}";
                if (healKey == _lastHealKey && currentTime - _lastHealTime < 0.3f)
                    return;

                _lastHealKey = healKey;
                _lastHealTime = currentTime;

                string message = string.IsNullOrEmpty(sourceName)
                    ? ModLocalization.Phrase("Healed", targetName, amount)
                    : ModLocalization.Phrase("HealedBy", targetName, amount, sourceName);
                MonsterTrainAccessibility.ScreenReader?.Speak(message);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in heal patch: {ex.Message}");
            }
        }

        private static string GetHealSourceName(object responsibleCard, object relicState)
        {
            try
            {
                if (responsibleCard != null)
                {
                    var getTitle = responsibleCard.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                    if (getTitle != null)
                    {
                        var result = getTitle.Invoke(responsibleCard, null) as string;
                        if (!string.IsNullOrEmpty(result))
                            return Utilities.TextUtilities.StripRichTextTags(result).Trim();
                    }
                }
                if (relicState != null)
                {
                    var getName = relicState.GetType().GetMethod("GetName", Type.EmptyTypes);
                    if (getName != null)
                    {
                        var result = getName.Invoke(relicState, null) as string;
                        if (!string.IsNullOrEmpty(result))
                            return Utilities.TextUtilities.StripRichTextTags(result).Trim();
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
