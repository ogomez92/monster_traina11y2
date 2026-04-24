using HarmonyLib;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Announces the defeat / victory screen. Patches GameOverScreen.Initialize, calls
    /// the screen's private FastForward() so the UI settles to final values immediately,
    /// then reads title, score, bonus, battles, clan XP, personal-record accolades,
    /// and progression objectives.
    /// </summary>
    public static class GameOverScreenPatch
    {
        private const BindingFlags InstanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("GameOverScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("GameOverScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize");
                if (method == null)
                {
                    MonsterTrainAccessibility.LogWarning("GameOverScreen.Initialize not found");
                    return;
                }

                var postfix = new HarmonyMethod(typeof(GameOverScreenPatch).GetMethod(nameof(Postfix)));
                harmony.Patch(method, postfix: postfix);
                MonsterTrainAccessibility.LogInfo("Patched GameOverScreen.Initialize");
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
                bool isVictory = IsVictory(__instance);
                ScreenStateTracker.SetScreen(isVictory ? Help.GameScreen.Victory : Help.GameScreen.Defeat);

                string immediate = BuildImmediateAnnouncement(__instance, isVictory);
                if (!string.IsNullOrEmpty(immediate))
                    MonsterTrainAccessibility.ScreenReader?.Speak(immediate, false);

                // Skip the game's in/out score/XP tweens so labels settle instantly.
                // Accessibility users don't need the visual drama; sighted users who
                // happened to enable this mod accept that tradeoff.
                InvokeFastForward(__instance);

                MonsterTrainAccessibility.Instance?.StartCoroutine(DeferredReadCoroutine(__instance));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in GameOverScreen patch: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete.", false);
            }
        }

        private static IEnumerator DeferredReadCoroutine(object screen)
        {
            // ManualCoroutine steps one yield per frame even with skipCoroutine; a
            // couple seconds is enough for progressionObjectiveUIs to populate.
            yield return new UnityEngine.WaitForSeconds(2.0f);
            try
            {
                string full = BuildFullAnnouncement(screen);
                if (!string.IsNullOrEmpty(full))
                    MonsterTrainAccessibility.ScreenReader?.Queue(full);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in deferred game over read: {ex.Message}");
            }
        }

        private static void InvokeFastForward(object screen)
        {
            try
            {
                var m = screen?.GetType().GetMethod("FastForward", InstanceFields);
                m?.Invoke(screen, null);
            }
            catch { }
        }

        private static bool IsVictory(object screen)
        {
            var field = screen?.GetType().GetField("victoryType", InstanceFields);
            var value = field?.GetValue(screen);
            if (value == null) return false;
            // SaveManager.VictoryType: None=0 (defeat), Standard=1, TrueFinalBoss=2
            try { return (int)value > 0; }
            catch { return false; }
        }

        private static string BuildImmediateAnnouncement(object screen, bool isVictory)
        {
            var sb = new StringBuilder();
            sb.Append(GetLabelText(screen, "titleLabel") ?? (isVictory ? "Victory" : "Defeat"));
            sb.Append(". Press F5 to re-read. Press F6 to read all. Press Q for back to outpost, F for new run, Tab for run summary.");
            return sb.ToString();
        }

        private static string BuildFullAnnouncement(object screen)
        {
            if (screen == null) return null;
            var sb = new StringBuilder();

            // Score: prefer the authoritative int field over the animated label.
            int? finalScore = GetIntField(screen, "finalScore");
            if (finalScore.HasValue)
            {
                sb.Append($"Score {finalScore.Value:N0}. ");
            }
            else
            {
                string scoreText = GetLabelText(screen, "finalScoreStatLabel");
                if (!string.IsNullOrEmpty(scoreText)) sb.Append($"Score {scoreText}. ");
            }

            string endlessText = GetLabelText(screen, "battleScoresEndlessLabel");
            if (!string.IsNullOrEmpty(endlessText))
                sb.Append($"Endless battles {endlessText}. ");

            string bonus = ReadGoldBonus(screen);
            if (!string.IsNullOrEmpty(bonus))
                sb.Append($"Bonus: {bonus}. ");

            string battles = ReadBattlesSummary(screen);
            if (!string.IsNullOrEmpty(battles))
                sb.Append($"{battles}. ");

            string mainClan = ReadClanInfo(screen, "mainClassInfo", "Primary clan");
            if (!string.IsNullOrEmpty(mainClan)) sb.Append(mainClan);
            string subClan = ReadClanInfo(screen, "subClassInfo", "Allied clan");
            if (!string.IsNullOrEmpty(subClan)) sb.Append(subClan);

            string highlights = ReadStatHighlights(screen);
            if (!string.IsNullOrEmpty(highlights)) sb.Append(highlights);

            string records = ReadProgressionObjectives(screen);
            if (!string.IsNullOrEmpty(records)) sb.Append(records);

            return TextUtilities.StripRichTextTags(sb.ToString()).Trim();
        }

        private static int? GetIntField(object obj, string fieldName)
        {
            try
            {
                var f = obj?.GetType().GetField(fieldName, InstanceFields);
                var v = f?.GetValue(obj);
                if (v is int i) return i;
            }
            catch { }
            return null;
        }

        private static string GetLabelText(object container, string fieldName)
        {
            try
            {
                var field = container?.GetType().GetField(fieldName, InstanceFields);
                var label = field?.GetValue(container);
                if (label == null) return null;
                var textProp = label.GetType().GetProperty("text");
                var text = textProp?.GetValue(label) as string;
                if (string.IsNullOrEmpty(text)) return null;
                text = TextUtilities.StripRichTextTags(text);
                text = TextUtilities.CleanSpriteTagsForSpeech(text);
                return text?.Trim();
            }
            catch { return null; }
        }

        private static string ReadGoldBonus(object screen)
        {
            try
            {
                var field = screen.GetType().GetField("goldUI", InstanceFields);
                var goldUI = field?.GetValue(screen);
                if (goldUI == null) return null;

                // goldLabel is declared on GoldScoreModifierDisplay; target it explicitly.
                string gold = GetLabelText(goldUI, "goldLabel");
                if (!string.IsNullOrEmpty(gold))
                    return $"{gold} {ModLocalization.Gold}";
            }
            catch { }
            return null;
        }

        private static string ReadBattlesSummary(object screen)
        {
            try
            {
                var field = screen.GetType().GetField("battleScoreUIs", InstanceFields);
                var list = field?.GetValue(screen) as IList;
                if (list == null || list.Count == 0) return null;

                int completed = 0;
                int total = list.Count;
                foreach (var battleUI in list)
                {
                    if (battleUI == null) continue;
                    var interactableProp = battleUI.GetType().GetProperty("interactable",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    var val = interactableProp?.GetValue(battleUI);
                    if (val is bool b && b) completed++;
                }

                if (completed == 0 && total == 0) return null;
                return $"{completed} of {total} battles completed";
            }
            catch { }
            return null;
        }

        private static string ReadClanInfo(object screen, string fieldName, string roleLabel)
        {
            try
            {
                var field = screen.GetType().GetField(fieldName, InstanceFields);
                var clanInfo = field?.GetValue(screen);
                if (clanInfo == null) return null;

                string clanName = GetLabelText(clanInfo, "classNameLabel");
                if (string.IsNullOrEmpty(clanName)) return null;

                string level = null;
                string xp = null;
                var meterField = clanInfo.GetType().GetField("classLevelMeterUI", InstanceFields);
                var meter = meterField?.GetValue(clanInfo);
                if (meter != null)
                {
                    level = GetLabelText(meter, "levelLabel");
                    var xpMeterField = meter.GetType().GetField("xpMeter", InstanceFields);
                    var xpMeter = xpMeterField?.GetValue(meter);
                    if (xpMeter != null)
                        xp = GetLabelText(xpMeter, "countLabel");
                }

                var sb = new StringBuilder();
                sb.Append($"{roleLabel} {clanName}");
                if (!string.IsNullOrEmpty(level) && level != "-")
                    sb.Append($", level {level}");
                if (!string.IsNullOrEmpty(xp))
                    sb.Append($", {xp} XP");
                sb.Append(". ");
                return sb.ToString();
            }
            catch { }
            return null;
        }

        private static string ReadStatHighlights(object screen)
        {
            try
            {
                var field = screen.GetType().GetField("statHighlightUIs", InstanceFields);
                var list = field?.GetValue(screen) as IList;
                if (list == null || list.Count == 0) return null;

                var parts = new List<string>();
                foreach (var ui in list)
                {
                    if (ui == null) continue;
                    var goProp = ui.GetType().GetProperty("gameObject");
                    var go = goProp?.GetValue(ui) as UnityEngine.GameObject;
                    if (go != null && !go.activeInHierarchy) continue;

                    string header = GetLabelText(ui, "headerLabel");
                    string body = GetLabelText(ui, "accoladeLabel");
                    if (string.IsNullOrEmpty(header) && string.IsNullOrEmpty(body)) continue;

                    var line = new StringBuilder();
                    if (!string.IsNullOrEmpty(header)) line.Append(header);
                    if (!string.IsNullOrEmpty(body))
                    {
                        if (line.Length > 0) line.Append(": ");
                        line.Append(body.Replace('\n', ',').Replace("\r", string.Empty));
                    }
                    if (line.Length > 0) parts.Add(line.ToString());
                }

                if (parts.Count == 0) return null;
                return "Highlights: " + string.Join(". ", parts) + ". ";
            }
            catch { }
            return null;
        }

        private static string ReadProgressionObjectives(object screen)
        {
            try
            {
                var field = screen.GetType().GetField("progressionObjectiveUIs", InstanceFields);
                var list = field?.GetValue(screen) as IList;
                if (list == null || list.Count == 0) return null;

                var parts = new List<string>();
                foreach (var entry in list)
                {
                    if (entry == null) continue;
                    var goProp = entry.GetType().GetProperty("gameObject");
                    var go = goProp?.GetValue(entry) as UnityEngine.GameObject;
                    if (go != null && !go.activeInHierarchy) continue;

                    string title = GetLabelText(entry, "titleLabel");
                    string desc = GetLabelText(entry, "descriptionLabel");
                    string numeric = GetLabelText(entry, "numericLabel");

                    var line = new StringBuilder();
                    if (!string.IsNullOrEmpty(title)) line.Append(title);
                    if (!string.IsNullOrEmpty(desc))
                    {
                        if (line.Length > 0) line.Append(": ");
                        line.Append(desc);
                    }
                    if (!string.IsNullOrEmpty(numeric))
                    {
                        if (line.Length > 0) line.Append(", ");
                        line.Append(numeric);
                    }

                    if (line.Length > 0) parts.Add(line.ToString());
                }

                if (parts.Count == 0) return null;
                return "Objectives: " + string.Join(". ", parts) + ".";
            }
            catch { }
            return null;
        }
    }
}
