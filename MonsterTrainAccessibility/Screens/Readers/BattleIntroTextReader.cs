using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from battle intro screens, boss details, and scenarios.
    /// </summary>
    public static class BattleIntroTextReader
    {
        /// <summary>
        /// Get battle info when on the Fight button of BattleIntro screen
        /// </summary>
        public static string GetBattleIntroText(GameObject go)
        {
            try
            {
                // Check if this is the Fight button
                string goName = go.name.ToLower();
                if (!goName.Contains("fight"))
                    return null;

                // Look for BattleIntroScreen component in parents
                Component battleIntroScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "BattleIntroScreen")
                        {
                            battleIntroScreen = component;
                            break;
                        }
                    }
                    if (battleIntroScreen != null) break;
                    current = current.parent;
                }

                if (battleIntroScreen == null)
                    return null;

                // Try to get the scenario/battle info from BattleIntroScreen
                var screenType = battleIntroScreen.GetType();

                // Log all fields on BattleIntroScreen to find scenario data
                LogScreenFields(screenType, battleIntroScreen);

                // Try to find scenario-specific text - look for labels that might contain wave info
                string scenarioName = null;
                string scenarioDescription = null;
                string battleMetadata = null;

                // Try to get ScenarioData from BattleIntroScreen
                scenarioName = GetScenarioNameFromScreen(battleIntroScreen, screenType);
                if (!string.IsNullOrEmpty(scenarioName))
                {
                    // Also try to get description and metadata from ScenarioData
                    scenarioDescription = GetScenarioDescriptionFromScreen(battleIntroScreen, screenType);
                    battleMetadata = GetBattleMetadataFromScreen(battleIntroScreen, screenType);
                }

                // If we found a scenario name, use it
                if (!string.IsNullOrEmpty(scenarioName))
                {
                    var sb = new StringBuilder();
                    sb.Append("Fight: ");

                    // Add battle type/metadata first if available
                    if (!string.IsNullOrEmpty(battleMetadata))
                    {
                        sb.Append($"{battleMetadata} - ");
                    }

                    sb.Append(scenarioName);

                    if (!string.IsNullOrEmpty(scenarioDescription))
                    {
                        sb.Append($". {scenarioDescription}");
                    }

                    return sb.ToString();
                }

                // Fallback to battleNameLabel if no scenario-specific name found
                string battleName = null;
                var nameField = screenType.GetField("battleNameLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nameField != null)
                {
                    var nameLabel = nameField.GetValue(battleIntroScreen);
                    if (nameLabel != null)
                    {
                        battleName = UITextHelper.GetTextFromComponent(nameLabel);
                    }
                }

                if (!string.IsNullOrEmpty(battleName))
                {
                    return $"Fight: {battleName}";
                }

                // Fallback to enemy names if we couldn't get scenario info
                string enemyNames = MapTextReader.GetEnemyNamesFromSiblings(go);
                if (!string.IsNullOrEmpty(enemyNames))
                {
                    return $"Fight: {enemyNames}";
                }

                // Fallback - at least indicate it's a battle
                return "Fight: Start Battle";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle intro text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get text for RunOpeningScreen (Boss Battles screen shown at start of run)
        /// </summary>
        public static string GetRunOpeningScreenText(GameObject go)
        {
            try
            {
                // Look for RunOpeningScreen component in parents
                Component runOpeningScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "RunOpeningScreen")
                        {
                            runOpeningScreen = component;
                            break;
                        }
                    }
                    if (runOpeningScreen != null) break;
                    current = current.parent;
                }

                if (runOpeningScreen == null)
                    return null;

                var screenType = runOpeningScreen.GetType();
                MonsterTrainAccessibility.LogInfo($"Found RunOpeningScreen component");

                // Build the boss battles text from bossDetailsUIs
                var sb = new StringBuilder();
                sb.Append("Boss Battles. ");

                // Get bossDetailsUIs field - List<BossDetailsUI>
                var bossDetailsField = screenType.GetField("bossDetailsUIs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"bossDetailsField found: {bossDetailsField != null}");
                if (bossDetailsField != null)
                {
                    var bossDetailsList = bossDetailsField.GetValue(runOpeningScreen) as System.Collections.IList;
                    MonsterTrainAccessibility.LogInfo($"bossDetailsList count: {bossDetailsList?.Count ?? 0}");
                    if (bossDetailsList != null && bossDetailsList.Count > 0)
                    {
                        for (int i = 0; i < bossDetailsList.Count; i++)
                        {
                            var bossDetailsUI = bossDetailsList[i];
                            if (bossDetailsUI == null) continue;

                            MonsterTrainAccessibility.LogInfo($"Processing BossDetailsUI[{i}], type: {bossDetailsUI.GetType().Name}");

                            // Log all fields of BossDetailsUI
                            var uiType = bossDetailsUI.GetType();
                            MonsterTrainAccessibility.LogInfo($"BossDetailsUI fields:");
                            foreach (var field in uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                try
                                {
                                    var val = field.GetValue(bossDetailsUI);
                                    MonsterTrainAccessibility.LogInfo($"  {field.Name} = {val?.GetType().Name ?? "null"}");
                                }
                                catch { }
                            }

                            string bossInfo = GetBossDetailsUIText(bossDetailsUI);
                            MonsterTrainAccessibility.LogInfo($"BossDetailsUI[{i}] text: '{bossInfo}'");
                            if (!string.IsNullOrEmpty(bossInfo))
                            {
                                sb.Append(bossInfo);
                                if (i < bossDetailsList.Count - 1)
                                    sb.Append(". ");
                            }
                        }

                        string result = sb.ToString().Trim();
                        MonsterTrainAccessibility.LogInfo($"Final boss battles text: '{result}'");
                        if (result.Length > 15) // More than just "Boss Battles. "
                        {
                            // Add button hint
                            sb.Append(". Press Enter to confirm.");
                            return sb.ToString();
                        }
                    }
                }

                // Fallback - try to get text from children
                var screenGo = (runOpeningScreen as MonoBehaviour)?.gameObject;
                if (screenGo != null)
                {
                    var texts = UITextHelper.GetAllTextFromChildren(screenGo);
                    if (texts != null && texts.Count > 0)
                    {
                        var meaningfulTexts = texts.Where(t =>
                            !string.IsNullOrWhiteSpace(t) &&
                            t.Length > 2 &&
                            !t.Equals("OK", StringComparison.OrdinalIgnoreCase) &&
                            !t.Equals("Confirm", StringComparison.OrdinalIgnoreCase) &&
                            !t.ToLower().Contains("placeholder")
                        ).ToList();

                        if (meaningfulTexts.Count > 0)
                        {
                            return "Boss Battles. " + string.Join(". ", meaningfulTexts) + ". Press Enter to confirm.";
                        }
                    }
                }

                return "Boss Battles. Press Enter to confirm.";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting run opening screen text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract text from a BossDetailsUI component
        /// </summary>
        public static string GetBossDetailsUIText(object bossDetailsUI)
        {
            if (bossDetailsUI == null) return null;

            try
            {
                var uiType = bossDetailsUI.GetType();
                var sb = new StringBuilder();

                // Get the title (Ring X: or Final Boss:) from titleLabel
                var titleField = uiType.GetField("titleLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField != null)
                {
                    var labelObj = titleField.GetValue(bossDetailsUI);
                    if (labelObj != null)
                    {
                        string titleText = UITextHelper.GetTMPTextFromObject(labelObj);
                        if (!string.IsNullOrEmpty(titleText) && !titleText.ToLower().Contains("placeholder"))
                        {
                            sb.Append(titleText);
                        }
                    }
                }

                // Get the boss name from tooltipProvider
                var tooltipField = uiType.GetField("tooltipProvider", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipField != null)
                {
                    var tooltipProvider = tooltipField.GetValue(bossDetailsUI);
                    if (tooltipProvider != null)
                    {
                        string bossName = GetBossNameFromTooltip(tooltipProvider);
                        MonsterTrainAccessibility.LogInfo($"Boss name from tooltip: '{bossName}'");
                        if (!string.IsNullOrEmpty(bossName) && !bossName.ToLower().Contains("placeholder"))
                        {
                            if (sb.Length > 0) sb.Append(" ");
                            sb.Append(bossName);
                        }
                    }
                }

                string result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"BossDetailsUI final text: '{result}'");
                return !string.IsNullOrEmpty(result) ? result : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss details UI text: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extract boss name from TooltipProviderComponent
        /// </summary>
        public static string GetBossNameFromTooltip(object tooltipProvider)
        {
            if (tooltipProvider == null) return null;

            try
            {
                var tooltipType = tooltipProvider.GetType();

                // Log all fields to see what's available
                MonsterTrainAccessibility.LogInfo($"TooltipProvider type: {tooltipType.Name}");
                foreach (var field in tooltipType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var val = field.GetValue(tooltipProvider);
                        string valStr = val?.ToString() ?? "null";
                        if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                        MonsterTrainAccessibility.LogInfo($"  Tooltip.{field.Name} = {valStr}");
                    }
                    catch { }
                }

                // Try common tooltip title field names
                string[] titleFieldNames = { "tooltipTitleKey", "_tooltipTitleKey", "titleKey", "title", "tooltipTitle" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var val = field.GetValue(tooltipProvider);
                        if (val is string key && !string.IsNullOrEmpty(key))
                        {
                            // Try to localize the key
                            string localized = LocalizationHelper.TryLocalize(key);
                            if (!string.IsNullOrEmpty(localized) && !localized.Contains("_") && !localized.Contains("-"))
                            {
                                return localized;
                            }
                            // If localization fails, return the key if it looks like a name
                            if (!key.Contains("_") && !key.Contains("-"))
                            {
                                return key;
                            }
                        }
                    }
                }

                // Try to get tooltips list/array
                var tooltipsField = tooltipType.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                   tooltipType.GetField("tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField != null)
                {
                    var tooltips = tooltipsField.GetValue(tooltipProvider);
                    if (tooltips is System.Collections.IList list && list.Count > 0)
                    {
                        var firstTooltip = list[0];
                        if (firstTooltip != null)
                        {
                            // Try to get title from the tooltip data
                            var ttType = firstTooltip.GetType();
                            var ttTitleField = ttType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                              ttType.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (ttTitleField != null)
                            {
                                var title = ttTitleField.GetValue(firstTooltip) as string;
                                if (!string.IsNullOrEmpty(title))
                                {
                                    string localized = LocalizationHelper.TryLocalize(title);
                                    return !string.IsNullOrEmpty(localized) ? localized : title;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss name from tooltip: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the scenario name from BattleIntroScreen's ScenarioData or SaveManager
        /// </summary>
        public static string GetScenarioNameFromScreen(object screen, Type screenType)
        {
            try
            {
                // First try to get SaveManager and get scenario from there
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found SaveManager of type {saveManager.GetType().Name}");
                        string name = GetScenarioNameFromSaveManager(saveManager);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                // Look for scenario-related fields
                string[] scenarioFieldNames = { "scenarioData", "_scenarioData", "scenario", "_scenario",
                    "currentScenario", "_currentScenario", "battleData", "_battleData" };

                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = screenType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(screen);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario field: {fieldName} of type {scenarioData.GetType().Name}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Also check properties
                string[] scenarioPropNames = { "ScenarioData", "Scenario", "CurrentScenario", "BattleData" };
                foreach (var propName in scenarioPropNames)
                {
                    var prop = screenType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var scenarioData = prop.GetValue(screen);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario property: {propName} of type {scenarioData.GetType().Name}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario name from screen: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the scenario name from SaveManager's run state
        /// </summary>
        public static string GetScenarioNameFromSaveManager(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Log SaveManager fields/methods for debugging
                var methods = saveManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var scenarioMethods = methods.Where(m => m.Name.Contains("Scenario") || m.Name.Contains("Battle") || m.Name.Contains("Wave")).ToList();
                MonsterTrainAccessibility.LogInfo($"SaveManager scenario-related methods: {string.Join(", ", scenarioMethods.Select(m => m.Name))}");

                // Try GetCurrentScenarioData method
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Got ScenarioData from GetCurrentScenarioData(): {scenarioData.GetType().Name}");
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
                        MonsterTrainAccessibility.LogInfo($"Got ScenarioData from GetScenario(): {scenarioData.GetType().Name}");
                        string name = GetBattleNameFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                // Try to access run state
                string[] runStateFields = { "runState", "_runState", "currentRun", "_currentRun", "activeRun" };
                foreach (var fieldName in runStateFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var runState = field.GetValue(saveManager);
                        if (runState != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found run state: {fieldName} of type {runState.GetType().Name}");
                            string name = GetScenarioNameFromRunState(runState);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Try GetBalanceData for current scenario info
                var getBalanceDataMethod = saveManagerType.GetMethod("GetBalanceData", BindingFlags.Public | BindingFlags.Instance);
                if (getBalanceDataMethod != null && getBalanceDataMethod.GetParameters().Length == 0)
                {
                    var balanceData = getBalanceDataMethod.Invoke(saveManager, null);
                    if (balanceData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Got BalanceData: {balanceData.GetType().Name}");
                        // BalanceData might have scenario info
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario from SaveManager: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get scenario name from run state object
        /// </summary>
        public static string GetScenarioNameFromRunState(object runState)
        {
            try
            {
                var runStateType = runState.GetType();
                MonsterTrainAccessibility.LogInfo($"RunState type: {runStateType.Name}");

                // Log fields for debugging
                var fields = runStateType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var scenarioFields = fields.Where(f => f.Name.ToLower().Contains("scenario") || f.Name.ToLower().Contains("battle") || f.Name.ToLower().Contains("wave")).ToList();
                MonsterTrainAccessibility.LogInfo($"RunState scenario-related fields: {string.Join(", ", scenarioFields.Select(f => f.Name))}");

                // Try to get current scenario
                string[] scenarioFieldNames = { "currentScenario", "_currentScenario", "scenario", "_scenario", "battleScenario" };
                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = runStateType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(runState);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario in run state: {fieldName}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Try GetScenario method on run state
                var getScenarioMethod = runStateType.GetMethod("GetScenario", BindingFlags.Public | BindingFlags.Instance);
                if (getScenarioMethod != null && getScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getScenarioMethod.Invoke(runState, null);
                    if (scenarioData != null)
                    {
                        string name = GetBattleNameFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario from run state: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the battle name from a ScenarioData object
        /// </summary>
        public static string GetBattleNameFromScenario(object scenarioData)
        {
            try
            {
                var dataType = scenarioData.GetType();
                MonsterTrainAccessibility.LogInfo($"ScenarioData type: {dataType.Name}");

                // Log fields for debugging
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"ScenarioData fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try GetBattleName method first
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got battle name via GetBattleName(): {name}");
                        return name;
                    }
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got battle name via GetName(): {name}");
                        return name;
                    }
                }

                // Try battleNameKey field
                string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = LocalizationHelper.Localize(key);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got battle name from {fieldName}: {localized}");
                                return localized;
                            }
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
        /// Get the scenario description from BattleIntroScreen's ScenarioData or SaveManager
        /// </summary>
        public static string GetScenarioDescriptionFromScreen(object screen, Type screenType)
        {
            try
            {
                // First try to get SaveManager and get scenario description from there
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        string desc = GetScenarioDescriptionFromSaveManager(saveManager);
                        if (!string.IsNullOrEmpty(desc))
                            return desc;
                    }
                }

                // Look for scenario-related fields
                string[] scenarioFieldNames = { "scenarioData", "_scenarioData", "scenario", "_scenario" };

                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = screenType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(screen);
                        if (scenarioData != null)
                        {
                            return GetBattleDescriptionFromScenario(scenarioData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario description: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get battle metadata (type, difficulty, ring, etc.) from screen
        /// </summary>
        public static string GetBattleMetadataFromScreen(object screen, Type screenType)
        {
            try
            {
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField == null) return null;

                var saveManager = saveManagerField.GetValue(screen);
                if (saveManager == null) return null;

                var parts = new List<string>();

                // Get current ring/floor
                string ringInfo = GetCurrentRingInfo(saveManager);
                if (!string.IsNullOrEmpty(ringInfo))
                {
                    parts.Add(ringInfo);
                }

                // Get battle type (boss, elite, normal)
                string battleType = GetBattleType(saveManager, screen, screenType);
                if (!string.IsNullOrEmpty(battleType))
                {
                    parts.Add(battleType);
                }

                // Get difficulty info from scenario
                string difficultyInfo = GetScenarioDifficulty(saveManager);
                if (!string.IsNullOrEmpty(difficultyInfo))
                {
                    parts.Add(difficultyInfo);
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle metadata: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get current ring/floor number
        /// </summary>
        public static string GetCurrentRingInfo(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try GetCurrentRing, GetRing, GetFloor, etc.
                string[] ringMethods = { "GetCurrentRing", "GetRing", "GetCurrentFloor", "GetFloor", "GetCurrentLevel" };
                foreach (var methodName in ringMethods)
                {
                    var method = saveManagerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        var result = method.Invoke(saveManager, null);
                        if (result != null)
                        {
                            int ring = Convert.ToInt32(result);
                            // Monster Train has rings 1-8 typically
                            if (ring >= 0 && ring <= 10)
                            {
                                MonsterTrainAccessibility.LogInfo($"Got ring from {methodName}: {ring}");
                                return $"Ring {ring + 1}"; // Convert 0-based to 1-based
                            }
                        }
                    }
                }

                // Try fields
                string[] ringFields = { "currentRing", "_currentRing", "ring", "_ring", "currentFloor", "_currentFloor" };
                foreach (var fieldName in ringFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(saveManager);
                        if (value != null)
                        {
                            int ring = Convert.ToInt32(value);
                            if (ring >= 0 && ring <= 10)
                            {
                                MonsterTrainAccessibility.LogInfo($"Got ring from field {fieldName}: {ring}");
                                return $"Ring {ring + 1}";
                            }
                        }
                    }
                }

                // Try to get from RunState
                var runStateField = saveManagerType.GetField("runState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (runStateField != null)
                {
                    var runState = runStateField.GetValue(saveManager);
                    if (runState != null)
                    {
                        var runStateType = runState.GetType();
                        foreach (var methodName in ringMethods)
                        {
                            var method = runStateType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                            if (method != null && method.GetParameters().Length == 0)
                            {
                                var result = method.Invoke(runState, null);
                                if (result != null)
                                {
                                    int ring = Convert.ToInt32(result);
                                    if (ring >= 0 && ring <= 10)
                                    {
                                        return $"Ring {ring + 1}";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting ring info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Determine battle type (Boss, Elite, Normal, etc.)
        /// </summary>
        public static string GetBattleType(object saveManager, object screen, Type screenType)
        {
            try
            {
                // Check if this is a boss battle by looking at bigBossDisplay visibility
                // The bigBossDisplay is only visible/active during actual boss fights
                var bigBossField = screenType.GetField("bigBossDisplay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (bigBossField != null)
                {
                    var bigBoss = bigBossField.GetValue(screen);
                    if (bigBoss != null && bigBoss is Component comp)
                    {
                        // Check if the boss display GameObject is active
                        if (comp.gameObject.activeInHierarchy)
                        {
                            MonsterTrainAccessibility.LogInfo("Boss display is active - this is a boss battle");
                            return "Boss Battle";
                        }
                        else
                        {
                            MonsterTrainAccessibility.LogInfo("Boss display exists but is not active - not a boss battle");
                        }
                    }
                }

                // Check ScenarioData for more info
                var saveManagerType = saveManager.GetType();
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        var scenarioType = scenarioData.GetType();

                        // Check if there's a GetIsBoss or IsBossBattle method
                        var isBossMethod = scenarioType.GetMethod("GetIsBoss", BindingFlags.Public | BindingFlags.Instance) ??
                                          scenarioType.GetMethod("IsBossBattle", BindingFlags.Public | BindingFlags.Instance);
                        if (isBossMethod != null && isBossMethod.GetParameters().Length == 0)
                        {
                            var isBoss = isBossMethod.Invoke(scenarioData, null);
                            if (isBoss is bool b && b)
                            {
                                return "Boss Battle";
                            }
                        }

                        // Check difficulty field - higher values might indicate harder battles
                        var difficultyField = scenarioType.GetField("difficulty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (difficultyField != null)
                        {
                            var difficulty = difficultyField.GetValue(scenarioData);
                            if (difficulty != null)
                            {
                                int diffValue = Convert.ToInt32(difficulty);
                                MonsterTrainAccessibility.LogInfo($"Scenario difficulty: {diffValue}");
                                // Could indicate elite/hard battle at certain thresholds
                            }
                        }

                        // Log bossVariant for reference (but don't use it to determine boss status)
                        var bossVariantField = scenarioType.GetField("bossVariant", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (bossVariantField != null)
                        {
                            var bossVariant = bossVariantField.GetValue(scenarioData);
                            if (bossVariant != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Ring boss pool: {bossVariant} (not current battle type)");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle type: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get difficulty info from current scenario
        /// </summary>
        public static string GetScenarioDifficulty(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try to get covenant level (difficulty modifier)
                var getCovenantMethod = saveManagerType.GetMethod("GetCovenantLevel", BindingFlags.Public | BindingFlags.Instance);
                if (getCovenantMethod == null)
                {
                    getCovenantMethod = saveManagerType.GetMethod("GetAscensionLevel", BindingFlags.Public | BindingFlags.Instance);
                }
                if (getCovenantMethod != null && getCovenantMethod.GetParameters().Length == 0)
                {
                    var covenant = getCovenantMethod.Invoke(saveManager, null);
                    if (covenant != null)
                    {
                        int level = Convert.ToInt32(covenant);
                        if (level > 0)
                        {
                            MonsterTrainAccessibility.LogInfo($"Covenant level: {level}");
                            return $"Covenant {level}";
                        }
                    }
                }

                // Check fields
                string[] covenantFields = { "covenantLevel", "_covenantLevel", "ascensionLevel", "_ascensionLevel" };
                foreach (var fieldName in covenantFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(saveManager);
                        if (value != null)
                        {
                            int level = Convert.ToInt32(value);
                            if (level > 0)
                            {
                                return $"Covenant {level}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario difficulty: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the scenario description from SaveManager's current scenario
        /// </summary>
        public static string GetScenarioDescriptionFromSaveManager(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try GetCurrentScenarioData method
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        string desc = GetBattleDescriptionFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(desc))
                        {
                            MonsterTrainAccessibility.LogInfo($"Got battle description: {desc}");
                            return desc;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario description from SaveManager: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the battle description from a ScenarioData object
        /// </summary>
        public static string GetBattleDescriptionFromScenario(object scenarioData)
        {
            try
            {
                var dataType = scenarioData.GetType();

                // Try GetBattleDescription method
                var getDescMethod = dataType.GetMethod("GetBattleDescription", BindingFlags.Public | BindingFlags.Instance);
                if (getDescMethod != null && getDescMethod.GetParameters().Length == 0)
                {
                    var result = getDescMethod.Invoke(scenarioData, null);
                    if (result is string desc && !string.IsNullOrEmpty(desc))
                    {
                        return desc;
                    }
                }

                // Try battleDescriptionKey field
                string[] descFieldNames = { "battleDescriptionKey", "_battleDescriptionKey", "descriptionKey", "_descriptionKey" };
                foreach (var fieldName in descFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = LocalizationHelper.Localize(key);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                return localized;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle description: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract battle name and description from ScenarioData
        /// </summary>
        public static string GetBattleNameAndDescription(object scenarioData)
        {
            if (scenarioData == null) return null;

            try
            {
                var dataType = scenarioData.GetType();
                string battleName = null;
                string battleDescription = null;

                // Debug: log fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Scenario fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try to get battle name
                // Method: GetBattleName()
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        battleName = name;
                        MonsterTrainAccessibility.LogInfo($"Got battle name from GetBattleName(): {battleName}");
                    }
                }

                // Try field: battleNameKey
                if (string.IsNullOrEmpty(battleName))
                {
                    string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey", "titleKey", "_titleKey" };
                    foreach (var fieldName in nameFieldNames)
                    {
                        var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var key = field.GetValue(scenarioData) as string;
                            if (!string.IsNullOrEmpty(key))
                            {
                                string localized = LocalizationHelper.Localize(key);
                                if (!string.IsNullOrEmpty(localized))
                                {
                                    battleName = localized;
                                    MonsterTrainAccessibility.LogInfo($"Got battle name from {fieldName}: {battleName}");
                                    break;
                                }
                            }
                        }
                    }
                }

                // Try to get battle description
                // Method: GetBattleDescription()
                var getBattleDescMethod = dataType.GetMethod("GetBattleDescription", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleDescMethod != null && getBattleDescMethod.GetParameters().Length == 0)
                {
                    var result = getBattleDescMethod.Invoke(scenarioData, null);
                    if (result is string desc && !string.IsNullOrEmpty(desc))
                    {
                        battleDescription = desc;
                        MonsterTrainAccessibility.LogInfo($"Got battle description from GetBattleDescription(): {battleDescription}");
                    }
                }

                // Try field: battleDescriptionKey
                if (string.IsNullOrEmpty(battleDescription))
                {
                    string[] descFieldNames = { "battleDescriptionKey", "_battleDescriptionKey", "descriptionKey", "_descriptionKey" };
                    foreach (var fieldName in descFieldNames)
                    {
                        var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var key = field.GetValue(scenarioData) as string;
                            if (!string.IsNullOrEmpty(key))
                            {
                                string localized = LocalizationHelper.Localize(key);
                                if (!string.IsNullOrEmpty(localized))
                                {
                                    battleDescription = localized;
                                    MonsterTrainAccessibility.LogInfo($"Got battle description from {fieldName}: {battleDescription}");
                                    break;
                                }
                            }
                        }
                    }
                }

                // Build the result
                if (!string.IsNullOrEmpty(battleName))
                {
                    if (!string.IsNullOrEmpty(battleDescription))
                    {
                        return $"Fight: {battleName}. {battleDescription}";
                    }
                    return $"Fight: {battleName}";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle name/description: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the name of a battle node from ScenarioData
        /// </summary>
        public static string GetBattleNodeName(object scenarioData, Type dataType)
        {
            try
            {
                // Try battleNameKey field
                var battleNameField = dataType.GetField("battleNameKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (battleNameField == null)
                {
                    battleNameField = dataType.GetField("_battleNameKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                string battleNameKey = null;
                if (battleNameField != null)
                {
                    battleNameKey = battleNameField.GetValue(scenarioData) as string;
                }

                // Also try GetBattleName method
                if (string.IsNullOrEmpty(battleNameKey))
                {
                    var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                    if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                    {
                        var result = getBattleNameMethod.Invoke(scenarioData, null);
                        if (result is string name && !string.IsNullOrEmpty(name))
                        {
                            return "Battle: " + name;
                        }
                    }
                }

                // Localize the key if found
                if (!string.IsNullOrEmpty(battleNameKey))
                {
                    string localized = LocalizationHelper.Localize(battleNameKey);
                    if (!string.IsNullOrEmpty(localized))
                    {
                        return "Battle: " + localized;
                    }
                }

                // Fallback to GetName or name property
                return GetFallbackNodeName(scenarioData, dataType, "Battle");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle node name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the name of a generic node (reward, merchant, event, etc.)
        /// </summary>
        public static string GetGenericNodeName(object nodeData, Type dataType)
        {
            try
            {
                // Special-case RewardNodeData: prefer name + linked clan over the generic
                // tooltipTitleKey, which often resolves to "Merchant of Magic" for unit-pack
                // banner nodes regardless of clan.
                if (dataType.Name == "RewardNodeData" || dataType.BaseType?.Name == "RewardNodeData")
                {
                    string rewardNodeText = ExtractRewardNodeName(nodeData, dataType);
                    if (!string.IsNullOrEmpty(rewardNodeText))
                        return rewardNodeText;
                }

                // Try tooltipTitleKey field
                var titleField = dataType.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField == null)
                {
                    titleField = dataType.GetField("_tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                string titleKey = null;
                if (titleField != null)
                {
                    titleKey = titleField.GetValue(nodeData) as string;
                }

                // Localize the key if found
                if (!string.IsNullOrEmpty(titleKey))
                {
                    string localized = LocalizationHelper.Localize(titleKey);
                    if (!string.IsNullOrEmpty(localized))
                    {
                        return localized;
                    }
                }

                // Determine node type for prefix
                string typeName = dataType.Name;
                string prefix = "";
                if (typeName.Contains("Merchant") || typeName.Contains("Shop"))
                    prefix = "Shop";
                else if (typeName.Contains("Event"))
                    prefix = "Event";
                else if (typeName.Contains("Reward"))
                    prefix = "Reward";

                return GetFallbackNodeName(nodeData, dataType, prefix);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting generic node name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Build a descriptive name for a RewardNodeData. Pulls the linked clan via
        /// requiredClass.GetTitle() and recognizes unit-pack banner nodes by asset name.
        /// Returns null if we can't make something better than the generic fallback.
        /// </summary>
        private static string ExtractRewardNodeName(object nodeData, Type dataType)
        {
            try
            {
                string clanName = null;
                var requiredClassField = dataType.GetField("requiredClass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (requiredClassField != null)
                {
                    var classData = requiredClassField.GetValue(nodeData);
                    if (classData != null)
                    {
                        var getTitleMethod = classData.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                        if (getTitleMethod != null)
                        {
                            clanName = getTitleMethod.Invoke(classData, null) as string;
                            if (!string.IsNullOrEmpty(clanName))
                                clanName = TextUtilities.StripRichTextTags(clanName);
                        }
                    }
                }

                string assetName = null;
                var nameProp = dataType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null)
                    assetName = nameProp.GetValue(nodeData) as string;

                bool isUnitPack = !string.IsNullOrEmpty(assetName) &&
                    assetName.IndexOf("UnitPack", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isUnitPack)
                {
                    return string.IsNullOrEmpty(clanName)
                        ? "Clan banner"
                        : $"{clanName} clan banner";
                }

                // Other reward nodes: get the localized title and append clan when known
                string title = null;
                var titleField = dataType.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField != null)
                {
                    var titleKey = titleField.GetValue(nodeData) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                        title = LocalizationHelper.Localize(titleKey);
                }

                if (string.IsNullOrEmpty(title))
                    return null;

                return string.IsNullOrEmpty(clanName) ? title : $"{title} ({clanName})";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ExtractRewardNodeName error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try fallback methods to get node name (GetName, name property, etc.)
        /// </summary>
        public static string GetFallbackNodeName(object nodeData, Type dataType, string prefix)
        {
            try
            {
                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(nodeData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
                    }
                }

                // Try name property
                var nameProp = dataType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null)
                {
                    var result = nameProp.GetValue(nodeData);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        // Clean up asset names (remove underscores, etc.)
                        name = TextUtilities.CleanAssetName(name);
                        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Find the scenario/wave name in children of BattleIntroScreen
        /// The battleNameLabel shows the boss name, but we want the wave/scenario name
        /// </summary>
        public static string FindScenarioTextInChildren(Transform root)
        {
            try
            {
                // Look for common patterns that might contain the scenario name
                // Often there's a "waveText", "scenarioText", "encounterText" or similar
                string[] namePatternsToFind = { "wave", "scenario", "encounter", "mission", "stage", "title" };
                string[] namePatternsToExclude = { "boss", "champion" };

                // Collect all text labels with their names
                var textLabels = new Dictionary<string, string>();
                CollectTextLabels(root, textLabels);

                // Log what we found
                foreach (var kvp in textLabels)
                {
                    MonsterTrainAccessibility.LogInfo($"Label [{kvp.Key}]: \"{kvp.Value}\"");
                }

                // First, try to find by label name patterns
                foreach (var pattern in namePatternsToFind)
                {
                    foreach (var kvp in textLabels)
                    {
                        string labelName = kvp.Key.ToLower();
                        if (labelName.Contains(pattern))
                        {
                            // Make sure it's not an excluded pattern
                            bool excluded = false;
                            foreach (var excludePattern in namePatternsToExclude)
                            {
                                if (labelName.Contains(excludePattern))
                                {
                                    excluded = true;
                                    break;
                                }
                            }

                            if (!excluded && !string.IsNullOrEmpty(kvp.Value))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found scenario name via pattern '{pattern}': {kvp.Value}");
                                return kvp.Value;
                            }
                        }
                    }
                }

                // If no pattern match, try to find by looking for text that's NOT the boss name
                // The boss name typically appears in "battleNameLabel" or similar
                // Look for another substantial text that might be the wave name
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding scenario text: {ex.Message}");
            }

            return null;
        }

        public static void CollectTextLabels(Transform node, Dictionary<string, string> labels)
        {
            if (node == null) return;

            // Get text from this node
            string text = GetTMPTextDirect(node.gameObject);
            if (string.IsNullOrEmpty(text))
            {
                var uiText = node.GetComponent<Text>();
                text = uiText?.text;
            }

            if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
            {
                labels[node.name] = text.Trim();
            }

            // Recurse to children
            foreach (Transform child in node)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    CollectTextLabels(child, labels);
                }
            }
        }

        /// <summary>
        /// Log all fields on a screen type for debugging
        /// </summary>
        public static void LogScreenFields(Type screenType, object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== Fields on {screenType.Name} ===");

                var fields = screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(screen);
                        string valueStr = value?.ToString() ?? "null";
                        // Truncate long values
                        if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                        sb.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging screen fields: {ex.Message}");
            }
        }

        /// <summary>
        /// Special handling for the Trial toggle on BattleIntroScreen
        /// Returns full trial info: name, description, reward, and toggle state
        /// </summary>
        public static string GetTrialToggleText(GameObject go)
        {
            try
            {
                // Check if this might be a trial toggle by looking at hierarchy
                bool isBattleIntroToggle = false;
                Component battleIntroScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "BattleIntroScreen")
                        {
                            battleIntroScreen = component;
                            isBattleIntroToggle = true;
                            break;
                        }
                    }
                    if (isBattleIntroToggle) break;
                    current = current.parent;
                }

                if (!isBattleIntroToggle || battleIntroScreen == null)
                    return null;

                // Get the BattleIntroScreen's trial data
                var screenType = battleIntroScreen.GetType();

                // Get trialEnabled field
                var trialEnabledField = screenType.GetField("trialEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                bool trialEnabled = false;
                if (trialEnabledField != null)
                {
                    var val = trialEnabledField.GetValue(battleIntroScreen);
                    if (val is bool b) trialEnabled = b;
                }

                // Get trialData field
                var trialDataField = screenType.GetField("trialData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                object trialData = trialDataField?.GetValue(battleIntroScreen);

                if (trialData == null)
                    return null;

                // Extract trial information
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

                        // Prefer RelicData.GetDescription() — it localizes with a
                        // CardEffectLocalizationContext that resolves {[effect*.power]}
                        // placeholders and sprite substitutions. GetDescriptionKey() +
                        // raw Localize() leaves placeholders unresolved, which produced
                        // broken output like "Pyre Dampener. per turn.".
                        var getDescMethod = sinType.GetMethod("GetDescription", Type.EmptyTypes);
                        if (getDescMethod != null)
                        {
                            ruleDescription = getDescMethod.Invoke(sinData, null) as string;
                        }
                        if (string.IsNullOrEmpty(ruleDescription))
                        {
                            var getDescKeyMethod = sinType.GetMethod("GetDescriptionKey");
                            if (getDescKeyMethod != null && getDescKeyMethod.GetParameters().Length == 0)
                            {
                                var descKey = getDescKeyMethod.Invoke(sinData, null) as string;
                                if (!string.IsNullOrEmpty(descKey))
                                    ruleDescription = LocalizationHelper.Localize(descKey);
                            }
                        }
                        if (!string.IsNullOrEmpty(ruleDescription))
                            ruleDescription = TextUtilities.CleanSpriteTagsForSpeech(ruleDescription);
                    }
                }

                // Prefer rewardList — the legacy `reward` field is [HideInInspector] and unused.
                var rewardListField = trialType.GetField("rewardList", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                var rewardList = rewardListField?.GetValue(trialData) as System.Collections.IList;
                if (rewardList != null && rewardList.Count > 0)
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var entry in rewardList)
                    {
                        if (entry == null) continue;
                        var n = ShopTextReader.GetRewardName(entry);
                        if (!string.IsNullOrEmpty(n)) names.Add(n);
                    }
                    if (names.Count > 0)
                        rewardName = string.Join(", ", names);
                }
                if (string.IsNullOrEmpty(rewardName))
                {
                    var rewardField = trialType.GetField("reward", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    var rewardData = rewardField?.GetValue(trialData);
                    if (rewardData != null)
                        rewardName = ShopTextReader.GetRewardName(rewardData);
                }

                // Build the announcement
                var sb = new StringBuilder();
                sb.Append("Trial toggle: ");
                sb.Append(trialEnabled ? "ON" : "OFF");
                sb.Append(". ");

                if (trialEnabled)
                {
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
                        sb.Append("You will gain additional reward: ");
                        sb.Append(rewardName);
                        sb.Append(". ");
                    }
                }
                else
                {
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
                        sb.Append("Enable to gain additional reward: ");
                        sb.Append(rewardName);
                        sb.Append(". ");
                    }
                }

                sb.Append("Press Enter to toggle.");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting trial toggle text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Debug: Log all text content in a UI hierarchy
        /// </summary>
        public static void LogAllTextInHierarchy(Transform root, string prefix)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== All text in {prefix} hierarchy ===");
                LogTextRecursive(root, sb, 0);
                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging text hierarchy: {ex.Message}");
            }
        }

        public static void LogTextRecursive(Transform node, StringBuilder sb, int depth)
        {
            if (node == null || depth > 10) return;

            string indent = new string(' ', depth * 2);

            // Get text from this node
            string text = GetTMPTextDirect(node.gameObject);
            if (string.IsNullOrEmpty(text))
            {
                var uiText = node.GetComponent<Text>();
                text = uiText?.text;
            }

            // Log if there's text
            if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
            {
                sb.AppendLine($"{indent}[{node.name}]: \"{text.Trim()}\"");
            }

            // Recurse to children
            foreach (Transform child in node)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    LogTextRecursive(child, sb, depth + 1);
                }
            }
        }


        private static string GetTMPTextDirect(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            return textProperty.GetValue(component) as string;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetTMPText(GameObject go)
        {
            try
            {
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetDirectText(GameObject go)
        {
            string text = GetTMPText(go);
            if (!string.IsNullOrEmpty(text))
                return text.Trim();
            var uiText = go.GetComponentInChildren<UnityEngine.UI.Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                return uiText.text.Trim();
            return null;
        }

        private static string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return name;
        }

    }
}
