using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
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
}
