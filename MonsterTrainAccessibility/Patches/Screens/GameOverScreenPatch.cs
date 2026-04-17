using HarmonyLib;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Announce the defeat / victory screen. Hooks GameOverScreen.Initialize (private,
    /// invoked when the screen opens) and reads the populated labels, clan XP bars, and
    /// progression objectives via reflection. Title / score / score-breakdown / battles /
    /// primary+allied clan levels / "NEW PERSONAL RECORD" quests / active-clan quests
    /// all get announced sequentially.
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

                // Initialize fires before the tweened labels settle to their final value
                // (score animates 0 → final, XP bars animate, etc.). A short delay lets
                // us read the real numbers — but we start by announcing the title+buttons
                // immediately so the screen never feels silent.
                string immediate = BuildImmediateAnnouncement(__instance, isVictory);
                if (!string.IsNullOrEmpty(immediate))
                    MonsterTrainAccessibility.ScreenReader?.Speak(immediate, false);

                MonsterTrainAccessibility.Instance?.StartCoroutine(DeferredReadCoroutine(__instance));

                MonsterTrainAccessibility.MenuHandler?.OnGameOverScreenEntered(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in GameOverScreen patch: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete.", false);
            }
        }

        private static IEnumerator DeferredReadCoroutine(object screen)
        {
            yield return new UnityEngine.WaitForSeconds(2.5f);
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

            // Score (standard run) or endless battles (endless run)
            string scoreText = GetLabelText(screen, "finalScoreStatLabel");
            if (!string.IsNullOrEmpty(scoreText))
                sb.Append($"Score {scoreText}. ");

            string endlessText = GetLabelText(screen, "battleScoresEndlessLabel");
            if (!string.IsNullOrEmpty(endlessText))
                sb.Append($"{endlessText}. ");

            // Bonus gold
            string bonus = ReadGoldBonus(screen);
            if (!string.IsNullOrEmpty(bonus))
                sb.Append($"Bonus: {bonus}. ");

            // Battles summary
            string battles = ReadBattlesSummary(screen);
            if (!string.IsNullOrEmpty(battles))
                sb.Append($"{battles}. ");

            // Primary + allied clan XP
            string mainClan = ReadClanInfo(screen, "mainClassInfo", "Primary clan");
            if (!string.IsNullOrEmpty(mainClan))
                sb.Append($"{mainClan}. ");
            string subClan = ReadClanInfo(screen, "subClassInfo", "Allied clan");
            if (!string.IsNullOrEmpty(subClan))
                sb.Append($"{subClan}. ");

            // Personal records / progression objectives
            string records = ReadProgressionObjectives(screen);
            if (!string.IsNullOrEmpty(records))
                sb.Append(records);

            return TextUtilities.StripRichTextTags(sb.ToString()).Trim();
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

                // Look for any TMP child field on GoldScoreModifierDisplay. Amount is
                // usually in a field named "amountLabel", "goldLabel", or similar.
                foreach (var f in goldUI.GetType().GetFields(InstanceFields))
                {
                    var val = f.GetValue(goldUI);
                    if (val == null) continue;
                    var textProp = val.GetType().GetProperty("text");
                    if (textProp == null) continue;
                    var text = textProp.GetValue(val) as string;
                    if (string.IsNullOrEmpty(text)) continue;
                    text = TextUtilities.StripRichTextTags(text)?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        return $"{text} {ModLocalization.Gold}";
                }
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

                // Clan level lives on classLevelMeterUI.levelLabel
                string level = null;
                var meterField = clanInfo.GetType().GetField("classLevelMeterUI", InstanceFields);
                var meter = meterField?.GetValue(clanInfo);
                if (meter != null)
                    level = GetLabelText(meter, "levelLabel");

                if (string.IsNullOrEmpty(clanName)) return null;
                if (!string.IsNullOrEmpty(level) && level != "-")
                    return $"{roleLabel} {clanName}, level {level}";
                return $"{roleLabel} {clanName}";
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

                var parts = new System.Collections.Generic.List<string>();
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
