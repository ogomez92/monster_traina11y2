using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from Dialog popup buttons (Yes/No confirmation dialogs).
    /// </summary>
    public static class DialogTextReader
    {
        /// <summary>
        /// Get text for buttons inside Dialog popups (Yes/No confirmation dialogs).
        /// Returns the dialog content text along with the button label.
        /// </summary>
        public static string GetDialogButtonText(GameObject go, ref string lastAnnouncedDialogText)
        {
            try
            {
                // Check if this looks like a dialog button
                string goName = go.name.ToLower();
                bool isDialogButton = goName.Contains("button") &&
                    (goName.Contains("yes") || goName.Contains("no") || goName.Contains("ok") ||
                     goName.Contains("cancel") || goName.Contains("confirm"));

                if (!isDialogButton)
                    return null;

                // Find the root dialog/popup container by walking up
                Component lastDialogComponent = null;
                Transform dialogRoot = FindDialogRoot(go.transform, ref lastDialogComponent);
                if (dialogRoot == null)
                {
                    MonsterTrainAccessibility.LogInfo("Could not find dialog root");
                    return null;
                }

                MonsterTrainAccessibility.LogInfo($"Found dialog root: {dialogRoot.name}");

                // Search for the dialog question text - look for visible TMP text that's NOT on buttons
                string dialogText = FindVisibleDialogText(dialogRoot.gameObject, go, lastDialogComponent);

                if (string.IsNullOrEmpty(dialogText))
                {
                    MonsterTrainAccessibility.LogInfo("Could not find dialog content text");
                    return null;
                }

                // Strip rich text tags
                dialogText = TextUtilities.StripRichTextTags(dialogText.Trim());

                // Get the button label
                string buttonLabel = GetDirectText(go);
                // If text is short (1-2 chars like icon "A"), use the cleaned GameObject name instead
                if (string.IsNullOrEmpty(buttonLabel) || buttonLabel.Length <= 2)
                {
                    buttonLabel = CleanGameObjectName(go.name);
                }

                // Check if this is the same dialog we already announced
                if (lastAnnouncedDialogText == dialogText)
                {
                    MonsterTrainAccessibility.LogInfo($"Dialog button (same dialog): '{buttonLabel}'");
                    return buttonLabel;
                }

                // New dialog - announce full text and remember it
                lastAnnouncedDialogText = dialogText;
                MonsterTrainAccessibility.LogInfo($"Dialog button detected (new): '{dialogText}' - Button: '{buttonLabel}'");

                return $"Dialog: {dialogText}. {buttonLabel}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting dialog button text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find the root container of a dialog by walking up from a button.
        /// </summary>
        public static Transform FindDialogRoot(Transform buttonTransform, ref Component lastDialogComponent)
        {
            // Find Dialog component and read from data.content
            Transform searchParent = buttonTransform;
            while (searchParent != null)
            {
                foreach (var comp in searchParent.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "Dialog")
                    {
                        // Found Dialog - store it and return this transform
                        lastDialogComponent = comp;
                        return searchParent;
                    }
                }
                searchParent = searchParent.parent;
            }

            // Fallback: return the button's grandparent
            return buttonTransform.parent?.parent;
        }

        /// <summary>
        /// Get the dialog content text from Dialog.data.content field.
        /// </summary>
        public static string GetDialogDataContent(Component dialogComponent)
        {
            if (dialogComponent == null)
                return null;

            try
            {
                var dataField = dialogComponent.GetType().GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataField == null)
                    return null;

                var data = dataField.GetValue(dialogComponent);
                if (data == null)
                    return null;

                var contentField = data.GetType().GetField("content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (contentField != null)
                {
                    var content = contentField.GetValue(data);
                    if (content != null)
                    {
                        return content.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting dialog data content: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find visible dialog text - first try Dialog.data.content, then search children.
        /// </summary>
        public static string FindVisibleDialogText(GameObject dialogRoot, GameObject excludeButton, Component lastDialogComponent)
        {
            // First, try to get text from Dialog.data.content (the actual current dialog content)
            if (lastDialogComponent != null)
            {
                string dataContent = GetDialogDataContent(lastDialogComponent);
                if (!string.IsNullOrEmpty(dataContent) && !TextUtilities.IsPlaceholderText(dataContent))
                {
                    MonsterTrainAccessibility.LogInfo($"Got dialog text from data.content: '{dataContent}'");
                    return dataContent;
                }
            }

            // Fallback: search TMP text in children
            try
            {
                string bestText = null;
                int bestLength = 0;

                var allTransforms = dialogRoot.GetComponentsInChildren<Transform>(false);

                foreach (var child in allTransforms)
                {
                    if (!child.gameObject.activeInHierarchy)
                        continue;

                    if (IsInsideButton(child, dialogRoot.transform))
                        continue;

                    string text = GetTMPTextDirect(child.gameObject);
                    if (string.IsNullOrEmpty(text))
                        continue;

                    text = text.Trim();
                    if (text.Length < 10)
                        continue;

                    string lower = text.ToLower();
                    if (lower == "yes" || lower == "no" || lower == "ok" || lower == "cancel")
                        continue;

                    bool hasQuestion = text.Contains("?");
                    int score = text.Length + (hasQuestion ? 1000 : 0);

                    if (score > bestLength)
                    {
                        bestText = text;
                        bestLength = score;
                    }
                }

                return bestText;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding visible dialog text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Check if a transform is inside a button element.
        /// </summary>
        public static bool IsInsideButton(Transform t, Transform root)
        {
            Transform current = t;
            while (current != null && current != root)
            {
                string name = current.name.ToLower();
                if (name.Contains("button") || name.Contains("yes") || name.Contains("no") ||
                    name.Contains("ok") || name.Contains("cancel") || name.Contains("confirm"))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Check if the given GameObject is inside a Dialog context.
        /// </summary>
        public static bool IsInDialogContext(GameObject go)
        {
            if (go == null) return false;

            Transform current = go.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "Dialog")
                    {
                        return true;
                    }
                }
                current = current.parent;
                depth++;
            }
            return false;
        }

        // Helper methods used by dialog text extraction

        private static string GetDirectText(GameObject go)
        {
            string text = GetTMPText(go);
            if (!string.IsNullOrEmpty(text))
                return text.Trim();

            var uiText = go.GetComponentInChildren<UnityEngine.UI.Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                return uiText.text.Trim();

            return null;
        }

        private static string GetTMPTextDirect(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            return textProperty.GetValue(component) as string;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetTMPText(GameObject go)
        {
            try
            {
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return name;
        }
    }
}
