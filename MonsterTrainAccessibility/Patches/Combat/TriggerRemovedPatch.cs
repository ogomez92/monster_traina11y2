using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Announce when a trigger ability is removed from a unit.
    /// Hooks CharacterState.RemoveTrigger(CharacterTriggerData, string).
    /// This is a coroutine so we use a prefix.
    /// </summary>
    public static class TriggerRemovedPatch
    {
        private static string _lastAnnounced = "";
        private static float _lastAnnouncedTime = 0f;

        private static MethodInfo _getTriggerMethod;
        private static bool _getTriggerSearched;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "RemoveTrigger");
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(TriggerRemovedPatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.RemoveTrigger");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CharacterState.RemoveTrigger not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping RemoveTrigger patch: {ex.Message}");
            }
        }

        // __instance = CharacterState, __0 = CharacterTriggerData
        public static void Prefix(object __instance, object __0)
        {
            try
            {
                if (__0 == null) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                string triggerName = GetTriggerName(__0);

                if (string.IsNullOrEmpty(triggerName)) return;

                string key = $"{unitName}_{triggerName}";
                float now = UnityEngine.Time.unscaledTime;
                if (key == _lastAnnounced && now - _lastAnnouncedTime < 0.5f) return;
                _lastAnnounced = key;
                _lastAnnouncedTime = now;

                MonsterTrainAccessibility.BattleHandler?.OnTriggerRemoved(unitName, triggerName);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RemoveTrigger patch: {ex.Message}");
            }
        }

        private static string GetTriggerName(object triggerData)
        {
            try
            {
                if (!_getTriggerSearched)
                {
                    _getTriggerSearched = true;
                    var triggerDataType = triggerData.GetType();
                    _getTriggerMethod = triggerDataType.GetMethod("GetTrigger", Type.EmptyTypes);
                }

                if (_getTriggerMethod != null)
                {
                    var triggerEnum = _getTriggerMethod.Invoke(triggerData, null);
                    if (triggerEnum != null)
                    {
                        string enumName = triggerEnum.ToString();

                        // Try the game's localization key first
                        string localized = Utilities.LocalizationHelper.Localize(
                            $"Trigger_{enumName}_CharacterTriggerData_CardText");
                        if (!string.IsNullOrEmpty(localized))
                            return Utilities.TextUtilities.StripRichTextTags(localized).Trim();

                        // Fallback: clean up enum name
                        string name = System.Text.RegularExpressions.Regex.Replace(enumName, "([a-z])([A-Z])", "$1 $2");
                        if (name.StartsWith("On "))
                            name = name.Substring(3);
                        return name;
                    }
                }
            }
            catch { }
            return "ability";
        }
    }
}
