using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect status effect application
    /// Note: AddStatusEffect has multiple overloads, so we need to find the right one
    /// </summary>
    public static class StatusEffectPatch
    {
        // Track last announced effect to avoid duplicate announcements
        private static string _lastAnnouncedEffect = "";
        private static float _lastAnnouncedTime = 0f;

        // Cached reflection for StatusEffectManager.GetLocalizedName
        private static MethodInfo _getLocalizedNameMethod;
        private static bool _getLocalizedNameSearched;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType != null)
                {
                    // AddStatusEffect has multiple overloads. Patch the one with the most
                    // parameters — the simpler overloads forward to it, but card/relic effects
                    // (CardEffectAddStatusEffect etc.) call the full overload directly, so
                    // patching only the simple one misses every status effect applied by a card.
                    System.Reflection.MethodInfo method = null;

                    var methods = characterType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var m in methods)
                    {
                        if (m.Name == "AddStatusEffect")
                        {
                            var parameters = m.GetParameters();
                            if (method == null || parameters.Length > method.GetParameters().Length)
                            {
                                method = m;
                            }
                        }
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(StatusEffectPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CharacterState.AddStatusEffect (params: {method.GetParameters().Length})");
                    }
                    else
                    {
                        // Expected in some game versions - not a critical patch
                        MonsterTrainAccessibility.LogInfo("AddStatusEffect method not found - status announcements disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping AddStatusEffect patch: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, string statusId, int numStacks)
        {
            try
            {
                if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                    return;

                if (string.IsNullOrEmpty(statusId) || numStacks <= 0)
                    return;

                // Skip if we're in preview mode
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                // Get unit name
                string unitName = GetUnitName(__instance);

                // Create a key to detect duplicate announcements
                string effectKey = $"{unitName}_{statusId}_{numStacks}";
                float currentTime = UnityEngine.Time.unscaledTime;

                // Avoid duplicate announcements within 0.5 seconds
                if (effectKey == _lastAnnouncedEffect && currentTime - _lastAnnouncedTime < 0.5f)
                    return;

                _lastAnnouncedEffect = effectKey;
                _lastAnnouncedTime = currentTime;

                // Get the localized name from the game (e.g., "Valor" instead of "pyreattackpower")
                string effectName = CleanStatusName(statusId, numStacks);

                MonsterTrainAccessibility.BattleHandler?.OnStatusEffectApplied(unitName, effectName, numStacks);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in status effect patch: {ex.Message}");
            }
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetName method first
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetSourceCharacterData (MT2 uses this instead of GetCharacterDataRead)
                var getDataMethod = type.GetMethod("GetSourceCharacterData") ?? type.GetMethod("GetCharacterData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Unit";
        }

        private static string CleanStatusName(string statusId, int numStacks = 1)
        {
            if (string.IsNullOrEmpty(statusId))
                return "effect";

            // Try the game's own StatusEffectManager.GetLocalizedName first
            string localized = GetLocalizedStatusName(statusId, numStacks);
            if (!string.IsNullOrEmpty(localized))
                return localized;

            // Fallback: basic string cleanup
            string name = statusId
                .Replace("_StatusId", "")
                .Replace("StatusId", "")
                .Replace("_", " ");

            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name.ToLower().Trim();
        }

        private static string GetLocalizedStatusName(string statusId, int numStacks)
        {
            try
            {
                if (!_getLocalizedNameSearched)
                {
                    _getLocalizedNameSearched = true;
                    var semType = AccessTools.TypeByName("StatusEffectManager");
                    if (semType != null)
                    {
                        // GetLocalizedName(string statusId, int stackCount, bool inBold, bool showStacks, bool inCardBodyText)
                        _getLocalizedNameMethod = semType.GetMethod("GetLocalizedName",
                            new[] { typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool) });
                        MonsterTrainAccessibility.LogInfo(
                            $"StatusEffectManager.GetLocalizedName found: {_getLocalizedNameMethod != null}");
                    }
                }

                if (_getLocalizedNameMethod != null)
                {
                    // inBold=false, showStacks=false (we announce stacks separately), inCardBodyText=false
                    var result = _getLocalizedNameMethod.Invoke(null,
                        new object[] { statusId, numStacks, false, false, false }) as string;
                    if (!string.IsNullOrEmpty(result))
                    {
                        result = Utilities.TextUtilities.StripRichTextTags(result);
                        result = Utilities.TextUtilities.CleanSpriteTagsForSpeech(result);
                        return result.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetLocalizedStatusName error for '{statusId}': {ex.Message}");
            }
            return null;
        }
    }
}
