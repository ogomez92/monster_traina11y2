using HarmonyLib;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using System;
using System.Linq;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Patches for detecting screen transitions and notifying accessibility handlers.
    /// These patches hook into game screen managers to detect when players enter different screens.
    /// </summary>

    /// <summary>
    /// Detect when main menu is shown
    /// </summary>
    public static class MainMenuScreenPatch
    {
        // Target: MainMenuScreen.Initialize
        // This will be resolved at runtime when the game DLLs are available

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MainMenuScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MainMenuScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched MainMenuScreen.Initialize");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MainMenuScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("MainMenuScreen.Initialize called!");
                ScreenStateTracker.SetScreen(Help.GameScreen.MainMenu);

                if (MonsterTrainAccessibility.MenuHandler == null)
                {
                    MonsterTrainAccessibility.LogInfo("MenuHandler is null - announcing directly");
                    MonsterTrainAccessibility.ScreenReader?.Speak("Main Menu. Press F1 for help.", false);
                }
                else
                {
                    MonsterTrainAccessibility.MenuHandler.OnMainMenuEntered(__instance);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MainMenuScreen patch: {ex.Message}");
            }
        }
    }

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
                var sb = new System.Text.StringBuilder();

                // Get battle/boss name from SaveManager's scenario data (more reliable than UI label)
                string battleName = GetBossNameFromScreen(battleIntroScreen, screenType);

                // Get battle description
                string battleDescription = null;
                var descField = screenType.GetField("battleDescriptionLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                    sb.Append(Screens.BattleAccessibility.StripRichTextTags(battleDescription));
                    sb.Append(" ");
                }

                // Check for trial
                var trialDataField = screenType.GetField("trialData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                object trialData = trialDataField?.GetValue(battleIntroScreen);

                if (trialData != null)
                {
                    var trialEnabledField = screenType.GetField("trialEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                    var sinField = trialType.GetField("sin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                    var rewardField = trialType.GetField("reward", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                            sb.Append(Screens.BattleAccessibility.StripRichTextTags(ruleDescription));
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
                            sb.Append(Screens.BattleAccessibility.StripRichTextTags(ruleDescription));
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
                var saveManagerField = screenType.GetField("saveManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        var saveManagerType = saveManager.GetType();

                        // Try GetCurrentScenarioData method
                        var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                        var getScenarioMethod = saveManagerType.GetMethod("GetScenario", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                var battleNameField = screenType.GetField("battleNameLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                    var field = dataType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                var titleKeyField = rewardType.GetField("_rewardTitleKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

        private static System.Reflection.MethodInfo _localizeMethod = null;
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

                                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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
                        var regex = new System.Text.RegularExpressions.Regex(@"\{\[effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");
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
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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

    /// <summary>
    /// Detect when combat starts
    /// </summary>
    public static class CombatStartPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CombatManager");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "StartCombat");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CombatStartPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatManager.StartCombat");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CombatManager: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("CombatManager.StartCombat called!");
                ScreenStateTracker.SetScreen(Help.GameScreen.Battle);

                if (MonsterTrainAccessibility.BattleHandler == null)
                {
                    MonsterTrainAccessibility.LogInfo("BattleHandler is null - announcing directly");
                    MonsterTrainAccessibility.ScreenReader?.Speak("Battle started. Press H for hand, L for floors, R for resources, F1 for help.", false);
                }
                else
                {
                    MonsterTrainAccessibility.BattleHandler.OnBattleEntered();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CombatStart patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect card draft screen
    /// </summary>
    public static class CardDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CardDraftScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CardDraftScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CardDraftScreen.Setup");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CardDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.CardDraft);
                // Extract draft cards from __instance and call handler
                // This would parse the actual CardDraftScreen to get card data
                MonsterTrainAccessibility.LogInfo("Card draft screen detected");

                // For now, announce generic draft entry
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Card Draft. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDraftScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect clan/class selection screen (RunSetupScreen in MT2, ClassSelectionScreen in MT1)
    /// </summary>
    public static class ClassSelectionScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try MT2's RunSetupScreen first
                var targetType = AccessTools.TypeByName("RunSetupScreen");
                if (targetType == null)
                {
                    // Fall back to MT1's ClassSelectionScreen
                    targetType = AccessTools.TypeByName("ClassSelectionScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ClassSelectionScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.Initialize");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunSetupScreen/ClassSelectionScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ClanSelection);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Run Setup. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunSetupScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect map screen
    /// </summary>
    public static class MapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MapScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("MapNodeScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MapScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MapScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Map);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Map. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MapScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for merchant/shop screen
    /// </summary>
    public static class MerchantScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MerchantScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Open") ??
                                 AccessTools.Method(targetType, "Show");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MerchantScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched MerchantScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("MerchantScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MerchantScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Shop);

                // Announce gold when entering shop
                int gold = InputInterceptor.GetCurrentGold();
                string goldText = gold >= 0 ? $"You have {gold} gold." : "";

                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Shop. {goldText} Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MerchantScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for enhancer/upgrade card selection screen
    /// </summary>
    public static class EnhancerSelectionScreenPatch
    {
        private static string _lastEnhancerName = null;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible class names for the upgrade card selection screen
                var targetNames = new[] {
                    "UpgradeSelectionScreen",
                    "EnhancerSelectionScreen",
                    "CardUpgradeSelectionScreen",
                    "UpgradeScreen",
                    "EnhancerScreen"
                };

                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Open");

                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(EnhancerSelectionScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo("EnhancerSelectionScreen not found - will use alternative detection");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch EnhancerSelectionScreen: {ex.Message}");
            }
        }

        public static void SetEnhancerName(string name)
        {
            _lastEnhancerName = name;
        }

        public static void Postfix(object __instance)
        {
            try
            {
                // Try to get the card count from the screen
                int cardCount = 0;
                var instanceType = __instance.GetType();

                // Look for cards list or count
                var getCardsMethod = instanceType.GetMethod("GetCards", System.Type.EmptyTypes) ??
                                     instanceType.GetMethod("GetCardList", System.Type.EmptyTypes);
                if (getCardsMethod != null)
                {
                    var cards = getCardsMethod.Invoke(__instance, null) as System.Collections.IList;
                    if (cards != null)
                        cardCount = cards.Count;
                }

                // Also try cards field
                if (cardCount == 0)
                {
                    var fields = instanceType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        if (field.Name.ToLower().Contains("card"))
                        {
                            var value = field.GetValue(__instance);
                            if (value is System.Collections.IList list)
                            {
                                cardCount = list.Count;
                                break;
                            }
                        }
                    }
                }

                MonsterTrainAccessibility.DraftHandler?.OnEnhancerCardSelectionEntered(_lastEnhancerName, cardCount);
                _lastEnhancerName = null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in EnhancerSelectionScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect game over / run summary screen (victory or defeat)
    /// </summary>
    public static class GameOverScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible class names for the game over screen
                var targetNames = new[] {
                    "GameOverScreen",
                    "RunSummaryScreen",
                    "VictoryScreen",
                    "DefeatScreen",
                    "RunEndScreen",
                    "EndRunScreen"
                };

                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Open");

                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(GameOverScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo("GameOverScreen not found - will use alternative detection");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch GameOverScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Game over screen entered");

                // Auto-read the game over screen
                AutoReadGameOverScreen(__instance);

                // Also call the menu handler for additional processing
                MonsterTrainAccessibility.MenuHandler?.OnGameOverScreenEntered(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in GameOverScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadGameOverScreen(object screen)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var screenType = screen?.GetType();

                // Log fields for debugging
                MonsterTrainAccessibility.LogInfo($"=== GameOverScreen fields ===");
                if (screenType != null)
                {
                    foreach (var field in screenType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    {
                        try
                        {
                            var value = field.GetValue(screen);
                            MonsterTrainAccessibility.LogInfo($"  {field.Name} = {value?.GetType().Name ?? "null"}");
                        }
                        catch { }
                    }
                }

                // Try to get victory/defeat status from SaveManager
                bool isVictory = false;
                int score = 0;
                int battlesWon = 0;
                int ring = 0;

                var saveManagerType = AccessTools.TypeByName("SaveManager");
                if (saveManagerType != null)
                {
                    var saveManager = UnityEngine.Object.FindObjectOfType(saveManagerType) as UnityEngine.Object;
                    if (saveManager != null)
                    {
                        // Check if battle was won/lost
                        var battleCompleteMethod = saveManagerType.GetMethod("BattleComplete");
                        if (battleCompleteMethod != null)
                        {
                            var result = battleCompleteMethod.Invoke(saveManager, null);
                            if (result is bool bc) isVictory = bc;
                        }

                        // Get ring/covenant level
                        var getCovenantMethod = saveManagerType.GetMethod("GetAscensionLevel") ??
                                               saveManagerType.GetMethod("GetCovenantLevel");
                        if (getCovenantMethod != null)
                        {
                            var result = getCovenantMethod.Invoke(saveManager, null);
                            if (result is int r) ring = r;
                        }

                        // Get battles won
                        var getBattlesMethod = saveManagerType.GetMethod("GetNumBattlesWon");
                        if (getBattlesMethod != null)
                        {
                            var result = getBattlesMethod.Invoke(saveManager, null);
                            if (result is int b) battlesWon = b;
                        }

                        // Get score
                        var getScoreMethod = saveManagerType.GetMethod("GetRunScore") ??
                                            saveManagerType.GetMethod("GetScore");
                        if (getScoreMethod != null)
                        {
                            var result = getScoreMethod.Invoke(saveManager, null);
                            if (result is int s) score = s;
                        }
                    }
                }

                // Try to get specific labels from the screen
                string resultTitle = null;
                string runType = null;

                if (screenType != null)
                {
                    // Look for result/victory/defeat label
                    var resultField = screenType.GetField("resultLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                     screenType.GetField("titleLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                     screenType.GetField("victoryDefeatLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (resultField != null)
                    {
                        var labelObj = resultField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                resultTitle = textProp.GetValue(labelObj) as string;
                            }
                        }
                    }

                    // Look for run type label
                    var runTypeField = screenType.GetField("runTypeLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                      screenType.GetField("runNameLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (runTypeField != null)
                    {
                        var labelObj = runTypeField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                runType = textProp.GetValue(labelObj) as string;
                            }
                        }
                    }

                    // Look for score label
                    var scoreField = screenType.GetField("scoreLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                    screenType.GetField("totalScoreLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (scoreField != null && score == 0)
                    {
                        var labelObj = scoreField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                var scoreText = textProp.GetValue(labelObj) as string;
                                if (!string.IsNullOrEmpty(scoreText))
                                {
                                    // Parse score from text like "4,254"
                                    scoreText = System.Text.RegularExpressions.Regex.Replace(scoreText, "[^0-9]", "");
                                    int.TryParse(scoreText, out score);
                                }
                            }
                        }
                    }
                }

                // Build announcement
                // Result title (Victory/Defeat)
                if (!string.IsNullOrEmpty(resultTitle))
                {
                    sb.Append($"{resultTitle}. ");
                }
                else
                {
                    sb.Append(isVictory ? "Victory. " : "Defeat. ");
                }

                // Run type and ring
                if (!string.IsNullOrEmpty(runType))
                {
                    sb.Append($"{runType}. ");
                }
                if (ring > 0)
                {
                    sb.Append($"Covenant {ring}. ");
                }

                // Score
                if (score > 0)
                {
                    sb.Append($"Score: {score:N0}. ");
                }

                // Battles
                if (battlesWon > 0)
                {
                    sb.Append($"Battles won: {battlesWon}. ");
                }

                // Announce
                string announcement = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Game over auto-read: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
                else
                {
                    // Fallback
                    MonsterTrainAccessibility.ScreenReader?.Speak("Run complete. Press T to read stats.", false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AutoReadGameOverScreen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete.", false);
            }
        }
    }

    /// <summary>
    /// Detect settings screen
    /// </summary>
    public static class SettingsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SettingsScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("OptionsScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show") ??
                                 AccessTools.Method(targetType, "Open");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(SettingsScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("SettingsScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SettingsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Settings. Press Tab to switch between tabs.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SettingsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generic screen manager patch to catch all screen transitions
    /// </summary>
    public static class ScreenManagerPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ScreenManager");
                if (targetType != null)
                {
                    // Try to find the method that handles screen changes
                    var method = AccessTools.Method(targetType, "ChangeScreen") ??
                                 AccessTools.Method(targetType, "LoadScreen") ??
                                 AccessTools.Method(targetType, "ShowScreen");

                    if (method != null)
                    {
                        // Log the parameters so we know what to capture
                        var parameters = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"ScreenManager.{method.Name} has {parameters.Length} parameters:");
                        foreach (var param in parameters)
                        {
                            MonsterTrainAccessibility.LogInfo($"  {param.Name}: {param.ParameterType.Name}");
                        }

                        var postfix = new HarmonyMethod(typeof(ScreenManagerPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ScreenManager.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ScreenManager: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, object __0)
        {
            try
            {
                // __0 is the first parameter (ScreenName enum)
                string screenName = __0?.ToString() ?? "Unknown";

                MonsterTrainAccessibility.LogInfo($"Screen transition: {screenName}");

                // Don't announce raw screen transitions - let individual screen patches handle announcements
                // This just logs for debugging
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ScreenManager patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Format a screen type name into a readable announcement
        /// </summary>
        private static string FormatScreenName(string screenName)
        {
            if (string.IsNullOrEmpty(screenName) || screenName == "Unknown")
                return null;

            // Remove "Screen" suffix for cleaner announcements
            if (screenName.EndsWith("Screen"))
            {
                screenName = screenName.Substring(0, screenName.Length - 6);
            }

            // Add spaces before capital letters (CamelCase to readable)
            var formatted = System.Text.RegularExpressions.Regex.Replace(screenName, "([a-z])([A-Z])", "$1 $2");

            return formatted;
        }
    }

    /// <summary>
    /// Patch for Dialog display - auto-read FTUE/tutorial dialogs
    /// </summary>
    public static class DialogPatch
    {
        private static string _lastDialogText = null;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Patch DialogScreen.ShowDialog (the entry point)
                var dialogScreenType = AccessTools.TypeByName("DialogScreen");
                if (dialogScreenType != null)
                {
                    MonsterTrainAccessibility.LogInfo($"Found DialogScreen type: {dialogScreenType.FullName}");

                    foreach (var m in dialogScreenType.GetMethods())
                    {
                        if (m.Name == "ShowDialog" && m.GetParameters().Length == 1)
                        {
                            var postfix = new HarmonyMethod(typeof(DialogPatch).GetMethod(nameof(DialogScreenPostfix)));
                            harmony.Patch(m, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched DialogScreen.ShowDialog");
                            break;
                        }
                    }
                }
                else
                {
                    MonsterTrainAccessibility.LogError("DialogScreen type not found!");
                }

                // Also patch Dialog.Show as backup
                var targetType = AccessTools.TypeByName("Dialog");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogError("Dialog type not found!");
                    return;
                }

                MonsterTrainAccessibility.LogInfo($"Found Dialog type: {targetType.FullName}");

                // Find Show method
                System.Reflection.MethodInfo method = null;
                foreach (var m in targetType.GetMethods())
                {
                    if (m.Name == "Show" && m.GetParameters().Length == 1)
                    {
                        method = m;
                        MonsterTrainAccessibility.LogInfo($"Found Dialog.Show with param: {m.GetParameters()[0].ParameterType.FullName}");
                        break;
                    }
                }

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DialogPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched Dialog.Show for FTUE/tutorial auto-read");
                }
                else
                {
                    MonsterTrainAccessibility.LogError("Dialog.Show method not found!");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch Dialog: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void DialogScreenPostfix(object __0)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("[DialogPatch] DialogScreen.ShowDialog called!");

                if (__0 != null)
                {
                    var contentField = __0.GetType().GetField("content");
                    if (contentField != null)
                    {
                        string content = contentField.GetValue(__0) as string;
                        if (!string.IsNullOrEmpty(content))
                        {
                            MonsterTrainAccessibility.LogInfo($"[DialogPatch] DialogScreen content: {content.Substring(0, Math.Min(100, content.Length))}");

                            if (content != _lastDialogText)
                            {
                                _lastDialogText = content;
                                string cleanText = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "");
                                MonsterTrainAccessibility.ScreenReader?.Speak($"Dialog: {cleanText}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DialogScreen patch: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, object __0)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("[DialogPatch] Postfix called!");

                if (__instance == null)
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] __instance is null");
                    return;
                }

                // Get the dialog content from the Data parameter
                string content = null;
                if (__0 != null)
                {
                    MonsterTrainAccessibility.LogInfo($"[DialogPatch] Data type: {__0.GetType().FullName}");
                    var contentField = __0.GetType().GetField("content");
                    if (contentField != null)
                    {
                        content = contentField.GetValue(__0) as string;
                        MonsterTrainAccessibility.LogInfo($"[DialogPatch] Got content from field: {content?.Substring(0, Math.Min(50, content?.Length ?? 0))}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("[DialogPatch] content field not found on Data");
                    }
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] __0 (Data) is null");
                }

                // Try to get content directly from the dialog instance
                if (string.IsNullOrEmpty(content))
                {
                    var instanceType = __instance.GetType();
                    var contentLabelField = instanceType.GetField("contentLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (contentLabelField != null)
                    {
                        var label = contentLabelField.GetValue(__instance);
                        if (label != null)
                        {
                            var textProp = label.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                content = textProp.GetValue(label) as string;
                                MonsterTrainAccessibility.LogInfo($"[DialogPatch] Got content from contentLabel: {content?.Substring(0, Math.Min(50, content?.Length ?? 0))}");
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(content) && content != _lastDialogText)
                {
                    _lastDialogText = content;
                    MonsterTrainAccessibility.LogInfo($"[DialogPatch] Speaking dialog: {content.Substring(0, Math.Min(100, content.Length))}...");

                    // Clean up any rich text tags
                    string cleanText = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "");

                    // Speak the dialog content
                    MonsterTrainAccessibility.ScreenReader?.Speak($"Tutorial: {cleanText}");
                }
                else if (string.IsNullOrEmpty(content))
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] No content found");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] Same content as before, not repeating");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in Dialog patch: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Patch for FTUE (First Time User Experience) tooltips - tutorial hints
    /// </summary>
    public static class FtueTooltipPatch
    {
        private static string _lastTooltipText = null;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible FTUE tooltip type names
                string[] possibleTypes = new[]
                {
                    "FtueTooltip", "FTUETooltip", "TutorialTooltip", "FtuePanel",
                    "TutorialPanel", "FtueHighlight", "CombatFtueTooltip", "FtueUI"
                };

                foreach (var typeName in possibleTypes)
                {
                    var targetType = AccessTools.TypeByName(typeName);
                    if (targetType != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found FTUE type: {targetType.FullName}");

                        // Log all methods for debugging
                        var methods = targetType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        MonsterTrainAccessibility.LogInfo($"  Methods: {string.Join(", ", methods.Select(m => m.Name).Distinct())}");

                        // Try to patch Show, Display, or Initialize methods
                        string[] methodNames = new[] { "Show", "Display", "Initialize", "Setup", "Open", "SetData", "SetContent" };
                        foreach (var methodName in methodNames)
                        {
                            var method = AccessTools.Method(targetType, methodName);
                            if (method != null)
                            {
                                var postfix = new HarmonyMethod(typeof(FtueTooltipPatch).GetMethod(nameof(Postfix)));
                                harmony.Patch(method, postfix: postfix);
                                MonsterTrainAccessibility.LogInfo($"Patched {typeName}.{methodName}");
                                break;
                            }
                        }
                    }
                }

                // Also try to find generic Tooltip types
                var tooltipType = AccessTools.TypeByName("Tooltip") ?? AccessTools.TypeByName("TooltipUI");
                if (tooltipType != null)
                {
                    MonsterTrainAccessibility.LogInfo($"Found Tooltip type: {tooltipType.FullName}");
                    var showMethod = AccessTools.Method(tooltipType, "Show") ?? AccessTools.Method(tooltipType, "Display");
                    if (showMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(FtueTooltipPatch).GetMethod(nameof(TooltipPostfix)));
                        harmony.Patch(showMethod, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched Tooltip.{showMethod.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch FTUE tooltips: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("[FtueTooltipPatch] FTUE tooltip shown!");
                ReadTooltipContent(__instance, "FTUE");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in FTUE tooltip patch: {ex.Message}");
            }
        }

        public static void TooltipPostfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("[FtueTooltipPatch] Tooltip shown!");
                ReadTooltipContent(__instance, "Tooltip");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in Tooltip patch: {ex.Message}");
            }
        }

        private static void ReadTooltipContent(object instance, string prefix)
        {
            if (instance == null) return;

            var type = instance.GetType();

            // Try various ways to get the tooltip text
            string title = null;
            string description = null;

            // Try common field/property names for title
            string[] titleNames = new[] { "title", "Title", "_title", "header", "Header", "_header", "titleText", "headerText" };
            foreach (var name in titleNames)
            {
                var field = type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var val = field.GetValue(instance);
                    if (val is string s)
                    {
                        title = s;
                        break;
                    }
                    // Could be a TMP text component
                    var textProp = val?.GetType()?.GetProperty("text");
                    if (textProp != null)
                    {
                        title = textProp.GetValue(val) as string;
                        break;
                    }
                }

                var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    var val = prop.GetValue(instance);
                    if (val is string s)
                    {
                        title = s;
                        break;
                    }
                }
            }

            // Try common field/property names for description
            string[] descNames = new[] { "description", "Description", "_description", "content", "Content", "_content", "body", "Body", "_body", "text", "Text", "_text", "descriptionText", "contentText" };
            foreach (var name in descNames)
            {
                var field = type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var val = field.GetValue(instance);
                    if (val is string s)
                    {
                        description = s;
                        break;
                    }
                    var textProp = val?.GetType()?.GetProperty("text");
                    if (textProp != null)
                    {
                        description = textProp.GetValue(val) as string;
                        break;
                    }
                }

                var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    var val = prop.GetValue(instance);
                    if (val is string s)
                    {
                        description = s;
                        break;
                    }
                }
            }

            // Build announcement
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(title))
            {
                sb.Append(title);
                sb.Append(". ");
            }
            if (!string.IsNullOrEmpty(description))
            {
                string cleanDesc = System.Text.RegularExpressions.Regex.Replace(description, @"<[^>]+>", "");
                sb.Append(cleanDesc);
            }

            string announcement = sb.ToString().Trim();
            if (!string.IsNullOrEmpty(announcement) && announcement != _lastTooltipText)
            {
                _lastTooltipText = announcement;
                MonsterTrainAccessibility.LogInfo($"[FtueTooltipPatch] Announcing: {announcement.Substring(0, Math.Min(100, announcement.Length))}...");
                MonsterTrainAccessibility.ScreenReader?.Speak($"{prefix}: {announcement}");
            }
        }
    }

    /// <summary>
    /// Patch for story event screen (random events on the map)
    /// </summary>
    public static class StoryEventScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StoryEventScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "SetupStory");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched StoryEventScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("StoryEventScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StoryEventScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Event);
                MonsterTrainAccessibility.LogInfo("Story event screen entered");

                // Auto-read the event content
                AutoReadStoryEvent(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StoryEventScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadStoryEvent(object screen)
        {
            try
            {
                if (screen == null) return;
                var screenType = screen.GetType();

                // Try to get the story content from the storyContent field
                var storyContentField = screenType.GetField("storyContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (storyContentField != null)
                {
                    var storyContent = storyContentField.GetValue(screen);
                    if (storyContent != null)
                    {
                        var contentType = storyContent.GetType();

                        // Try to get event name
                        string eventName = null;
                        var getNameMethod = contentType.GetMethod("GetName") ?? contentType.GetMethod("GetTitle");
                        if (getNameMethod != null)
                        {
                            eventName = getNameMethod.Invoke(storyContent, null) as string;
                        }

                        // Fall back to KnotName field or property
                        if (string.IsNullOrEmpty(eventName))
                        {
                            var knotNameField = contentType.GetField("KnotName");
                            if (knotNameField != null)
                            {
                                eventName = knotNameField.GetValue(storyContent) as string;
                            }
                            else
                            {
                                var knotNameProp = contentType.GetProperty("KnotName");
                                if (knotNameProp != null)
                                {
                                    eventName = knotNameProp.GetValue(storyContent) as string;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(eventName))
                        {
                            MonsterTrainAccessibility.ScreenReader?.Speak($"Random event: {eventName}. Press F1 for help.");
                            return;
                        }
                    }
                }

                // Generic announcement
                MonsterTrainAccessibility.ScreenReader?.Speak("Random event. Listen for choices. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading story event: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Random event. Press F1 for help.");
            }
        }
    }

    /// <summary>
    /// Patch for reward screen (after battle rewards)
    /// </summary>
    public static class RewardScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RewardScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RewardScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RewardScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RewardScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RewardScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RewardScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Rewards);
                MonsterTrainAccessibility.LogInfo("Reward screen entered");

                // Count rewards if possible
                int rewardCount = CountRewards(__instance);
                string countText = rewardCount > 0 ? $" {rewardCount} rewards available." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Rewards.{countText} Use arrow keys to browse, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RewardScreen patch: {ex.Message}");
            }
        }

        private static int CountRewards(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                // Look for rewards list
                var fields = screenType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("reward"))
                    {
                        var value = field.GetValue(screen);
                        if (value is System.Collections.IList list)
                        {
                            return list.Count;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }

    /// <summary>
    /// Patch for relic/artifact draft screen
    /// </summary>
    public static class RelicDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RelicDraftScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RelicDraftScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RelicDraftScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RelicDraftScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RelicDraftScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RelicDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Relic draft screen entered");
                Help.Contexts.ArtifactSelectionHelp.SetActive(true);

                // Count artifacts if possible
                int count = CountRelics(__instance);
                string countText = count > 0 ? $" Choose from {count} artifacts." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Artifact Selection.{countText} Use arrow keys to browse, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RelicDraftScreen patch: {ex.Message}");
            }
        }

        private static int CountRelics(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                var fields = screenType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("relic") || fieldName.Contains("artifact") || fieldName.Contains("choice"))
                    {
                        var value = field.GetValue(screen);
                        if (value is System.Collections.IList list)
                        {
                            return list.Count;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }

    /// <summary>
    /// Patch for deck screen (viewing your deck)
    /// </summary>
    public static class DeckScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DeckScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("DeckScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DeckScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched DeckScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("DeckScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DeckScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Deck screen entered");
                Help.Contexts.DeckViewHelp.SetActive(true);

                // Count cards if possible
                int cardCount = CountCards(__instance);
                string countText = cardCount > 0 ? $" Your deck has {cardCount} cards." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Deck View.{countText} Use arrow keys to browse cards. Press Escape to close. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DeckScreen patch: {ex.Message}");
            }
        }

        private static int CountCards(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                var fields = screenType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") && (fieldName.Contains("list") || fieldName.Contains("deck")))
                    {
                        var value = field.GetValue(screen);
                        if (value is System.Collections.IList list)
                        {
                            return list.Count;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }

    /// <summary>
    /// Patch for compendium/logbook screen
    /// </summary>
    public static class CompendiumScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CompendiumScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("CompendiumScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CompendiumScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CompendiumScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CompendiumScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CompendiumScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Compendium screen entered");
                ScreenStateTracker.SetScreen(Help.GameScreen.Compendium);
                MonsterTrainAccessibility.ScreenReader?.Speak("Compendium. Browse cards, clans, and game information. Use Tab to switch categories. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CompendiumScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for champion upgrade screen
    /// </summary>
    public static class ChampionUpgradeScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChampionUpgradeScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ChampionUpgradeScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(ChampionUpgradeScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched ChampionUpgradeScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("ChampionUpgradeScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChampionUpgradeScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Champion upgrade screen entered");
                Help.Contexts.ChampionUpgradeHelp.SetActive(true);
                MonsterTrainAccessibility.ScreenReader?.Speak("Champion Upgrade. Choose an upgrade for your champion. Use arrow keys to browse options, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChampionUpgradeScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for run history screen
    /// </summary>
    public static class RunHistoryScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RunHistoryScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RunHistoryScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RunHistoryScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RunHistoryScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RunHistoryScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunHistoryScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                // Only trigger if this is actually a RunHistoryScreen (not base UIScreen)
                if (__instance == null) return;
                var typeName = __instance.GetType().Name;
                if (typeName != "RunHistoryScreen") return;

                MonsterTrainAccessibility.LogInfo("Run history screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run History. Browse your previous runs. Use arrow keys to navigate. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunHistoryScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for credits screen
    /// </summary>
    public static class CreditsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CreditsScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("CreditsScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CreditsScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CreditsScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CreditsScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CreditsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Credits screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Credits. Press Escape to return to main menu.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CreditsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Dragon's Hoard screen (MT2 specific)
    /// </summary>
    public static class DragonsHoardScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DragonsHoardScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("DragonsHoardScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DragonsHoardScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched DragonsHoardScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("DragonsHoardScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DragonsHoardScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Dragon's Hoard screen entered");

                // Try to get gold count
                int gold = GetHoardGold(__instance);
                string goldText = gold > 0 ? $" {gold} gold stored." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Dragon's Hoard.{goldText} Use arrow keys to browse options. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DragonsHoardScreen patch: {ex.Message}");
            }
        }

        private static int GetHoardGold(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                // Look for gold/hoard value
                var fields = screenType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("gold") || fieldName.Contains("hoard") || fieldName.Contains("value"))
                    {
                        var value = field.GetValue(screen);
                        if (value is int g)
                        {
                            return g;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }

    /// <summary>
    /// Patch for Elixir draft screen (MT2 specific)
    /// </summary>
    public static class ElixirDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ElixirDraftScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ElixirDraftScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(ElixirDraftScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched ElixirDraftScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("ElixirDraftScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ElixirDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Elixir draft screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Elixir Selection. Choose an elixir to modify your run. Use arrow keys to browse, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ElixirDraftScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Run Opening screen (showing upcoming battles)
    /// </summary>
    public static class RunOpeningScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RunOpeningScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RunOpeningScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RunOpeningScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RunOpeningScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RunOpeningScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunOpeningScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Run opening screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run Overview. Showing upcoming battles and challenges. Use arrow keys to browse. Press Enter to begin. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunOpeningScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Run Summary screen (end of run stats)
    /// </summary>
    public static class RunSummaryScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RunSummaryScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RunSummaryScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RunSummaryScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RunSummaryScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RunSummaryScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunSummaryScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Run summary screen entered");
                // The GameOverScreenPatch may already handle this, but this is a backup
                MonsterTrainAccessibility.ScreenReader?.Speak("Run Summary. Press T to read stats. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunSummaryScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Challenge screens
    /// </summary>
    public static class ChallengeScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try ChallengeOverviewScreen
                var targetType = AccessTools.TypeByName("ChallengeOverviewScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ChallengeOverviewScreen type not found");
                }
                else
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeScreenPatch).GetMethod(nameof(OverviewPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeOverviewScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("ChallengeOverviewScreen methods not found");
                    }
                }

                // Try ChallengeDetailsScreen
                var detailsType = AccessTools.TypeByName("ChallengeDetailsScreen");
                if (detailsType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ChallengeDetailsScreen type not found");
                }
                else
                {
                    var method = AccessTools.Method(detailsType, "Initialize") ??
                                 AccessTools.Method(detailsType, "Setup");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeScreenPatch).GetMethod(nameof(DetailsPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeDetailsScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("ChallengeDetailsScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch Challenge screens: {ex.Message}");
            }
        }

        public static void OverviewPostfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Challenge overview screen entered");
                Help.Contexts.ChallengesHelp.SetActive(true);
                MonsterTrainAccessibility.ScreenReader?.Speak("Challenges Overview. Browse daily and weekly challenges. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeOverviewScreen patch: {ex.Message}");
            }
        }

        public static void DetailsPostfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Challenge details screen entered");
                Help.Contexts.ChallengesHelp.SetActive(true);
                MonsterTrainAccessibility.ScreenReader?.Speak("Challenge Details. View challenge rules and rewards. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeDetailsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Card Details popup
    /// </summary>
    public static class CardDetailsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CardDetailsScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("CardDetailsScreen type not found");
                    return;
                }

                // First try ShowCard which has a CardState parameter
                var showCardMethod = AccessTools.Method(targetType, "ShowCard");
                if (showCardMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(CardDetailsScreenPatch).GetMethod(nameof(ShowCardPostfix)));
                    harmony.Patch(showCardMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CardDetailsScreen.ShowCard");
                    return;
                }

                // Fall back to Initialize/Setup
                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CardDetailsScreenPatch).GetMethod(nameof(InitializePostfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CardDetailsScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CardDetailsScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CardDetailsScreen: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for ShowCard method which receives a CardState
        /// </summary>
        public static void ShowCardPostfix(object __instance, object __0)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("CardDetailsScreen.ShowCard called");

                // __0 is the CardState being shown
                if (__0 != null)
                {
                    string cardInfo = GetCardInfo(__0);
                    if (!string.IsNullOrEmpty(cardInfo))
                    {
                        MonsterTrainAccessibility.ScreenReader?.Speak($"Card Details: {cardInfo}. Press Escape to close.");
                        return;
                    }
                }

                MonsterTrainAccessibility.ScreenReader?.Speak("Card Details. Press Escape to close.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDetailsScreen.ShowCard patch: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Card Details. Press Escape to close.");
            }
        }

        /// <summary>
        /// Postfix for Initialize/Setup methods (no card parameter)
        /// </summary>
        public static void InitializePostfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("CardDetailsScreen initialized");
                MonsterTrainAccessibility.ScreenReader?.Speak("Card Details. Press Escape to close.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDetailsScreen.Initialize patch: {ex.Message}");
            }
        }

        private static string GetCardInfo(object cardState)
        {
            try
            {
                if (cardState == null) return null;

                var cardType = cardState.GetType();
                MonsterTrainAccessibility.LogInfo($"Getting card info from type: {cardType.Name}");

                // Get card name
                var getTitleMethod = cardType.GetMethod("GetTitle") ?? cardType.GetMethod("GetName");
                string name = getTitleMethod?.Invoke(cardState, null) as string ?? "Unknown";

                // Get cost
                var getCostMethod = cardType.GetMethod("GetCost") ?? cardType.GetMethod("GetCostWithoutAnyModifications");
                int cost = 0;
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardState, null);
                    if (costResult is int c) cost = c;
                }

                // Get description
                var getDescMethod = cardType.GetMethod("GetDescription");
                string desc = getDescMethod?.Invoke(cardState, null) as string ?? "";
                desc = Screens.BattleAccessibility.StripRichTextTags(desc);

                MonsterTrainAccessibility.LogInfo($"Card info: {name}, {cost} ember");
                return $"{name}, {cost} ember. {desc}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting card info: {ex.Message}");
            }
            return null;
        }
    }

    /// <summary>
    /// Patch for Minimap screen
    /// </summary>
    public static class MinimapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MinimapScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("MinimapScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(MinimapScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched MinimapScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("MinimapScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MinimapScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Minimap screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Minimap. View run progress and upcoming nodes. Press Escape to close.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MinimapScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch for Train Cosmetics screen (MT2)
    /// </summary>
    public static class TrainCosmeticsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("TrainCosmeticsScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("TrainCosmeticsScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(TrainCosmeticsScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched TrainCosmeticsScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("TrainCosmeticsScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch TrainCosmeticsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Train cosmetics screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Train Cosmetics. Customize your train appearance. Use arrow keys to browse options. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in TrainCosmeticsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Read game-over stat highlights (e.g., "Most damage: Hornbreaker Prince 245")
    /// </summary>
    public static class StatHighlightPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StatHighlightUI");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("StatHighlightUI not found");
                    return;
                }

                var animateMethod = AccessTools.Method(targetType, "AnimateInCoroutine");
                if (animateMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(StatHighlightPatch).GetMethod(nameof(AnimatePrefix)));
                    harmony.Patch(animateMethod, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched StatHighlightUI.AnimateInCoroutine");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StatHighlightUI: {ex.Message}");
            }
        }

        public static void AnimatePrefix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                string headerText = null;
                string statText = null;

                // Get headerLabel text
                var headerField = instanceType.GetField("headerLabel", bindingFlags);
                if (headerField != null)
                {
                    var label = headerField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            headerText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Get statLabel text
                var statField = instanceType.GetField("statLabel", bindingFlags);
                if (statField != null)
                {
                    var label = statField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            statText = textProp.GetValue(label) as string;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(headerText) || !string.IsNullOrEmpty(statText))
                {
                    headerText = Screens.BattleAccessibility.StripRichTextTags(headerText ?? "");
                    statText = Screens.BattleAccessibility.StripRichTextTags(statText ?? "");
                    statText = statText.Replace("\n", " ").Replace("\r", "");

                    string announcement = $"{headerText}: {statText}".Trim();
                    MonsterTrainAccessibility.LogInfo($"Stat highlight: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StatHighlightPatch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce win streak when it appears on the game over screen.
    /// </summary>
    public static class WinStreakPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // WinStreakIncreaseUI extends WinStreakUI. The Set() method is called
                // when the win streak display is updated. No AnimateInCoroutine exists.
                var targetType = AccessTools.TypeByName("WinStreakIncreaseUI");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("WinStreakIncreaseUI not found");
                    return;
                }

                // Set(int winStreak, int lowestAscensionLevel, bool trueFinalBossStreak) is on WinStreakIncreaseUI (override)
                var setMethod = AccessTools.Method(targetType, "Set");
                if (setMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(WinStreakPatch).GetMethod(nameof(SetPostfix)));
                    harmony.Patch(setMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched WinStreakIncreaseUI.Set");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("WinStreakIncreaseUI.Set not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch WinStreakIncreaseUI: {ex.Message}");
            }
        }

        // Set(int winStreak, int lowestAscensionLevel, bool trueFinalBossStreak)
        // __0 = winStreak
        public static void SetPostfix(object __instance, int __0)
        {
            try
            {
                if (__instance == null || __0 <= 0) return;

                int winStreak = __0;
                int previousStreak = winStreak - 1;

                string announcement;
                if (previousStreak > 0)
                {
                    announcement = $"Win streak increased! {previousStreak} to {winStreak}";
                }
                else
                {
                    announcement = $"Win streak: {winStreak}";
                }
                MonsterTrainAccessibility.LogInfo($"Win streak: {announcement}");
                MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in WinStreakPatch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce unlocks (level ups, new cards/relics, clan unlocks, etc.) as they appear.
    /// </summary>
    public static class UnlockScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("UnlockScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("UnlockScreen not found");
                    return;
                }

                var showMethod = AccessTools.Method(targetType, "ShowNextUnlock");
                if (showMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(UnlockScreenPatch).GetMethod(nameof(ShowNextUnlockPostfix)));
                    harmony.Patch(showMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched UnlockScreen.ShowNextUnlock");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch UnlockScreen: {ex.Message}");
            }
        }

        public static void ShowNextUnlockPostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                var currentItemProp = instanceType.GetProperty("currentItem", bindingFlags);
                if (currentItemProp == null) return;

                var currentItem = currentItemProp.GetValue(__instance);
                if (currentItem == null) return;

                var itemType = currentItem.GetType();

                var sourceField = itemType.GetField("source");
                string source = sourceField?.GetValue(currentItem)?.ToString() ?? "";

                var headerContentField = itemType.GetField("headerTextContent");
                string headerContent = headerContentField?.GetValue(currentItem) as string ?? "";

                var headerLevelField = itemType.GetField("headerLevel");
                int headerLevel = -1;
                if (headerLevelField != null)
                {
                    var lvl = headerLevelField.GetValue(currentItem);
                    if (lvl is int l) headerLevel = l;
                }

                string unlockedCardName = null;
                var cardDataField = itemType.GetField("unlockedCardData");
                if (cardDataField != null)
                {
                    var cardData = cardDataField.GetValue(currentItem);
                    if (cardData != null)
                    {
                        var getNameMethod = cardData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                            unlockedCardName = getNameMethod.Invoke(cardData, null) as string;
                    }
                }

                string unlockedRelicName = null;
                var relicDataField = itemType.GetField("unlockedRelicData");
                if (relicDataField != null)
                {
                    var relicData = relicDataField.GetValue(currentItem);
                    if (relicData != null)
                    {
                        var getNameMethod = relicData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                            unlockedRelicName = getNameMethod.Invoke(relicData, null) as string;
                    }
                }

                string featureTitle = null;
                var featureDataField = itemType.GetField("unlockedFeatureData");
                if (featureDataField != null)
                {
                    var featureData = featureDataField.GetValue(currentItem);
                    if (featureData != null)
                    {
                        var titleField = featureData.GetType().GetField("title");
                        if (titleField != null)
                            featureTitle = titleField.GetValue(featureData) as string;
                    }
                }

                string announcement = BuildUnlockAnnouncement(source, headerContent, headerLevel,
                    unlockedCardName, unlockedRelicName, featureTitle);

                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Unlock: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in UnlockScreenPatch: {ex.Message}");
            }
        }

        private static string BuildUnlockAnnouncement(string source, string headerContent, int headerLevel,
            string cardName, string relicName, string featureTitle)
        {
            var sb = new StringBuilder();

            switch (source)
            {
                case "ClanLevelUp":
                    sb.Append($"{headerContent} reached level {headerLevel}! ");
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;

                case "NewClan":
                    sb.Append($"New clan unlocked: {featureTitle ?? headerContent}! ");
                    break;

                case "CovenantUnlocked":
                    sb.Append($"Covenant mode unlocked! {featureTitle}");
                    break;

                case "ChallengeLevelUp":
                    sb.Append($"New covenant level unlocked! {featureTitle}");
                    break;

                case "CardMastery":
                    sb.Append("Card mastery achieved! ");
                    break;

                case "DivineCardMastery":
                    sb.Append("Divine card mastery achieved! ");
                    break;

                case "FeatureUnlocked":
                    sb.Append($"Feature unlocked: {featureTitle}! ");
                    break;

                case "MasteryCardFrameUnlocked":
                    sb.Append("Mastery card frame unlocked! ");
                    break;

                default:
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    else if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    else if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;
            }

            sb.Append("Press Enter to continue.");
            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// Announce key mapping screen
    /// </summary>
    public static class KeyMappingScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetNames = new[] { "KeyMappingScreen", "KeyBindingsScreen", "ControlsScreen", "InputMappingScreen" };
                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Setup");
                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(KeyMappingScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }
                MonsterTrainAccessibility.LogInfo("KeyMappingScreen not found");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch KeyMappingScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(GameScreen.KeyMapping);
                MonsterTrainAccessibility.ScreenReader?.Speak("Key Mapping. Use arrows to navigate.", false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in KeyMappingScreen patch: {ex.Message}");
            }
        }
    }
}
