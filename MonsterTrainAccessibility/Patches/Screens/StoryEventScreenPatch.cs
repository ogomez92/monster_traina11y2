using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for story event screen (random events on the map)
    /// </summary>
    public static class StoryEventScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StoryEventScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "SetupStory");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched StoryEventScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("StoryEventScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StoryEventScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Event);
                MonsterTrainAccessibility.LogInfo("Story event screen entered");

                // Auto-read the event content
                AutoReadStoryEvent(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StoryEventScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadStoryEvent(object screen)
        {
            try
            {
                if (screen == null) return;
                var screenType = screen.GetType();

                string eventName = null;
                string eventText = null;

                // Get event name from storyContent (StoryEventData)
                var storyContentField = screenType.GetField("storyContent", BindingFlags.NonPublic | BindingFlags.Instance);
                if (storyContentField != null)
                {
                    var storyContent = storyContentField.GetValue(screen);
                    if (storyContent != null)
                    {
                        var contentType = storyContent.GetType();
                        var getNameMethod = contentType.GetMethod("GetName") ?? contentType.GetMethod("GetTitle");
                        if (getNameMethod != null)
                        {
                            eventName = getNameMethod.Invoke(storyContent, null) as string;
                        }
                    }
                }

                // Read the actual story body text from contentLabel (TextMeshProUGUI)
                var contentLabelField = screenType.GetField("contentLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (contentLabelField != null)
                {
                    var contentLabel = contentLabelField.GetValue(screen);
                    if (contentLabel != null)
                    {
                        var textProp = contentLabel.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            eventText = textProp.GetValue(contentLabel) as string;
                            if (!string.IsNullOrEmpty(eventText))
                            {
                                eventText = Utilities.TextUtilities.StripRichTextTags(eventText).Trim();
                            }
                        }
                    }
                }

                // Also try currentTextContent StringBuilder
                if (string.IsNullOrEmpty(eventText))
                {
                    var textContentField = screenType.GetField("currentTextContent", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (textContentField != null)
                    {
                        var textContent = textContentField.GetValue(screen);
                        if (textContent != null)
                        {
                            eventText = Utilities.TextUtilities.StripRichTextTags(textContent.ToString()).Trim();
                        }
                    }
                }

                // Build announcement
                var announcement = new System.Text.StringBuilder();
                announcement.Append("Event");
                if (!string.IsNullOrEmpty(eventName))
                {
                    announcement.Append($": {eventName}");
                }
                announcement.Append(". ");
                if (!string.IsNullOrEmpty(eventText))
                {
                    announcement.Append(eventText);
                    announcement.Append(". ");
                }
                announcement.Append("Press F1 for help.");

                MonsterTrainAccessibility.ScreenReader?.Speak(announcement.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading story event: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Random event. Press F1 for help.");
            }
        }
    }
}
