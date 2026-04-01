using HarmonyLib;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Screens
{
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
                var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;

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
                    headerText = TextUtilities.StripRichTextTags(headerText ?? "");
                    statText = TextUtilities.StripRichTextTags(statText ?? "");
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
}
