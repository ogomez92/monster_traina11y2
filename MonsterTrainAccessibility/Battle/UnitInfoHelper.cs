using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Shared unit introspection methods used by FloorReader and EnemyReader.
    /// Extracts name, HP, attack, team type, status effects from character objects via reflection.
    /// </summary>
    public static class UnitInfoHelper
    {
        public static string GetUnitName(object characterState, BattleManagerCache cache)
        {
            try
            {
                string name = null;

                // Try GetLocName or similar
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName") ??
                                   type.GetMethod("GetLocName") ??
                                   type.GetMethod("GetTitle");
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(characterState, null) as string;
                }

                // Try getting CharacterData and its name
                if (string.IsNullOrEmpty(name))
                {
                    var getDataMethod = type.GetMethod("GetCharacterData");
                    if (getDataMethod != null)
                    {
                        var data = getDataMethod.Invoke(characterState, null);
                        if (data != null && cache.GetCharacterNameMethod != null)
                        {
                            name = cache.GetCharacterNameMethod.Invoke(data, null) as string;
                        }
                    }
                }

                return StripRichTextTags(name) ?? "Unit";
            }
            catch { }
            return "Unit";
        }

        public static int GetUnitHP(object characterState, BattleManagerCache cache)
        {
            try
            {
                if (cache.GetHPMethod == null)
                {
                    var type = characterState.GetType();
                    cache.GetHPMethod = type.GetMethod("GetHP");
                }
                var result = cache.GetHPMethod?.Invoke(characterState, null);
                if (result is int hp) return hp;
            }
            catch { }
            return 0;
        }

        public static int GetUnitMaxHP(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var method = type.GetMethod("GetMaxHP", Type.EmptyTypes);
                if (method != null)
                {
                    var result = method.Invoke(characterState, null);
                    if (result is int hp) return hp;
                }
            }
            catch { }
            return -1;
        }

        public static int GetUnitAttack(object characterState, BattleManagerCache cache)
        {
            try
            {
                if (cache.GetAttackDamageMethod == null)
                {
                    var type = characterState.GetType();
                    cache.GetAttackDamageMethod = type.GetMethod("GetAttackDamage");
                }
                var result = cache.GetAttackDamageMethod?.Invoke(characterState, null);
                if (result is int attack) return attack;
            }
            catch { }
            return 0;
        }

        public static bool IsEnemyUnit(object characterState, BattleManagerCache cache)
        {
            try
            {
                if (cache.GetTeamTypeMethod == null)
                {
                    var type = characterState.GetType();
                    cache.GetTeamTypeMethod = type.GetMethod("GetTeamType");
                }
                var team = cache.GetTeamTypeMethod?.Invoke(characterState, null);
                string teamStr = team?.ToString() ?? "null";
                MonsterTrainAccessibility.LogInfo($"IsEnemyUnit: team = {teamStr}");
                // In Monster Train, "Heroes" are the enemies attacking the train
                return teamStr == "Heroes";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"IsEnemyUnit error: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Get equipment names attached to a unit as a comma-separated string.
        /// Game API: CharacterState.GetEquipment() -> List&lt;CardState&gt;
        /// </summary>
        public static string GetUnitEquipment(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getEquipMethod = type.GetMethod("GetEquipment", Type.EmptyTypes);
                if (getEquipMethod == null) return null;

                var equipList = getEquipMethod.Invoke(characterState, null) as System.Collections.IList;
                if (equipList == null || equipList.Count == 0) return null;

                var names = new List<string>();
                foreach (var equipCard in equipList)
                {
                    if (equipCard == null) continue;
                    var getTitle = equipCard.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                    if (getTitle == null) continue;
                    var title = getTitle.Invoke(equipCard, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        names.Add(StripRichTextTags(title));
                    }
                }
                return names.Count > 0 ? string.Join(", ", names) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting equipment: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get status effects on a unit as a readable string
        /// </summary>
        public static string GetUnitStatusEffects(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetStatusEffects method which takes an out parameter
                var getStatusMethod = type.GetMethods()
                    .FirstOrDefault(m => m.Name == "GetStatusEffects" && m.GetParameters().Length >= 1);

                if (getStatusMethod != null)
                {
                    // Create the list parameter
                    var parameters = getStatusMethod.GetParameters();
                    var listType = parameters[0].ParameterType;

                    // Handle out parameter - need to create array for Invoke
                    var args = new object[parameters.Length];

                    // For out parameters, we pass null and get the value back
                    if (parameters[0].IsOut)
                    {
                        args[0] = null;
                    }
                    else
                    {
                        // Create empty list
                        args[0] = Activator.CreateInstance(listType);
                    }

                    // Fill additional params with defaults
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType == typeof(bool))
                            args[i] = false;
                        else
                            args[i] = parameters[i].ParameterType.IsValueType
                                ? Activator.CreateInstance(parameters[i].ParameterType)
                                : null;
                    }

                    getStatusMethod.Invoke(characterState, args);

                    // The list should now be populated (args[0] for out param)
                    var statusList = args[0] as System.Collections.IList;
                    if (statusList != null && statusList.Count > 0)
                    {
                        var effects = new List<string>();
                        foreach (var statusStack in statusList)
                        {
                            string effectName = GetStatusEffectName(statusStack);
                            int stacks = GetStatusEffectStacks(statusStack);

                            if (!string.IsNullOrEmpty(effectName))
                            {
                                if (stacks > 1)
                                    effects.Add($"{effectName} {stacks}");
                                else
                                    effects.Add(effectName);
                            }
                        }

                        if (effects.Count > 0)
                        {
                            return string.Join(", ", effects);
                        }
                    }
                }

                // Alternative: try to get individual status effects by common IDs
                var commonStatuses = new[] { "armor", "damage shield", "rage", "quick", "multistrike", "regen", "sap", "dazed", "rooted", "spell weakness" };
                var foundEffects = new List<string>();

                var getStacksMethod = type.GetMethod("GetStatusEffectStacks", new[] { typeof(string) });
                if (getStacksMethod != null)
                {
                    foreach (var statusId in commonStatuses)
                    {
                        try
                        {
                            var result = getStacksMethod.Invoke(characterState, new object[] { statusId });
                            if (result is int stacks && stacks > 0)
                            {
                                string displayName = FormatStatusName(statusId);
                                if (stacks > 1)
                                    foundEffects.Add($"{displayName} {stacks}");
                                else
                                    foundEffects.Add(displayName);
                            }
                        }
                        catch { }
                    }

                    if (foundEffects.Count > 0)
                    {
                        return string.Join(", ", foundEffects);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting status effects: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the name of a status effect from a StatusEffectStack
        /// </summary>
        public static string GetStatusEffectName(object statusStack)
        {
            try
            {
                var stackType = statusStack.GetType();

                // Try to get State property which returns StatusEffectState
                var stateProp = stackType.GetProperty("State");
                if (stateProp != null)
                {
                    var state = stateProp.GetValue(statusStack);
                    if (state != null)
                    {
                        var stateType = state.GetType();

                        // Try GetStatusId
                        var getIdMethod = stateType.GetMethod("GetStatusId", Type.EmptyTypes);
                        if (getIdMethod != null)
                        {
                            var id = getIdMethod.Invoke(state, null) as string;
                            if (!string.IsNullOrEmpty(id))
                            {
                                return FormatStatusName(id);
                            }
                        }

                        // Try GetName or similar
                        var getNameMethod = stateType.GetMethod("GetName", Type.EmptyTypes) ??
                                           stateType.GetMethod("GetDisplayName", Type.EmptyTypes);
                        if (getNameMethod != null)
                        {
                            var name = getNameMethod.Invoke(state, null) as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                return StripRichTextTags(name);
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get stack count from a StatusEffectStack
        /// </summary>
        public static int GetStatusEffectStacks(object statusStack)
        {
            try
            {
                var stackType = statusStack.GetType();

                // Try Count property
                var countProp = stackType.GetProperty("Count");
                if (countProp != null)
                {
                    var result = countProp.GetValue(statusStack);
                    if (result is int count) return count;
                }

                // Try count field
                var countField = stackType.GetField("count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (countField != null)
                {
                    var result = countField.GetValue(statusStack);
                    if (result is int count) return count;
                }
            }
            catch { }
            return 1;
        }

        /// <summary>
        /// Format a status effect ID into a readable name
        /// </summary>
        public static string FormatStatusName(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return statusId;

            // Convert snake_case or camelCase to Title Case
            statusId = statusId.Replace("_", " ");
            statusId = System.Text.RegularExpressions.Regex.Replace(statusId, "([a-z])([A-Z])", "$1 $2");

            // Capitalize first letter of each word
            var words = statusId.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        /// <summary>
        /// Get a detailed description of a unit including stats, status effects, and intent
        /// </summary>
        public static string GetDetailedUnitDescription(object unit, BattleManagerCache cache)
        {
            try
            {
                var sb = new StringBuilder();

                // Get basic info
                string name = GetUnitName(unit, cache);
                int hp = GetUnitHP(unit, cache);
                int maxHp = GetUnitMaxHP(unit);
                int attack = GetUnitAttack(unit, cache);

                sb.Append($"{name}: {attack} attack, {hp}");
                if (maxHp > 0 && maxHp != hp)
                {
                    sb.Append($" of {maxHp}");
                }
                sb.Append(" health");

                // Get status effects
                string statusEffects = GetUnitStatusEffects(unit);
                if (!string.IsNullOrEmpty(statusEffects))
                {
                    sb.Append($". Status: {statusEffects}");
                }

                // Get equipment
                string equipment = GetUnitEquipment(unit);
                if (!string.IsNullOrEmpty(equipment))
                {
                    sb.Append($". Equipped: {equipment}");
                }

                // Get intent (for bosses or units with visible intent)
                string intent = GetUnitIntent(unit);
                if (!string.IsNullOrEmpty(intent))
                {
                    sb.Append($". Intent: {intent}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting unit description: {ex.Message}");
                return GetUnitName(unit, cache) ?? "Unknown unit";
            }
        }

        /// <summary>
        /// Get a brief description of a unit for targeting announcements.
        /// </summary>
        public static string GetTargetUnitDescription(object characterState, BattleManagerCache cache)
        {
            if (characterState == null) return null;
            try
            {
                string name = GetUnitName(characterState, cache);
                int hp = GetUnitHP(characterState, cache);
                int maxHp = GetUnitMaxHP(characterState);
                int attack = GetUnitAttack(characterState, cache);

                var sb = new StringBuilder();
                sb.Append($"{name} {attack} attack, {hp}");
                if (maxHp > 0 && maxHp != hp)
                    sb.Append($" of {maxHp}");
                sb.Append(" health");

                string statusEffects = GetUnitStatusEffects(characterState);
                if (!string.IsNullOrEmpty(statusEffects))
                {
                    sb.Append($" ({statusEffects})");
                }

                string equipment = GetUnitEquipment(characterState);
                if (!string.IsNullOrEmpty(equipment))
                {
                    sb.Append($", equipped {equipment}");
                }

                bool isEnemy = IsEnemyUnit(characterState, cache);
                if (isEnemy)
                {
                    string intent = GetUnitIntent(characterState);
                    if (!string.IsNullOrEmpty(intent))
                    {
                        sb.Append($". Intent: {intent}");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting target unit description: {ex.Message}");
                return GetUnitName(characterState, cache) ?? "Unknown unit";
            }
        }

        /// <summary>
        /// Get the intent/action of an enemy (what they will do)
        /// </summary>
        public static string GetUnitIntent(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Check if this is a boss with a BossState
                var getBossStateMethod = type.GetMethod("GetBossState", Type.EmptyTypes);
                if (getBossStateMethod != null)
                {
                    var bossState = getBossStateMethod.Invoke(characterState, null);
                    if (bossState != null)
                    {
                        string bossIntent = GetBossIntent(bossState);
                        if (!string.IsNullOrEmpty(bossIntent))
                        {
                            return bossIntent;
                        }
                    }
                }

                // For regular enemies, try to get their current action/behavior
                var getActionMethod = type.GetMethod("GetCurrentAction", Type.EmptyTypes) ??
                                     type.GetMethod("GetNextAction", Type.EmptyTypes);
                if (getActionMethod != null)
                {
                    var action = getActionMethod.Invoke(characterState, null);
                    if (action != null)
                    {
                        return GetActionDescription(action);
                    }
                }

                // Check attack damage to infer basic intent
                // Use direct reflection here to avoid needing cache parameter
                var getAttackMethod = type.GetMethod("GetAttackDamage", Type.EmptyTypes);
                if (getAttackMethod != null)
                {
                    var result = getAttackMethod.Invoke(characterState, null);
                    if (result is int attack && attack > 0)
                    {
                        return $"Will attack for {attack} damage";
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting unit intent: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the intent of a boss enemy
        /// </summary>
        private static string GetBossIntent(object bossState)
        {
            try
            {
                var bossType = bossState.GetType();

                // Try to get the current action group
                var getActionGroupMethod = bossType.GetMethod("GetCurrentActionGroup", Type.EmptyTypes) ??
                                          bossType.GetMethod("GetActionGroup", Type.EmptyTypes);

                object actionGroup = null;
                if (getActionGroupMethod != null)
                {
                    actionGroup = getActionGroupMethod.Invoke(bossState, null);
                }

                // Try via field if method not found
                if (actionGroup == null)
                {
                    var actionGroupField = bossType.GetField("_actionGroup", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                          bossType.GetField("actionGroup", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (actionGroupField != null)
                    {
                        actionGroup = actionGroupField.GetValue(bossState);
                    }
                }

                if (actionGroup != null)
                {
                    var agType = actionGroup.GetType();

                    // Get next action
                    var getNextActionMethod = agType.GetMethod("GetNextAction", Type.EmptyTypes);
                    if (getNextActionMethod != null)
                    {
                        var nextAction = getNextActionMethod.Invoke(actionGroup, null);
                        if (nextAction != null)
                        {
                            return GetBossActionDescription(nextAction);
                        }
                    }

                    // Get all actions
                    var getActionsMethod = agType.GetMethod("GetActions", Type.EmptyTypes);
                    if (getActionsMethod != null)
                    {
                        var actions = getActionsMethod.Invoke(actionGroup, null) as System.Collections.IList;
                        if (actions != null && actions.Count > 0)
                        {
                            return GetBossActionDescription(actions[0]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss intent: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get description of a boss action
        /// </summary>
        private static string GetBossActionDescription(object bossAction)
        {
            try
            {
                var actionType = bossAction.GetType();
                var parts = new List<string>();

                // Get target room
                var getTargetRoomMethod = actionType.GetMethod("GetTargetedRoomIndex", Type.EmptyTypes);
                if (getTargetRoomMethod != null)
                {
                    var result = getTargetRoomMethod.Invoke(bossAction, null);
                    if (result is int roomIndex && roomIndex >= 0)
                    {
                        string floorName = roomIndex == 0 ? "pyre room" : $"floor {roomIndex}";
                        parts.Add($"targeting {floorName}");
                    }
                }

                // Get effects/damage
                var getEffectsMethod = actionType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod != null)
                {
                    var effects = getEffectsMethod.Invoke(bossAction, null) as System.Collections.IList;
                    if (effects != null && effects.Count > 0)
                    {
                        foreach (var effect in effects)
                        {
                            string effectDesc = GetActionDescription(effect);
                            if (!string.IsNullOrEmpty(effectDesc))
                            {
                                parts.Add(effectDesc);
                                break; // Just get the first meaningful effect
                            }
                        }
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss action description: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get description of a card/action effect
        /// </summary>
        private static string GetActionDescription(object action)
        {
            try
            {
                var actionType = action.GetType();

                // Try GetDescription
                var getDescMethod = actionType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(action, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        return StripRichTextTags(desc);
                    }
                }

                // Try to get damage amount
                var getDamageMethod = actionType.GetMethod("GetDamageAmount", Type.EmptyTypes) ??
                                     actionType.GetMethod("GetParamInt", Type.EmptyTypes);
                if (getDamageMethod != null)
                {
                    var result = getDamageMethod.Invoke(action, null);
                    if (result is int damage && damage > 0)
                    {
                        return $"{damage} damage";
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Strip rich text tags - delegates to the canonical implementation.
        /// </summary>
        private static string StripRichTextTags(string text)
        {
            return TextUtilities.StripRichTextTags(text);
        }

        /// <summary>
        /// Get units in a room using reflection on the room object.
        /// </summary>
        public static List<object> GetUnitsInRoom(object room)
        {
            var units = new List<object>();
            try
            {
                var roomType = room.GetType();

                // First try AddCharactersToList method
                var addCharsMethods = roomType.GetMethods().Where(m => m.Name == "AddCharactersToList").ToArray();

                // Find the Team.Type enum at runtime
                Type teamTypeEnum = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    teamTypeEnum = assembly.GetType("Team+Type") ?? assembly.GetType("Team`Type");
                    if (teamTypeEnum != null) break;

                    var teamType = assembly.GetType("Team");
                    if (teamType != null)
                    {
                        teamTypeEnum = teamType.GetNestedType("Type");
                        if (teamTypeEnum != null) break;
                    }
                }

                foreach (var addCharsMethod in addCharsMethods)
                {
                    var parameters = addCharsMethod.GetParameters();
                    if (parameters.Length >= 2)
                    {
                        var listType = parameters[0].ParameterType;
                        var secondParamType = parameters[1].ParameterType;

                        if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>) && secondParamType.IsEnum)
                        {
                            try
                            {
                                var enumValues = Enum.GetValues(secondParamType);

                                foreach (var teamValue in enumValues)
                                {
                                    var charList = Activator.CreateInstance(listType);

                                    var args = new object[parameters.Length];
                                    args[0] = charList;
                                    args[1] = teamValue;

                                    for (int i = 2; i < parameters.Length; i++)
                                    {
                                        args[i] = parameters[i].ParameterType.IsValueType
                                            ? Activator.CreateInstance(parameters[i].ParameterType)
                                            : null;
                                    }

                                    addCharsMethod.Invoke(room, args);

                                    if (charList is System.Collections.IEnumerable enumerable)
                                    {
                                        foreach (var c in enumerable)
                                        {
                                            if (c != null && !units.Contains(c))
                                            {
                                                units.Add(c);
                                            }
                                        }
                                    }
                                }

                                if (units.Count > 0)
                                {
                                    MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units via AddCharactersToList (both teams)");
                                    return units;
                                }
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"AddCharactersToList with team types failed: {ex.Message}");
                            }
                        }
                        else if (listType.Name.Contains("WeakRefList") && secondParamType.IsEnum)
                        {
                            continue;
                        }
                    }
                    else if (parameters.Length == 1)
                    {
                        var listType = parameters[0].ParameterType;
                        if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            try
                            {
                                var charList = Activator.CreateInstance(listType);
                                addCharsMethod.Invoke(room, new object[] { charList });

                                if (charList is System.Collections.IEnumerable enumerable)
                                {
                                    foreach (var c in enumerable)
                                    {
                                        if (c != null) units.Add(c);
                                    }
                                    if (units.Count > 0)
                                    {
                                        MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units via AddCharactersToList (single param)");
                                        return units;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"AddCharactersToList single-param failed: {ex.Message}");
                            }
                        }
                    }
                }

                // Fallback: try to access the characters field directly
                string[] fieldNames = { "characters", "_characters", "m_characters", "characterList" };
                foreach (var fieldName in fieldNames)
                {
                    var charsField = roomType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (charsField != null)
                    {
                        var chars = charsField.GetValue(room);
                        if (chars != null)
                        {
                            if (chars is System.Collections.IEnumerable enumerable)
                            {
                                foreach (var c in enumerable)
                                {
                                    if (c != null)
                                    {
                                        units.Add(c);
                                    }
                                }
                                if (units.Count > 0)
                                {
                                    MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units via field '{fieldName}'");
                                    return units;
                                }
                            }
                        }
                    }
                }

                // Log available methods for debugging if nothing worked
                if (units.Count == 0)
                {
                    var methods = roomType.GetMethods().Where(m => m.Name.Contains("Character") || m.Name.Contains("Unit")).ToList();
                    var methodLog = string.Join(", ", methods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"));
                    MonsterTrainAccessibility.LogInfo($"Room character-related methods: {methodLog}");
                }

                MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units (no method worked)");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting units: {ex.Message}");
            }
            return units;
        }
    }
}
