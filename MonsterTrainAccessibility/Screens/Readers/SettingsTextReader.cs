using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from settings screen elements (dropdowns, sliders, toggles).
    /// </summary>
    public static class SettingsTextReader
    {
        /// <summary>
        /// Get text for settings screen elements (dropdowns, sliders, toggles)
        /// </summary>
        public static string GetSettingsElementText(GameObject go)
        {
            try
            {
                string settingLabel = null;
                Transform current = go.transform;

                for (int i = 0; i < 3 && current.parent != null; i++)
                {
                    Transform parent = current.parent;

                    foreach (var component in parent.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "SettingsEntry")
                        {
                            settingLabel = CleanSettingsLabel(parent.name);
                            break;
                        }
                    }

                    if (settingLabel != null) break;
                    current = parent;
                }

                if (string.IsNullOrEmpty(settingLabel))
                    return null;

                string value = null;

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    string typeName = type.Name;

                    if (typeName.Contains("Dropdown"))
                    {
                        value = GetDropdownValue(component);
                        break;
                    }
                    else if (typeName.Contains("Slider"))
                    {
                        value = GetSliderValue(go);
                        break;
                    }
                    else if (typeName.Contains("Toggle"))
                    {
                        value = GetToggleValue(go);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(value))
                {
                    value = GetTMPText(go);
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = TextUtilities.StripRichTextTags(value.Trim());
                    }
                }

                if (!string.IsNullOrEmpty(value))
                {
                    return $"{settingLabel}: {value}";
                }

                return settingLabel;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting settings element text: {ex.Message}");
            }
            return null;
        }

        public static string CleanSettingsLabel(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            name = name.Replace("Dropdown", "");
            name = name.Replace("dropdown", "");
            name = name.Replace("Toggle", "");
            name = name.Replace("toggle", "");
            name = name.Replace("Slider", "");
            name = name.Replace("slider", "");
            name = name.Replace("Control", "");
            name = name.Replace("control", "");
            name = name.Replace("Option", "");
            name = name.Replace("option", "");
            name = name.Replace("Setting", "");
            name = name.Replace("setting", "");
            name = name.Replace("Entry", "");
            name = name.Replace("entry", "");
            name = name.Replace("input", "");
            name = name.Replace("Input", "");

            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            name = name.Replace("BG", "Background");
            name = name.Replace("SFX", "Sound Effects");
            name = name.Replace("VSync", "V-Sync");
            name = name.Replace("Vsync", "V-Sync");
            name = name.Replace("UI", "Interface");

            return name.Trim();
        }

        public static string GetDropdownValue(Component dropdown)
        {
            try
            {
                var type = dropdown.GetType();

                var getCurrentTextMethod = type.GetMethod("GetCurrentText") ??
                                           type.GetMethod("GetText") ??
                                           type.GetMethod("GetSelectedText");
                if (getCurrentTextMethod != null)
                {
                    var result = getCurrentTextMethod.Invoke(dropdown, null);
                    if (result != null)
                        return result.ToString();
                }

                var textProp = type.GetProperty("currentText") ??
                               type.GetProperty("text") ??
                               type.GetProperty("captionText");
                if (textProp != null)
                {
                    var result = textProp.GetValue(dropdown);
                    if (result != null)
                    {
                        var textComponent = result as Component;
                        if (textComponent != null)
                        {
                            var tmpText = GetTMPTextDirect(textComponent.gameObject);
                            if (!string.IsNullOrEmpty(tmpText))
                                return tmpText;
                        }
                        return result.ToString();
                    }
                }

                var dropdownGO = dropdown.gameObject;
                foreach (Transform child in dropdownGO.transform)
                {
                    string childName = child.name.ToLower();
                    if (childName.Contains("label") || childName.Contains("caption") || childName.Contains("text"))
                    {
                        string text = GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(text))
                            return text.Trim();
                    }
                }

                string anyText = GetTMPText(dropdownGO);
                if (!string.IsNullOrEmpty(anyText))
                    return anyText.Trim();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting dropdown value: {ex.Message}");
            }
            return null;
        }

        public static string GetSliderValue(GameObject go)
        {
            try
            {
                var slider = go.GetComponent<Slider>();
                if (slider != null)
                {
                    int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
                    return $"{percent}%";
                }

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();

                    var valueProp = type.GetProperty("value") ?? type.GetProperty("normalizedValue");
                    if (valueProp != null)
                    {
                        var val = valueProp.GetValue(component);
                        if (val is float f)
                        {
                            int percent = Mathf.RoundToInt(f * 100);
                            return $"{percent}%";
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static string GetToggleValue(GameObject go)
        {
            try
            {
                var toggle = go.GetComponent<Toggle>();
                if (toggle != null)
                {
                    return toggle.isOn ? "on" : "off";
                }

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();

                    var isOnProp = type.GetProperty("isOn") ?? type.GetProperty("IsOn");
                    if (isOnProp != null)
                    {
                        var val = isOnProp.GetValue(component);
                        if (val is bool b)
                            return b ? "on" : "off";
                    }

                    var toggleField = type.GetField("toggle", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (toggleField != null)
                    {
                        var innerToggle = toggleField.GetValue(component) as Toggle;
                        if (innerToggle != null)
                            return innerToggle.isOn ? "on" : "off";
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get text for toggle/checkbox controls with their label
        /// </summary>
        public static string GetToggleText(GameObject go)
        {
            try
            {
                // First check if this is the Trial toggle on BattleIntroScreen
                string trialText = BattleIntroTextReader.GetTrialToggleText(go);
                if (!string.IsNullOrEmpty(trialText))
                {
                    return trialText;
                }

                var unityToggle = go.GetComponent<Toggle>();
                if (unityToggle != null)
                {
                    string label = GetToggleLabelFromHierarchy(go);
                    string state = unityToggle.isOn ? "on" : "off";
                    return string.IsNullOrEmpty(label) ? state : $"{label}: {state}";
                }

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    string typeName = type.Name;

                    if (typeName.Contains("Toggle") || typeName.Contains("Checkbox"))
                    {
                        bool? isOn = null;
                        var isOnProp = type.GetProperty("isOn");
                        if (isOnProp != null)
                        {
                            isOn = isOnProp.GetValue(component) as bool?;
                        }
                        if (isOn == null)
                        {
                            var isCheckedProp = type.GetProperty("isChecked");
                            if (isCheckedProp != null)
                            {
                                isOn = isCheckedProp.GetValue(component) as bool?;
                            }
                        }
                        if (isOn == null)
                        {
                            var isOnField = type.GetField("m_IsOn", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (isOnField != null)
                            {
                                isOn = isOnField.GetValue(component) as bool?;
                            }
                        }

                        if (isOn.HasValue)
                        {
                            string label = GetToggleLabelFromHierarchy(go);
                            string state = isOn.Value ? "on" : "off";
                            return string.IsNullOrEmpty(label) ? state : $"{label}: {state}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting toggle text: {ex.Message}");
            }
            return null;
        }

        private static string GetToggleLabelFromHierarchy(GameObject go)
        {
            try
            {
                Transform parent = go.transform.parent;
                if (parent != null)
                {
                    foreach (Transform sibling in parent)
                    {
                        if (sibling == go.transform) continue;

                        string sibName = sibling.name.ToLower();
                        if (sibName.Contains("onlabel") || sibName.Contains("offlabel") ||
                            sibName == "on" || sibName == "off")
                            continue;

                        string sibText = GetTMPTextDirect(sibling.gameObject);
                        if (string.IsNullOrEmpty(sibText))
                        {
                            var uiText = sibling.GetComponent<Text>();
                            sibText = uiText?.text;
                        }

                        if (!string.IsNullOrEmpty(sibText) && sibText.Length > 2)
                        {
                            string lower = sibText.ToLower().Trim();
                            if (lower != "on" && lower != "off")
                            {
                                return sibText.Trim();
                            }
                        }
                    }

                    string parentName = CleanGameObjectName(parent.name);
                    if (!string.IsNullOrEmpty(parentName) && parentName.Length > 2)
                    {
                        return parentName;
                    }

                    if (parent.parent != null)
                    {
                        foreach (Transform uncle in parent.parent)
                        {
                            if (uncle == parent) continue;

                            string uncleText = GetTMPTextDirect(uncle.gameObject);
                            if (string.IsNullOrEmpty(uncleText))
                            {
                                var uiText = uncle.GetComponent<Text>();
                                uncleText = uiText?.text;
                            }

                            if (!string.IsNullOrEmpty(uncleText) && uncleText.Length > 2)
                            {
                                string lower = uncleText.ToLower().Trim();
                                if (lower != "on" && lower != "off")
                                {
                                    return uncleText.Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch { }
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
