using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Shared UI visibility and text extraction helpers.
    /// </summary>
    public static class UITextHelper
    {
        /// <summary>
        /// Panel names that should be ignored when scanning for text.
        /// </summary>
        public static readonly HashSet<string> PanelBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "quitconfirmation", "exitdialog", "confirmdialog", "confirmationpopup",
            "quitpanel", "exitpanel", "confirmquit", "quitgame", "exitgame"
        };

        /// <summary>
        /// Check if a GameObject is actually visible (not hidden by CanvasGroup, disabled Canvas, or blacklisted).
        /// </summary>
        public static bool IsActuallyVisible(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
                return false;

            Transform current = go.transform;
            while (current != null)
            {
                string objName = current.name.Replace(" ", "").Replace("_", "");
                if (PanelBlacklist.Contains(objName))
                    return false;

                if (IsHiddenByCanvasGroup(current.gameObject))
                    return false;

                if (IsCanvasDisabled(current.gameObject))
                    return false;

                if (IsHiddenDialog(current.gameObject))
                    return false;

                current = current.parent;
            }

            return true;
        }

        /// <summary>
        /// Check if a GameObject is hidden by CanvasGroup (alpha = 0).
        /// </summary>
        public static bool IsHiddenByCanvasGroup(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "CanvasGroup")
                    {
                        var alphaProp = type.GetProperty("alpha");
                        if (alphaProp != null)
                        {
                            float alpha = (float)alphaProp.GetValue(component);
                            if (alpha <= 0.01f)
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a Canvas component is disabled.
        /// </summary>
        public static bool IsCanvasDisabled(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "Canvas")
                    {
                        var enabledProp = type.GetProperty("enabled");
                        if (enabledProp != null)
                        {
                            bool enabled = (bool)enabledProp.GetValue(component);
                            if (!enabled)
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a GameObject has a Dialog component that's not currently showing.
        /// </summary>
        public static bool IsHiddenDialog(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "Dialog")
                    {
                        // Check IsShowing property
                        var isShowingProp = type.GetProperty("IsShowing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (isShowingProp != null)
                        {
                            bool isShowing = (bool)isShowingProp.GetValue(component);
                            if (!isShowing) return true;
                        }

                        // Check isShowing field
                        var isShowingField = type.GetField("isShowing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (isShowingField != null)
                        {
                            bool isShowing = (bool)isShowingField.GetValue(component);
                            if (!isShowing) return true;
                        }

                        // Check showing field
                        var showingField = type.GetField("showing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (showingField != null)
                        {
                            bool showing = (bool)showingField.GetValue(component);
                            if (!showing) return true;
                        }

                        // Check isOpen property
                        var isOpenProp = type.GetProperty("isOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (isOpenProp != null)
                        {
                            bool isOpen = (bool)isOpenProp.GetValue(component);
                            if (!isOpen) return true;
                        }

                        // Check overlay alpha
                        var overlayField = type.GetField("overlay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (overlayField != null)
                        {
                            var overlay = overlayField.GetValue(component);
                            if (overlay != null)
                            {
                                var overlayType = overlay.GetType();
                                var colorProp = overlayType.GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                                if (colorProp != null)
                                {
                                    var color = colorProp.GetValue(overlay);
                                    if (color != null)
                                    {
                                        var aField = color.GetType().GetField("a", BindingFlags.Public | BindingFlags.Instance);
                                        if (aField != null)
                                        {
                                            float alpha = (float)aField.GetValue(color);
                                            if (alpha <= 0.01f) return true;
                                        }
                                    }
                                }

                                var overlayGoProp = overlayType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                                if (overlayGoProp != null)
                                {
                                    var overlayGo = overlayGoProp.GetValue(overlay) as GameObject;
                                    if (overlayGo != null && !overlayGo.activeInHierarchy)
                                        return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Get text from a UI text component (TMP_Text, Text, etc.) via reflection.
        /// </summary>
        public static string GetTextFromComponent(object textComponent)
        {
            if (textComponent == null) return null;

            try
            {
                var type = textComponent.GetType();

                var textProp = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProp != null)
                {
                    var text = textProp.GetValue(textComponent) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }

                var getParsedMethod = type.GetMethod("GetParsedText", BindingFlags.Public | BindingFlags.Instance);
                if (getParsedMethod != null && getParsedMethod.GetParameters().Length == 0)
                {
                    var text = getParsedMethod.Invoke(textComponent, null) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting text from component: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get all text strings from child text components of a GameObject.
        /// </summary>
        public static List<string> GetAllTextFromChildren(GameObject go)
        {
            var texts = new List<string>();

            if (go == null) return texts;

            try
            {
                var components = go.GetComponentsInChildren<Component>(false);

                foreach (var comp in components)
                {
                    if (comp == null || !comp.gameObject.activeInHierarchy) continue;

                    string typeName = comp.GetType().Name;

                    if (typeName.Contains("Text"))
                    {
                        string text = GetTextFromComponent(comp);
                        if (!string.IsNullOrEmpty(text) && text.Length > 1)
                        {
                            string lowerText = text.ToLower();
                            if (!lowerText.Contains("view") &&
                                !lowerText.StartsWith("$") &&
                                !text.All(c => char.IsDigit(c) || c == ' '))
                            {
                                texts.Add(text);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting text from children: {ex.Message}");
            }

            return texts;
        }

        /// <summary>
        /// Get TMP text from an object via reflection (handles label fields).
        /// </summary>
        public static string GetTMPTextFromObject(object labelObj)
        {
            if (labelObj == null) return null;

            try
            {
                var labelType = labelObj.GetType();

                // If it's a Component, try to get text directly
                if (labelObj is Component comp)
                {
                    return GetTextFromComponent(comp);
                }

                // Try the text property
                var textProp = labelType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProp != null)
                {
                    var text = textProp.GetValue(labelObj) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Find a child transform by name (case-insensitive, partial match).
        /// </summary>
        public static Transform FindChildRecursive(Transform parent, string nameContains)
        {
            string lowerName = nameContains.ToLower();
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(lowerName))
                    return child;

                var found = FindChildRecursive(child, nameContains);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
