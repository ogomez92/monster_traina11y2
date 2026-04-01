using HarmonyLib;
using System;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Patches.Screens
{
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
            var formatted = Regex.Replace(screenName, "([a-z])([A-Z])", "$1 $2");

            return formatted;
        }
    }
}
