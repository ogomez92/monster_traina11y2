using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Patches.Screens
{
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
                        var methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
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
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
                string cleanDesc = Regex.Replace(description, @"<[^>]+>", "");
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
}
