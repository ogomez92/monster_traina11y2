using HarmonyLib;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Screens.Readers;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for story event screen (random events on the map).
    /// Patches:
    ///   Initialize         – screen entry, read event name
    ///   AppendTextContent  – body text set on contentLabel, read it
    ///   OnChoicesPresented – new choices appear, read them all
    ///   OnStoryFinished    – no more choices, announce continue/leave
    /// </summary>
    public static class StoryEventScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StoryEventScreen");
                if (targetType == null) return;

                var initMethod = AccessTools.Method(targetType, "Initialize")
                              ?? AccessTools.Method(targetType, "Setup")
                              ?? AccessTools.Method(targetType, "SetupStory");
                if (initMethod != null)
                {
                    harmony.Patch(initMethod,
                        postfix: new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(InitPostfix))));
                    MonsterTrainAccessibility.LogInfo($"Patched StoryEventScreen.{initMethod.Name}");
                }

                var appendMethod = AccessTools.Method(targetType, "AppendTextContent");
                if (appendMethod != null)
                {
                    harmony.Patch(appendMethod,
                        postfix: new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(AppendTextPostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched StoryEventScreen.AppendTextContent");
                }

                var choicesMethod = AccessTools.Method(targetType, "OnChoicesPresented");
                if (choicesMethod != null)
                {
                    harmony.Patch(choicesMethod,
                        postfix: new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(ChoicesPresentedPostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched StoryEventScreen.OnChoicesPresented");
                }

                var finishedMethod = AccessTools.Method(targetType, "OnStoryFinished");
                if (finishedMethod != null)
                {
                    harmony.Patch(finishedMethod,
                        postfix: new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(StoryFinishedPostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched StoryEventScreen.OnStoryFinished");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StoryEventScreen: {ex.Message}");
            }
        }

        public static void InitPostfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Event);

                string eventName = GetEventName(__instance);
                string announcement = !string.IsNullOrEmpty(eventName)
                    ? $"Event: {eventName}"
                    : "Event";
                MonsterTrainAccessibility.ScreenReader?.Speak(announcement);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StoryEventScreen init patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Fires after AppendTextContent sets body text on contentLabel.
        /// Read the full body text each time (multi-page events get new text per page).
        /// </summary>
        public static void AppendTextPostfix(object __instance)
        {
            try
            {
                string bodyText = ReadContentLabel(__instance);
                if (!string.IsNullOrEmpty(bodyText))
                {
                    MonsterTrainAccessibility.ScreenReader?.Queue(bodyText);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading event body: {ex.Message}");
            }
        }

        /// <summary>
        /// Fires when new choices are presented. Read all choice labels + reward descriptions.
        /// Parameter: List of StoryChoiceData
        /// </summary>
        public static void ChoicesPresentedPostfix(object __instance)
        {
            try
            {
                var screenType = __instance.GetType();
                var choiceItemsField = screenType.GetField("currentChoiceItems",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (choiceItemsField == null) return;

                var choiceItems = choiceItemsField.GetValue(__instance) as IList;
                if (choiceItems == null || choiceItems.Count == 0) return;

                var sb = new StringBuilder();
                int count = choiceItems.Count;
                sb.Append($"{count} choices: ");

                for (int i = 0; i < count; i++)
                {
                    var item = choiceItems[i] as UnityEngine.Component;
                    if (item == null) continue;

                    string choiceText = EventTextReader.GetStoryChoiceText(
                        item.gameObject, item);
                    if (!string.IsNullOrEmpty(choiceText))
                    {
                        if (i > 0) sb.Append(". ");
                        sb.Append($"{i + 1}: {choiceText}");
                    }
                }

                string result = sb.ToString();
                if (!string.IsNullOrEmpty(result))
                {
                    MonsterTrainAccessibility.ScreenReader?.Queue(result);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading event choices: {ex.Message}");
            }
        }

        /// <summary>
        /// Fires when there are no more choices - continue/leave button appears.
        /// Read any final body text and announce continue.
        /// </summary>
        public static void StoryFinishedPostfix(object __instance)
        {
            try
            {
                // Read any final body text
                string bodyText = ReadContentLabel(__instance);
                if (!string.IsNullOrEmpty(bodyText))
                {
                    MonsterTrainAccessibility.ScreenReader?.Queue(bodyText);
                }

                // Localize "Continue" if possible
                string continueText = LocalizationHelper.TryLocalize("Continue")
                                   ?? LocalizationHelper.TryLocalize("UI_Continue")
                                   ?? "Continue";
                MonsterTrainAccessibility.ScreenReader?.Queue(continueText);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StoryFinished patch: {ex.Message}");
            }
        }

        private static string ReadContentLabel(object screen)
        {
            try
            {
                var screenType = screen.GetType();
                var field = screenType.GetField("contentLabel",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return null;

                var contentLabel = field.GetValue(screen);
                if (contentLabel == null) return null;

                var textProp = contentLabel.GetType().GetProperty("text");
                if (textProp == null) return null;

                string text = textProp.GetValue(contentLabel) as string;
                if (string.IsNullOrEmpty(text)) return null;

                return TextUtilities.StripRichTextTags(text).Trim();
            }
            catch { }
            return null;
        }

        private static string GetEventName(object screen)
        {
            try
            {
                var screenType = screen.GetType();
                var storyContentField = screenType.GetField("storyContent",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (storyContentField == null) return null;

                var storyContent = storyContentField.GetValue(screen);
                if (storyContent == null) return null;

                var contentType = storyContent.GetType();
                var getNameMethod = contentType.GetMethod("GetName") ?? contentType.GetMethod("GetTitle");
                return getNameMethod?.Invoke(storyContent, null) as string;
            }
            catch { }
            return null;
        }
    }
}
