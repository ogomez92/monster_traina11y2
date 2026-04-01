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

                // Try to get the story content from the storyContent field
                var storyContentField = screenType.GetField("storyContent", BindingFlags.NonPublic | BindingFlags.Instance);
                if (storyContentField != null)
                {
                    var storyContent = storyContentField.GetValue(screen);
                    if (storyContent != null)
                    {
                        var contentType = storyContent.GetType();

                        // Try to get event name
                        string eventName = null;
                        var getNameMethod = contentType.GetMethod("GetName") ?? contentType.GetMethod("GetTitle");
                        if (getNameMethod != null)
                        {
                            eventName = getNameMethod.Invoke(storyContent, null) as string;
                        }

                        // Fall back to KnotName field or property
                        if (string.IsNullOrEmpty(eventName))
                        {
                            var knotNameField = contentType.GetField("KnotName");
                            if (knotNameField != null)
                            {
                                eventName = knotNameField.GetValue(storyContent) as string;
                            }
                            else
                            {
                                var knotNameProp = contentType.GetProperty("KnotName");
                                if (knotNameProp != null)
                                {
                                    eventName = knotNameProp.GetValue(storyContent) as string;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(eventName))
                        {
                            MonsterTrainAccessibility.ScreenReader?.Speak($"Random event: {eventName}. Press F1 for help.");
                            return;
                        }
                    }
                }

                // Generic announcement
                MonsterTrainAccessibility.ScreenReader?.Speak("Random event. Listen for choices. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading story event: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Random event. Press F1 for help.");
            }
        }
    }
}
