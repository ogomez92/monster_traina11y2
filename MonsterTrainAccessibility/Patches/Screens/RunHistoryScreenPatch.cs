using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
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
}
