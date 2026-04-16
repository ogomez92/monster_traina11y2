using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect enemy dialogue/chatter (speech bubbles like "These chains would suit you!")
    /// Hooks ChatterExpression.Express which receives the final translated text.
    /// Signature: Express(Chatter setChatter, ChatterExpressionType expressionType, CharacterState character, float delay, string translatedText)
    /// </summary>
    public static class EnemyDialoguePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var chatterExpressionType = AccessTools.TypeByName("ChatterExpression");
                if (chatterExpressionType == null)
                {
                    MonsterTrainAccessibility.LogWarning("EnemyDialoguePatch: ChatterExpression type not found");
                    return;
                }

                var method = AccessTools.Method(chatterExpressionType, "Express");
                if (method == null)
                {
                    MonsterTrainAccessibility.LogWarning("EnemyDialoguePatch: ChatterExpression.Express method not found");
                    return;
                }

                var postfix = new HarmonyMethod(typeof(EnemyDialoguePatch).GetMethod(nameof(PostfixExpress)));
                harmony.Patch(method, postfix: postfix);
                MonsterTrainAccessibility.LogInfo("Patched ChatterExpression.Express for dialogue reading");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch enemy dialogue: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix on ChatterExpression.Express.
        /// __2 = CharacterState character, __4 = string translatedText
        /// </summary>
        public static void PostfixExpress(object __2, string __4)
        {
            try
            {
                if (string.IsNullOrEmpty(__4))
                    return;

                string charName = "Enemy";
                if (__2 != null)
                {
                    var getNameMethod = __2.GetType().GetMethod("GetName", Type.EmptyTypes);
                    charName = getNameMethod?.Invoke(__2, null) as string ?? "Enemy";
                }

                string cleanText = Utilities.TextUtilities.StripRichTextTags(__4);
                MonsterTrainAccessibility.BattleHandler?.OnEnemyDialogue($"{charName}: {cleanText}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in chatter express patch: {ex.Message}");
            }
        }
    }
}
