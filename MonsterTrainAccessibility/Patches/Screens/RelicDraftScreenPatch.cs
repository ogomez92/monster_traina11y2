using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for relic/artifact draft screen
    /// </summary>
    public static class RelicDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RelicDraftScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RelicDraftScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RelicDraftScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RelicDraftScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RelicDraftScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RelicDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Relic draft screen entered");
                Help.Contexts.ArtifactSelectionHelp.SetActive(true);

                // Count artifacts if possible
                int count = CountRelics(__instance);
                string countText = count > 0 ? $" Choose from {count} artifacts." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Artifact Selection.{countText} Use arrow keys to browse, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RelicDraftScreen patch: {ex.Message}");
            }
        }

        private static int CountRelics(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                var fields = screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("relic") || fieldName.Contains("artifact") || fieldName.Contains("choice"))
                    {
                        var value = field.GetValue(screen);
                        if (value is IList list)
                        {
                            return list.Count;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }
}
