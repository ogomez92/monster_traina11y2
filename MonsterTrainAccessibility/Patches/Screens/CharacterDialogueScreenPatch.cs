using HarmonyLib;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

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

            MonsterTrainAccessibility.LogInfo($"[CharacterDialogue] {announcement}");
            MonsterTrainAccessibility.ScreenReader?.Speak(announcement);
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
