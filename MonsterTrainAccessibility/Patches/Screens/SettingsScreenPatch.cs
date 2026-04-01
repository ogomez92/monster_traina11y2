using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
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
}
