using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Detect map screen
    /// </summary>
    public static class MapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MapScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("MapNodeScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MapScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MapScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Map);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Map. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MapScreen patch: {ex.Message}");
            }
        }
    }
}
