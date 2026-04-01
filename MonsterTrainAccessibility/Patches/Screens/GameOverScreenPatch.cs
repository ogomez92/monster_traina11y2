using HarmonyLib;
using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Detect game over / run summary screen (victory or defeat)
    /// </summary>
    public static class GameOverScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible class names for the game over screen
                var targetNames = new[] {
                    "GameOverScreen",
                    "RunSummaryScreen",
                    "VictoryScreen",
                    "DefeatScreen",
                    "RunEndScreen",
                    "EndRunScreen"
                };

                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Open");

                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(GameOverScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo("GameOverScreen not found - will use alternative detection");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch GameOverScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Game over screen entered");

                // Auto-read the game over screen
                AutoReadGameOverScreen(__instance);

                // Also call the menu handler for additional processing
                MonsterTrainAccessibility.MenuHandler?.OnGameOverScreenEntered(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in GameOverScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadGameOverScreen(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                var screenType = screen?.GetType();

                // Log fields for debugging
                MonsterTrainAccessibility.LogInfo($"=== GameOverScreen fields ===");
                if (screenType != null)
                {
                    foreach (var field in screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        try
                        {
                            var value = field.GetValue(screen);
                            MonsterTrainAccessibility.LogInfo($"  {field.Name} = {value?.GetType().Name ?? "null"}");
                        }
                        catch { }
                    }
                }

                // Try to get victory/defeat status from SaveManager
                bool isVictory = false;
                int score = 0;
                int battlesWon = 0;
                int ring = 0;

                var saveManagerType = AccessTools.TypeByName("SaveManager");
                if (saveManagerType != null)
                {
                    var saveManager = UnityEngine.Object.FindObjectOfType(saveManagerType) as UnityEngine.Object;
                    if (saveManager != null)
                    {
                        // Check if battle was won/lost
                        var battleCompleteMethod = saveManagerType.GetMethod("BattleComplete");
                        if (battleCompleteMethod != null)
                        {
                            var result = battleCompleteMethod.Invoke(saveManager, null);
                            if (result is bool bc) isVictory = bc;
                        }

                        // Get ring/covenant level
                        var getCovenantMethod = saveManagerType.GetMethod("GetAscensionLevel") ??
                                               saveManagerType.GetMethod("GetCovenantLevel");
                        if (getCovenantMethod != null)
                        {
                            var result = getCovenantMethod.Invoke(saveManager, null);
                            if (result is int r) ring = r;
                        }

                        // Get battles won
                        var getBattlesMethod = saveManagerType.GetMethod("GetNumBattlesWon");
                        if (getBattlesMethod != null)
                        {
                            var result = getBattlesMethod.Invoke(saveManager, null);
                            if (result is int b) battlesWon = b;
                        }

                        // Get score
                        var getScoreMethod = saveManagerType.GetMethod("GetRunScore") ??
                                            saveManagerType.GetMethod("GetScore");
                        if (getScoreMethod != null)
                        {
                            var result = getScoreMethod.Invoke(saveManager, null);
                            if (result is int s) score = s;
                        }
                    }
                }

                // Try to get specific labels from the screen
                string resultTitle = null;
                string runType = null;

                if (screenType != null)
                {
                    // Look for result/victory/defeat label
                    var resultField = screenType.GetField("resultLabel", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                     screenType.GetField("titleLabel", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                     screenType.GetField("victoryDefeatLabel", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (resultField != null)
                    {
                        var labelObj = resultField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                resultTitle = textProp.GetValue(labelObj) as string;
                            }
                        }
                    }

                    // Look for run type label
                    var runTypeField = screenType.GetField("runTypeLabel", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                      screenType.GetField("runNameLabel", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (runTypeField != null)
                    {
                        var labelObj = runTypeField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                runType = textProp.GetValue(labelObj) as string;
                            }
                        }
                    }

                    // Look for score label
                    var scoreField = screenType.GetField("scoreLabel", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                    screenType.GetField("totalScoreLabel", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (scoreField != null && score == 0)
                    {
                        var labelObj = scoreField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                var scoreText = textProp.GetValue(labelObj) as string;
                                if (!string.IsNullOrEmpty(scoreText))
                                {
                                    // Parse score from text like "4,254"
                                    scoreText = Regex.Replace(scoreText, "[^0-9]", "");
                                    int.TryParse(scoreText, out score);
                                }
                            }
                        }
                    }
                }

                // Build announcement
                // Result title (Victory/Defeat)
                if (!string.IsNullOrEmpty(resultTitle))
                {
                    sb.Append($"{resultTitle}. ");
                }
                else
                {
                    sb.Append(isVictory ? "Victory. " : "Defeat. ");
                }

                // Run type and ring
                if (!string.IsNullOrEmpty(runType))
                {
                    sb.Append($"{runType}. ");
                }
                if (ring > 0)
                {
                    sb.Append($"Covenant {ring}. ");
                }

                // Score
                if (score > 0)
                {
                    sb.Append($"Score: {score:N0}. ");
                }

                // Battles
                if (battlesWon > 0)
                {
                    sb.Append($"Battles won: {battlesWon}. ");
                }

                // Announce
                string announcement = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Game over auto-read: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
                else
                {
                    // Fallback
                    MonsterTrainAccessibility.ScreenReader?.Speak("Run complete. Press T to read stats.", false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AutoReadGameOverScreen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete.", false);
            }
        }
    }
}
