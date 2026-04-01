using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Challenge screens
    /// </summary>
    public static class ChallengeScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try ChallengeOverviewScreen
                var targetType = AccessTools.TypeByName("ChallengeOverviewScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ChallengeOverviewScreen type not found");
                }
                else
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeScreenPatch).GetMethod(nameof(OverviewPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeOverviewScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("ChallengeOverviewScreen methods not found");
                    }
                }

                // Try ChallengeDetailsScreen
                var detailsType = AccessTools.TypeByName("ChallengeDetailsScreen");
                if (detailsType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ChallengeDetailsScreen type not found");
                }
                else
                {
                    var method = AccessTools.Method(detailsType, "Initialize") ??
                                 AccessTools.Method(detailsType, "Setup");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeScreenPatch).GetMethod(nameof(DetailsPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeDetailsScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("ChallengeDetailsScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch Challenge screens: {ex.Message}");
            }
        }

        public static void OverviewPostfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Challenge overview screen entered");
                Help.Contexts.ChallengesHelp.SetActive(true);
                MonsterTrainAccessibility.ScreenReader?.Speak("Challenges Overview. Browse daily and weekly challenges. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeOverviewScreen patch: {ex.Message}");
            }
        }

        public static void DetailsPostfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Challenge details screen entered");
                Help.Contexts.ChallengesHelp.SetActive(true);
                MonsterTrainAccessibility.ScreenReader?.Speak("Challenge Details. View challenge rules and rewards. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeDetailsScreen patch: {ex.Message}");
            }
        }
    }
}
