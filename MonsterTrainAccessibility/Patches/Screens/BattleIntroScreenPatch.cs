using HarmonyLib;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Detect battle intro screen (pre-battle, showing enemy info and Fight button)
    /// </summary>
    public static class BattleIntroScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("BattleIntroScreen");
                if (targetType != null)
                {
                    // Try Initialize or Setup method
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(BattleIntroScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched BattleIntroScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("BattleIntroScreen methods not found - will use alternative detection");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch BattleIntroScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.BattleIntro);
                MonsterTrainAccessibility.LogInfo("Battle intro screen entered");

                // Auto-read the battle intro content
                AutoReadBattleIntro(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in BattleIntroScreen patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically read the battle intro screen content
        /// </summary>
        private static void AutoReadBattleIntro(object battleIntroScreen)
        {
            try
            {
                if (battleIntroScreen == null) return;

                var screenType = battleIntroScreen.GetType();
                var sb = new StringBuilder();

                // Get battle/boss name from SaveManager's scenario data (more reliable than UI label)
                string battleName = GetBossNameFromScreen(battleIntroScreen, screenType);

                // Get battle description
                string battleDescription = null;
                var descField = screenType.GetField("battleDescriptionLabel", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (descField != null)
                {
                    var descLabel = descField.GetValue(battleIntroScreen);
                    if (descLabel != null)
                    {
                        // MultilineTextFitter might have a text property or GetText method
                        var textProp = descLabel.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            battleDescription = textProp.GetValue(descLabel) as string;
                        }
                        else
                        {
                            // Try GetText method
                            var getTextMethod = descLabel.GetType().GetMethod("GetText");
                            if (getTextMethod != null)
                            {
                                battleDescription = getTextMethod.Invoke(descLabel, null) as string;
                            }
                        }
                    }
                }

                // Build the announcement
                sb.Append("Battle intro. ");
                if (!string.IsNullOrEmpty(battleName))
                {
                    sb.Append("Boss: ");
                    sb.Append(battleName);
                    sb.Append(". ");
                }
                if (!string.IsNullOrEmpty(battleDescription))
                {
                    sb.Append(TextUtilities.StripRichTextTags(battleDescription));
                    sb.Append(" ");
                }

                // Check for trial
                var trialDataField = screenType.GetField("trialData", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                object trialData = trialDataField?.GetValue(battleIntroScreen);

                if (trialData != null)
                {
                    var trialEnabledField = screenType.GetField("trialEnabled", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    bool trialEnabled = false;
                    if (trialEnabledField != null)
                    {
                        var val = trialEnabledField.GetValue(battleIntroScreen);
                        if (val is bool b) trialEnabled = b;
                    }

                    var trialType = trialData.GetType();
                    string ruleName = null;
                    string ruleDescription = null;
                    string rewardName = null;

                    // The rule comes from the 'sin' field (SinsData), which is a RelicData subclass
                    var sinField = trialType.GetField("sin", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (sinField != null)
                    {
                        var sinData = sinField.GetValue(trialData);
                        if (sinData != null)
                        {
                            var sinType = sinData.GetType();

                            // Get the rule name from sin
                            var getNameMethod = sinType.GetMethod("GetName");
                            if (getNameMethod != null)
                            {
                                ruleName = getNameMethod.Invoke(sinData, null) as string;
                            }

                            // Get the rule description - try GetDescriptionKey() and localize
                            var getDescKeyMethod = sinType.GetMethod("GetDescriptionKey");
                            if (getDescKeyMethod != null && getDescKeyMethod.GetParameters().Length == 0)
                            {
                                var descKey = getDescKeyMethod.Invoke(sinData, null) as string;
                                if (!string.IsNullOrEmpty(descKey))
                                {
                                    ruleDescription = TryLocalizeKey(descKey);

                                    // Resolve placeholders like {[effect0.status0.power]}
                                    if (!string.IsNullOrEmpty(ruleDescription) && ruleDescription.Contains("{["))
                                    {
                                        ruleDescription = ResolveEffectPlaceholders(ruleDescription, sinData, sinType);
                                    }
                                }
                            }
                        }
                    }

                    // Get reward from trial data
                    var rewardField = trialType.GetField("reward", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (rewardField != null)
                    {
                        var rewardData = rewardField.GetValue(trialData);
                        if (rewardData != null)
                        {
                            rewardName = GetRewardName(rewardData);
                        }
                    }

                    // Build a clear, descriptive trial announcement
                    sb.Append("Trial available! ");

                    if (trialEnabled)
                    {
                        sb.Append("Trial is ON. ");
                        if (!string.IsNullOrEmpty(ruleName))
                        {
                            sb.Append("Additional rule: ");
                            sb.Append(ruleName);
                            sb.Append(". ");
                        }
                        if (!string.IsNullOrEmpty(ruleDescription))
                        {
                            sb.Append(TextUtilities.StripRichTextTags(ruleDescription));
                            sb.Append(" ");
                        }
                        if (!string.IsNullOrEmpty(rewardName))
                        {
                            sb.Append("You will gain an additional reward: ");
                            sb.Append(rewardName);
                            sb.Append(". ");
                        }
                    }
                    else
                    {
                        sb.Append("Trial is OFF. ");
                        if (!string.IsNullOrEmpty(ruleName))
                        {
                            sb.Append("If enabled, additional rule: ");
                            sb.Append(ruleName);
                            sb.Append(". ");
                        }
                        if (!string.IsNullOrEmpty(ruleDescription))
                        {
                            sb.Append(TextUtilities.StripRichTextTags(ruleDescription));
                            sb.Append(" ");
                        }
                        if (!string.IsNullOrEmpty(rewardName))
                        {
                            sb.Append("Enable trial to gain additional reward: ");
                            sb.Append(rewardName);
                            sb.Append(". ");
                        }
                    }

                    sb.Append("Press F to toggle trial. ");
                }

                sb.Append("Press Enter to fight. Press F1 for help.");

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading battle intro: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the boss/battle name from the screen's SaveManager scenario data
        /// </summary>
        private static string GetBossNameFromScreen(object screen, Type screenType)
        {
            try
            {
                // Try to get SaveManager from the screen
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        var saveManagerType = saveManager.GetType();

                        // Try GetCurrentScenarioData method
                        var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                        if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                        {
                            var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                            if (scenarioData != null)
                            {
                                string name = GetBattleNameFromScenario(scenarioData);
                                if (!string.IsNullOrEmpty(name))
                                    return name;
                            }
                        }

                        // Try GetScenario method
                        var getScenarioMethod = saveManagerType.GetMethod("GetScenario", BindingFlags.Public | BindingFlags.Instance);
                        if (getScenarioMethod != null && getScenarioMethod.GetParameters().Length == 0)
                        {
                            var scenarioData = getScenarioMethod.Invoke(saveManager, null);
                            if (scenarioData != null)
                            {
                                string name = GetBattleNameFromScenario(scenarioData);
                                if (!string.IsNullOrEmpty(name))
                                    return name;
                            }
                        }
                    }
                }

                // Fallback: try to read from the UI label
                var battleNameField = screenType.GetField("battleNameLabel", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (battleNameField != null)
                {
                    var label = battleNameField.GetValue(screen);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            return textProp.GetValue(label) as string;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss name from screen: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the battle name from a ScenarioData object
        /// </summary>
        private static string GetBattleNameFromScenario(object scenarioData)
        {
            if (scenarioData == null) return null;

            try
            {
                var dataType = scenarioData.GetType();

                // Try GetBattleName method first
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try battleNameKey field with localization
                string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = TryLocalizeKey(key);
                            if (!string.IsNullOrEmpty(localized) && localized != key)
                                return localized;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle name from scenario: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract the name from a RewardData object
        /// </summary>
        private static string GetRewardName(object rewardData)
        {
            if (rewardData == null) return null;

            try
            {
                var rewardType = rewardData.GetType();

                // Try GetTitle method first (if it exists)
                var getTitleMethod = rewardType.GetMethod("GetTitle");
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(rewardData, null) as string;
                    if (!string.IsNullOrEmpty(title) && !title.Contains("_") && !title.Contains("-"))
                        return title;
                }

                // Try to get the title key and localize it using the game's Localize method
                var titleKeyField = rewardType.GetField("_rewardTitleKey", BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleKeyField != null)
                {
                    var titleKey = titleKeyField.GetValue(rewardData) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                    {
                        // Try to find and use the Localize extension method
                        string localized = TryLocalizeKey(titleKey);
                        if (!string.IsNullOrEmpty(localized) && localized != titleKey && !localized.Contains("-"))
                            return localized;
                    }
                }

                // Fall back to type name - this is the most reliable approach
                return GetRewardTypeDisplayName(rewardType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting reward name: {ex.Message}");
                return "Reward";
            }
        }

        private static MethodInfo _localizeMethod = null;
        private static bool _localizeMethodSearched = false;

        /// <summary>
        /// Try to localize a key using the game's localization system
        /// </summary>
        private static string TryLocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                // Cache the Localize method on first use
                if (!_localizeMethodSearched)
                {
                    _localizeMethodSearched = true;

                    // Search in Assembly-CSharp for LocalizationExtensions.Localize
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var assemblyName = assembly.GetName().Name;
                        if (!assemblyName.Contains("Assembly-CSharp"))
                            continue;

                        try
                        {
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                // Look for static classes that contain extension methods
                                if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
                                    continue;

                                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                                foreach (var method in methods)
                                {
                                    if (method.Name == "Localize" && method.ReturnType == typeof(string))
                                    {
                                        var parameters = method.GetParameters();
                                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                        {
                                            _localizeMethod = method;
                                            break;
                                        }
                                    }
                                }
                                if (_localizeMethod != null) break;
                            }
                        }
                        catch { }
                        if (_localizeMethod != null) break;
                    }
                }

                // Use cached method
                if (_localizeMethod != null)
                {
                    var parameters = _localizeMethod.GetParameters();
                    object[] args;
                    if (parameters.Length == 1)
                    {
                        args = new object[] { key };
                    }
                    else
                    {
                        // Fill additional params with defaults
                        args = new object[parameters.Length];
                        args[0] = key;
                        for (int i = 1; i < parameters.Length; i++)
                        {
                            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                        }
                    }

                    var result = _localizeMethod.Invoke(null, args);
                    if (result is string localized && !string.IsNullOrEmpty(localized))
                    {
                        return localized;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"TryLocalizeKey error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Resolve placeholders like {[effect0.status0.power]} in localized text
        /// </summary>
        private static string ResolveEffectPlaceholders(string text, object relicData, Type relicType)
        {
            if (string.IsNullOrEmpty(text) || relicData == null) return text;

            try
            {
                // Get effects from the relic data (SinsData inherits from RelicData)
                var getEffectsMethod = relicType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null)
                {
                    // Try the base RelicData type
                    var baseType = relicType.BaseType;
                    while (baseType != null && getEffectsMethod == null)
                    {
                        getEffectsMethod = baseType.GetMethod("GetEffects", Type.EmptyTypes);
                        baseType = baseType.BaseType;
                    }
                }

                if (getEffectsMethod != null)
                {
                    var effects = getEffectsMethod.Invoke(relicData, null) as System.Collections.IList;
                    if (effects != null && effects.Count > 0)
                    {
                        // Look for patterns like {[effect0.status0.power]} or {[effect0.power]}
                        var regex = new Regex(@"\{\[effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");
                        text = regex.Replace(text, match =>
                        {
                            int effectIndex = int.Parse(match.Groups[1].Value);
                            string property = match.Groups[3].Value;
                            int statusIndex = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : -1;

                            if (effectIndex < effects.Count)
                            {
                                var effect = effects[effectIndex];
                                if (effect != null)
                                {
                                    var effectType = effect.GetType();

                                    // If status index specified, get from paramStatusEffects array
                                    if (statusIndex >= 0 && property.ToLower() == "power")
                                    {
                                        var statusEffectsField = effectType.GetField("paramStatusEffects",
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (statusEffectsField != null)
                                        {
                                            var statusEffects = statusEffectsField.GetValue(effect) as Array;
                                            if (statusEffects != null && statusIndex < statusEffects.Length)
                                            {
                                                var statusEffect = statusEffects.GetValue(statusIndex);
                                                if (statusEffect != null)
                                                {
                                                    // StatusEffectStackData has 'count' field for the power
                                                    var countField = statusEffect.GetType().GetField("count",
                                                        BindingFlags.Public | BindingFlags.Instance);
                                                    if (countField != null)
                                                    {
                                                        var count = countField.GetValue(statusEffect);
                                                        return count?.ToString() ?? match.Value;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Try to get the property directly from effect
                                        var propField = effectType.GetField("param" + char.ToUpper(property[0]) + property.Substring(1),
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (propField != null)
                                        {
                                            var value = propField.GetValue(effect);
                                            return value?.ToString() ?? match.Value;
                                        }
                                    }
                                }
                            }
                            return match.Value;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveEffectPlaceholders error: {ex.Message}");
            }

            return text;
        }

        /// <summary>
        /// Get a human-readable display name from the reward type
        /// </summary>
        private static string GetRewardTypeDisplayName(Type rewardType)
        {
            string typeName = rewardType.Name;
            if (typeName.EndsWith("RewardData"))
                typeName = typeName.Substring(0, typeName.Length - "RewardData".Length);

            // Convert type name to readable format
            switch (typeName)
            {
                case "RelicPool": return "Random Artifact";
                case "Relic": return "Artifact";
                case "CardPool": return "Random Card";
                case "Card": return "Card";
                case "Gold": return "Gold";
                case "Health": return "Pyre Health";
                case "Crystal": return "Crystal";
                case "EnhancerPool": return "Random Upgrade";
                case "Enhancer": return "Upgrade";
                case "Draft": return "Card Draft";
                case "RelicDraft": return "Artifact Choice";
                default: return typeName;
            }
        }
    }
}
