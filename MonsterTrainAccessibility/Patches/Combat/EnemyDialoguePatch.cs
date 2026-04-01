using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect enemy dialogue/chatter (speech bubbles like "These chains would suit you!")
    /// This hooks into the Chatter system to read enemy dialogue
    /// DisplayChatter signature: (ChatterExpressionType expressionType, CharacterState character, float delay, CharacterTriggerData+Trigger trigger)
    /// </summary>
    public static class EnemyDialoguePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to find the Chatter or ChatterUI class that displays speech bubbles
                var chatterType = AccessTools.TypeByName("Chatter");
                if (chatterType != null)
                {
                    // Look for method that sets/displays the chatter text
                    var method = AccessTools.Method(chatterType, "SetExpression") ??
                                 AccessTools.Method(chatterType, "ShowExpression") ??
                                 AccessTools.Method(chatterType, "DisplayChatter") ??
                                 AccessTools.Method(chatterType, "Play");

                    if (method != null)
                    {
                        // Log the parameters
                        var parameters = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"Chatter.{method.Name} has {parameters.Length} parameters:");
                        foreach (var p in parameters)
                        {
                            MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                        }

                        var postfix = new HarmonyMethod(typeof(EnemyDialoguePatch).GetMethod(nameof(PostfixChatter)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched Chatter method: {method.Name}");
                    }
                }

                // Also try ChatterUI
                var chatterUIType = AccessTools.TypeByName("ChatterUI");
                if (chatterUIType != null)
                {
                    var method = AccessTools.Method(chatterUIType, "SetChatter") ??
                                 AccessTools.Method(chatterUIType, "DisplayChatter") ??
                                 AccessTools.Method(chatterUIType, "ShowChatter");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(EnemyDialoguePatch).GetMethod(nameof(PostfixChatterUI)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChatterUI method: {method.Name}");
                    }
                }

                // Try CharacterChatterData which stores the dialogue expressions
                var chatterDataType = AccessTools.TypeByName("CharacterChatterData");
                if (chatterDataType != null)
                {
                    // Log available methods for debugging
                    var methods = chatterDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"CharacterChatterData methods: {string.Join(", ", methods.Where(m => m.Name.Contains("Expression") || m.Name.Contains("Chatter")).Select(m => m.Name))}");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch enemy dialogue: {ex.Message}");
            }
        }

        // Use positional parameters: __0 = expressionType (enum), __1 = character (CharacterState)
        public static void PostfixChatter(object __instance, object __0, object __1)
        {
            try
            {
                // __0 is the expression type enum, __1 is the CharacterState
                object expressionType = __0;
                object character = __1;

                if (expressionType == null) return;

                // Try to get the chatter data from the character
                string text = null;

                // First try to get text from the expression type
                text = GetExpressionText(expressionType);

                // If that didn't work, try to get the character's name and log the expression type
                if (string.IsNullOrEmpty(text))
                {
                    string charName = character != null ? GetCharacterName(character) : "Enemy";
                    string exprTypeName = expressionType.ToString();
                    MonsterTrainAccessibility.LogInfo($"Chatter: {charName} - {exprTypeName}");

                    // Don't announce if we couldn't get the actual text
                    return;
                }

                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.BattleHandler?.OnEnemyDialogue(text);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in chatter patch: {ex.Message}");
            }
        }

        private static string GetCharacterName(object character)
        {
            try
            {
                var type = character.GetType();
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(character, null) as string ?? "Enemy";
                }
            }
            catch { }
            return "Enemy";
        }

        public static void PostfixChatterUI(object __instance, string text)
        {
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.BattleHandler?.OnEnemyDialogue(text);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in chatter UI patch: {ex.Message}");
            }
        }

        private static string GetExpressionText(object expression)
        {
            try
            {
                var type = expression.GetType();

                // Try common property/method names for getting the text
                var getText = type.GetMethod("GetText") ?? type.GetMethod("GetLocalizedText");
                if (getText != null)
                {
                    var text = getText.Invoke(expression, null) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }

                // Try text property
                var textProp = type.GetProperty("text") ?? type.GetProperty("Text");
                if (textProp != null)
                {
                    var text = textProp.GetValue(expression) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }

                // Try localization key
                var getKey = type.GetMethod("GetLocalizationKey");
                if (getKey != null)
                {
                    var key = getKey.Invoke(expression, null) as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        // Try to localize
                        var localizeMethod = typeof(string).GetMethod("Localize", new Type[] { typeof(string) });
                        if (localizeMethod != null)
                        {
                            return localizeMethod.Invoke(null, new object[] { key }) as string ?? key;
                        }
                        return key;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
