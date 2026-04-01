using HarmonyLib;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Dialog display - auto-read FTUE/tutorial dialogs
    /// </summary>
    public static class DialogPatch
    {
        private static string _lastDialogText = null;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Patch DialogScreen.ShowDialog (the entry point)
                var dialogScreenType = AccessTools.TypeByName("DialogScreen");
                if (dialogScreenType != null)
                {
                    MonsterTrainAccessibility.LogInfo($"Found DialogScreen type: {dialogScreenType.FullName}");

                    foreach (var m in dialogScreenType.GetMethods())
                    {
                        if (m.Name == "ShowDialog" && m.GetParameters().Length == 1)
                        {
                            var postfix = new HarmonyMethod(typeof(DialogPatch).GetMethod(nameof(DialogScreenPostfix)));
                            harmony.Patch(m, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched DialogScreen.ShowDialog");
                            break;
                        }
                    }
                }
                else
                {
                    MonsterTrainAccessibility.LogError("DialogScreen type not found!");
                }

                // Also patch Dialog.Show as backup
                var targetType = AccessTools.TypeByName("Dialog");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogError("Dialog type not found!");
                    return;
                }

                MonsterTrainAccessibility.LogInfo($"Found Dialog type: {targetType.FullName}");

                // Find Show method
                MethodInfo method = null;
                foreach (var m in targetType.GetMethods())
                {
                    if (m.Name == "Show" && m.GetParameters().Length == 1)
                    {
                        method = m;
                        MonsterTrainAccessibility.LogInfo($"Found Dialog.Show with param: {m.GetParameters()[0].ParameterType.FullName}");
                        break;
                    }
                }

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DialogPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched Dialog.Show for FTUE/tutorial auto-read");
                }
                else
                {
                    MonsterTrainAccessibility.LogError("Dialog.Show method not found!");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch Dialog: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void DialogScreenPostfix(object __0)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("[DialogPatch] DialogScreen.ShowDialog called!");

                if (__0 != null)
                {
                    var contentField = __0.GetType().GetField("content");
                    if (contentField != null)
                    {
                        string content = contentField.GetValue(__0) as string;
                        if (!string.IsNullOrEmpty(content))
                        {
                            MonsterTrainAccessibility.LogInfo($"[DialogPatch] DialogScreen content: {content.Substring(0, Math.Min(100, content.Length))}");

                            if (content != _lastDialogText)
                            {
                                _lastDialogText = content;
                                string cleanText = Regex.Replace(content, @"<[^>]+>", "");
                                MonsterTrainAccessibility.ScreenReader?.Speak($"Dialog: {cleanText}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DialogScreen patch: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, object __0)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("[DialogPatch] Postfix called!");

                if (__instance == null)
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] __instance is null");
                    return;
                }

                // Get the dialog content from the Data parameter
                string content = null;
                if (__0 != null)
                {
                    MonsterTrainAccessibility.LogInfo($"[DialogPatch] Data type: {__0.GetType().FullName}");
                    var contentField = __0.GetType().GetField("content");
                    if (contentField != null)
                    {
                        content = contentField.GetValue(__0) as string;
                        MonsterTrainAccessibility.LogInfo($"[DialogPatch] Got content from field: {content?.Substring(0, Math.Min(50, content?.Length ?? 0))}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("[DialogPatch] content field not found on Data");
                    }
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] __0 (Data) is null");
                }

                // Try to get content directly from the dialog instance
                if (string.IsNullOrEmpty(content))
                {
                    var instanceType = __instance.GetType();
                    var contentLabelField = instanceType.GetField("contentLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (contentLabelField != null)
                    {
                        var label = contentLabelField.GetValue(__instance);
                        if (label != null)
                        {
                            var textProp = label.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                content = textProp.GetValue(label) as string;
                                MonsterTrainAccessibility.LogInfo($"[DialogPatch] Got content from contentLabel: {content?.Substring(0, Math.Min(50, content?.Length ?? 0))}");
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(content) && content != _lastDialogText)
                {
                    _lastDialogText = content;
                    MonsterTrainAccessibility.LogInfo($"[DialogPatch] Speaking dialog: {content.Substring(0, Math.Min(100, content.Length))}...");

                    // Clean up any rich text tags
                    string cleanText = Regex.Replace(content, @"<[^>]+>", "");

                    // Speak the dialog content
                    MonsterTrainAccessibility.ScreenReader?.Speak($"Tutorial: {cleanText}");
                }
                else if (string.IsNullOrEmpty(content))
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] No content found");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("[DialogPatch] Same content as before, not repeating");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in Dialog patch: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
