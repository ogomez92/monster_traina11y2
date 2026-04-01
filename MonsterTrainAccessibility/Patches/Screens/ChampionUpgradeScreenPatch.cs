using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for champion upgrade screen
    /// </summary>
    public static class ChampionUpgradeScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChampionUpgradeScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ChampionUpgradeScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(ChampionUpgradeScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched ChampionUpgradeScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("ChampionUpgradeScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChampionUpgradeScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Champion upgrade screen entered");
                Help.Contexts.ChampionUpgradeHelp.SetActive(true);
                MonsterTrainAccessibility.ScreenReader?.Speak("Champion Upgrade. Choose an upgrade for your champion. Use arrow keys to browse options, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChampionUpgradeScreen patch: {ex.Message}");
            }
        }
    }
}
