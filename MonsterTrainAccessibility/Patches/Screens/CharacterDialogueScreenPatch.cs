using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for the character dialogue screen (story conversations with speaker name and body text).
    /// Hooks ShowPhrase to read each dialogue line, and OnTextDoneDisplaying as a backup
    /// to read the final text after the typewriter effect completes.
    /// Fields: dialogueText (TextMeshProUGUI), speakerLabel (TextMeshProUGUI).
    /// </summary>
    public static class CharacterDialogueScreenPatch
    {
        private static string _lastDialogueText = null;
        private static string _currentSpeaker = null;
        private static FieldInfo _dialogueTextField = null;
        private static FieldInfo _speakerLabelField = null;
        private static bool _hintShown = false;

        private static readonly Dictionary<SystemLanguage, string> _dialogueHints = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.English, "Press Enter to continue or Escape to skip tutorial messages" },
            { SystemLanguage.Spanish, "Pulsa Enter para continuar o Escape para saltar los mensajes del tutorial" },
            { SystemLanguage.French, "Appuyez sur Entr\u00e9e pour continuer ou \u00c9chap pour passer les messages du tutoriel" },
            { SystemLanguage.German, "Dr\u00fccke Enter zum Fortfahren oder Escape zum \u00dcberspringen der Tutorial-Nachrichten" },
            { SystemLanguage.Italian, "Premi Invio per continuare o Esc per saltare i messaggi del tutorial" },
            { SystemLanguage.Portuguese, "Pressione Enter para continuar ou Escape para pular as mensagens do tutorial" },
            { SystemLanguage.Russian, "\u041d\u0430\u0436\u043c\u0438\u0442\u0435 Enter \u0434\u043b\u044f \u043f\u0440\u043e\u0434\u043e\u043b\u0436\u0435\u043d\u0438\u044f \u0438\u043b\u0438 Escape \u0434\u043b\u044f \u043f\u0440\u043e\u043f\u0443\u0441\u043a\u0430 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0439 \u043e\u0431\u0443\u0447\u0435\u043d\u0438\u044f" },
            { SystemLanguage.Chinese, "\u6309 Enter \u7ee7\u7eed\u6216 Escape \u8df3\u8fc7\u6559\u7a0b\u6d88\u606f" },
            { SystemLanguage.ChineseSimplified, "\u6309 Enter \u7ee7\u7eed\u6216 Escape \u8df3\u8fc7\u6559\u7a0b\u6d88\u606f" },
            { SystemLanguage.ChineseTraditional, "\u6309 Enter \u7e7c\u7e8c\u6216 Escape \u8df3\u904e\u6559\u5b78\u8a0a\u606f" },
            { SystemLanguage.Japanese, "Enter \u3067\u7d9a\u884c\u3001Escape \u3067\u30c1\u30e5\u30fc\u30c8\u30ea\u30a2\u30eb\u30e1\u30c3\u30bb\u30fc\u30b8\u3092\u30b9\u30ad\u30c3\u30d7" },
            { SystemLanguage.Korean, "Enter\ub97c \ub20c\ub7ec \uacc4\uc18d\ud558\uac70\ub098 Escape\ub97c \ub20c\ub7ec \ud29c\ud1a0\ub9ac\uc5bc \uba54\uc2dc\uc9c0\ub97c \uac74\ub108\ub6f0\uc138\uc694" },
        };

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CharacterDialogueScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("CharacterDialogueScreen type not found");
                    return;
                }

                // Cache the text fields
                _dialogueTextField = AccessTools.Field(targetType, "dialogueText");
                _speakerLabelField = AccessTools.Field(targetType, "speakerLabel");

                // Patch ShowPhrase - called for each dialogue line (2 params)
                var showPhraseMethod = AccessTools.Method(targetType, "ShowPhrase");
                if (showPhraseMethod != null)
                {
                    harmony.Patch(showPhraseMethod,
                        postfix: new HarmonyMethod(typeof(CharacterDialogueScreenPatch).GetMethod(nameof(ShowPhrasePostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched CharacterDialogueScreen.ShowPhrase");
                }

                // Patch OnTextDoneDisplaying - fires when typewriter finishes, good time to read final text
                var textDoneMethod = AccessTools.Method(targetType, "OnTextDoneDisplaying");
                if (textDoneMethod != null)
                {
                    harmony.Patch(textDoneMethod,
                        postfix: new HarmonyMethod(typeof(CharacterDialogueScreenPatch).GetMethod(nameof(TextDonePostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched CharacterDialogueScreen.OnTextDoneDisplaying");
                }

                // Patch SetSpeaker - called when the speaking character changes (2 params)
                var setSpeakerMethod = AccessTools.Method(targetType, "SetSpeaker");
                if (setSpeakerMethod != null)
                {
                    harmony.Patch(setSpeakerMethod,
                        postfix: new HarmonyMethod(typeof(CharacterDialogueScreenPatch).GetMethod(nameof(SetSpeakerPostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched CharacterDialogueScreen.SetSpeaker");
                }

                // Patch Initialize for screen entry
                var initMethod = AccessTools.Method(targetType, "Initialize");
                if (initMethod != null)
                {
                    harmony.Patch(initMethod,
                        postfix: new HarmonyMethod(typeof(CharacterDialogueScreenPatch).GetMethod(nameof(InitPostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched CharacterDialogueScreen.Initialize");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CharacterDialogueScreen: {ex.Message}");
            }
        }

        public static void InitPostfix(object __instance)
        {
            try
            {
                _lastDialogueText = null;
                _currentSpeaker = null;
                Help.ScreenStateTracker.SetScreen(Help.GameScreen.CharacterDialogue);
                MonsterTrainAccessibility.LogInfo("Character dialogue screen entered");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CharacterDialogueScreen init: {ex.Message}");
            }
        }

        public static void SetSpeakerPostfix(object __instance)
        {
            try
            {
                // Read the speaker label after SetSpeaker updates it
                string speaker = ReadTextField(__instance, _speakerLabelField);
                if (!string.IsNullOrEmpty(speaker))
                {
                    _currentSpeaker = Regex.Replace(speaker, @"<[^>]+>", "").Trim();
                    MonsterTrainAccessibility.LogInfo($"[CharacterDialogue] Speaker: {_currentSpeaker}");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SetSpeaker patch: {ex.Message}");
            }
        }

        public static void ShowPhrasePostfix(object __instance)
        {
            try
            {
                AnnounceCurrentDialogue(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ShowPhrase patch: {ex.Message}");
            }
        }

        public static void TextDonePostfix(object __instance)
        {
            try
            {
                // Backup: read the fully revealed text after typewriter completes
                AnnounceCurrentDialogue(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in TextDone patch: {ex.Message}");
            }
        }

        private static void AnnounceCurrentDialogue(object instance)
        {
            // Read speaker from label if we don't have it cached
            if (string.IsNullOrEmpty(_currentSpeaker))
            {
                string speaker = ReadTextField(instance, _speakerLabelField);
                if (!string.IsNullOrEmpty(speaker))
                    _currentSpeaker = Regex.Replace(speaker, @"<[^>]+>", "").Trim();
            }

            string body = ReadTextField(instance, _dialogueTextField);
            if (string.IsNullOrEmpty(body)) return;

            string cleanBody = Regex.Replace(body, @"<[^>]+>", "").Trim();
            if (string.IsNullOrEmpty(cleanBody) || cleanBody == _lastDialogueText) return;

            _lastDialogueText = cleanBody;

            string announcement = !string.IsNullOrEmpty(_currentSpeaker)
                ? $"{_currentSpeaker}: {cleanBody}"
                : cleanBody;

            // On the first dialogue, append navigation hint
            if (!_hintShown)
            {
                _hintShown = true;
                announcement += ". " + GetDialogueHint();
            }

            MonsterTrainAccessibility.LogInfo($"[CharacterDialogue] {announcement}");

            // Silence pending speech first — dialogue takes priority over
            // screen announcements (e.g. "Main Menu") that may be queued.
            // Without this, NVDA can drop queued speech during screen transitions.
            MonsterTrainAccessibility.ScreenReader?.Silence();
            MonsterTrainAccessibility.ScreenReader?.Speak(announcement);
        }

        private static string GetDialogueHint()
        {
            // Prefer the game's current language (I2.Loc.LocalizationManager.CurrentLanguageCode)
            // over Application.systemLanguage, which reports the OS locale and doesn't
            // track the in-game language setting.
            var lang = GetGameLanguage();
            if (_dialogueHints.TryGetValue(lang, out string hint))
                return hint;
            return _dialogueHints[SystemLanguage.English];
        }

        private static SystemLanguage GetGameLanguage()
        {
            try
            {
                var locMgrType = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                if (locMgrType != null)
                {
                    var prop = locMgrType.GetProperty("CurrentLanguageCode", BindingFlags.Public | BindingFlags.Static);
                    var code = prop?.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(code))
                    {
                        return MapLanguageCode(code);
                    }
                }
            }
            catch { }
            return Application.systemLanguage;
        }

        private static SystemLanguage MapLanguageCode(string code)
        {
            // I2.Loc uses ISO codes like "en", "es", "fr", "zh-CN", "pt-BR", etc.
            // Strip any region suffix for the primary match, then special-case CJK variants.
            string primary = code;
            int dash = code.IndexOf('-');
            if (dash > 0) primary = code.Substring(0, dash);

            switch (primary.ToLowerInvariant())
            {
                case "en": return SystemLanguage.English;
                case "es": return SystemLanguage.Spanish;
                case "fr": return SystemLanguage.French;
                case "de": return SystemLanguage.German;
                case "it": return SystemLanguage.Italian;
                case "pt": return SystemLanguage.Portuguese;
                case "ru": return SystemLanguage.Russian;
                case "ja": return SystemLanguage.Japanese;
                case "ko": return SystemLanguage.Korean;
                case "zh":
                    if (code.IndexOf("TW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        code.IndexOf("HK", StringComparison.OrdinalIgnoreCase) >= 0)
                        return SystemLanguage.ChineseTraditional;
                    return SystemLanguage.ChineseSimplified;
                default: return SystemLanguage.English;
            }
        }

        private static string ReadTextField(object instance, FieldInfo field)
        {
            if (field == null || instance == null) return null;
            try
            {
                var value = field.GetValue(instance);
                if (value == null) return null;
                if (value is string s) return s;

                // TextMeshProUGUI - read .text property
                var textProp = value.GetType().GetProperty("text");
                return textProp?.GetValue(value) as string;
            }
            catch { return null; }
        }

        public static void Reset()
        {
            _lastDialogueText = null;
            _currentSpeaker = null;
        }
    }
}
