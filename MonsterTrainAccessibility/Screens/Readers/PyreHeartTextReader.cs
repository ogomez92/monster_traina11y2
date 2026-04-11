using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text for the Run Setup pyre heart selection tiles
    /// (RunSetupPyreHeartSelectionItemUI). Mirrors the clan tile reader:
    /// announces name, HP/attack, ability description, locked state, and
    /// unlock condition when locked.
    /// </summary>
    public static class PyreHeartTextReader
    {
        public static string GetRunSetupPyreHeartItemText(GameObject go)
        {
            try
            {
                Component itemUi = null;
                Transform current = go.transform;
                while (current != null && itemUi == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "RunSetupPyreHeartSelectionItemUI")
                        {
                            itemUi = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (itemUi == null) return null;

                var itemType = itemUi.GetType();

                // PyreHeartOptionData property (nullable)
                var optionProp = itemType.GetProperty("PyreHeartOptionData",
                    BindingFlags.Public | BindingFlags.Instance);
                object option = optionProp?.GetValue(itemUi);
                if (option == null) return null;

                var optionType = option.GetType();

                bool isRandom = false;
                var isRandomProp = optionType.GetProperty("IsRandom", BindingFlags.Public | BindingFlags.Instance);
                if (isRandomProp != null && isRandomProp.GetValue(option) is bool r) isRandom = r;

                bool isLocked = false;
                var isLockedField = optionType.GetField("isLocked", BindingFlags.Public | BindingFlags.Instance);
                if (isLockedField != null && isLockedField.GetValue(option) is bool l1) isLocked = l1;
                if (!isLocked)
                {
                    var isLockedProp = itemType.GetProperty("IsLocked", BindingFlags.Public | BindingFlags.Instance);
                    if (isLockedProp != null && isLockedProp.GetValue(itemUi) is bool l2) isLocked = l2;
                }

                if (isRandom)
                {
                    return isLocked ? "Random pyre heart, Locked" : "Random pyre heart";
                }

                // pyreCharacterData (CharacterData)
                object characterData = null;
                var charField = optionType.GetField("pyreCharacterData", BindingFlags.Public | BindingFlags.Instance);
                if (charField != null) characterData = charField.GetValue(option);
                if (characterData == null) return isLocked ? "Locked pyre heart" : null;

                var charType = characterData.GetType();

                // Name
                string name = null;
                var getName = charType.GetMethod("GetName", Type.EmptyTypes);
                if (getName != null) name = getName.Invoke(characterData, null) as string;
                if (string.IsNullOrEmpty(name))
                {
                    var getNameKey = charType.GetMethod("GetNameKey", Type.EmptyTypes);
                    if (getNameKey != null)
                    {
                        var key = getNameKey.Invoke(characterData, null) as string;
                        name = LocalizationHelper.TryLocalize(key);
                    }
                }
                if (string.IsNullOrEmpty(name)) name = "Unknown pyre heart";
                name = TextUtilities.StripRichTextTags(name);

                // PyreHeartData
                object pyreHeartData = null;
                var getPyreHeartData = charType.GetMethod("GetPyreHeartData", Type.EmptyTypes);
                if (getPyreHeartData != null) pyreHeartData = getPyreHeartData.Invoke(characterData, null);

                int? hp = null, attack = null;
                string description = null;
                if (pyreHeartData != null)
                {
                    var phType = pyreHeartData.GetType();

                    var getHP = phType.GetMethod("GetStartingHP", Type.EmptyTypes);
                    if (getHP != null && getHP.Invoke(pyreHeartData, null) is int hpVal) hp = hpVal;

                    var getAttack = phType.GetMethod("GetAttack", Type.EmptyTypes);
                    if (getAttack != null && getAttack.Invoke(pyreHeartData, null) is int atkVal) attack = atkVal;

                    // Description comes from the pyre artifact's description.
                    var getArtifact = phType.GetMethod("GetPyreArtifact", Type.EmptyTypes);
                    if (getArtifact != null)
                    {
                        var artifact = getArtifact.Invoke(pyreHeartData, null);
                        if (artifact != null)
                        {
                            var getDesc = artifact.GetType().GetMethod("GetDescription", Type.EmptyTypes);
                            if (getDesc != null) description = getDesc.Invoke(artifact, null) as string;
                        }
                    }
                }

                var sb = new StringBuilder();
                sb.Append("Pyre heart: ");
                sb.Append(name);

                if (isLocked)
                {
                    sb.Append(", Locked");

                    string unlockText = TryGetUnlockText(pyreHeartData);
                    if (!string.IsNullOrEmpty(unlockText))
                    {
                        sb.Append(": ");
                        sb.Append(TextUtilities.StripRichTextTags(unlockText));
                    }
                }
                else
                {
                    if (hp.HasValue) sb.Append($", {hp.Value} HP");
                    if (attack.HasValue) sb.Append($", {attack.Value} attack");
                }

                if (!string.IsNullOrEmpty(description))
                {
                    sb.Append(". ");
                    sb.Append(TextUtilities.StripRichTextTags(description));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting pyre heart item text: {ex.Message}");
            }
            return null;
        }

        private static string TryGetUnlockText(object pyreHeartData)
        {
            if (pyreHeartData == null) return null;
            try
            {
                var getUnlock = pyreHeartData.GetType().GetMethod("GetUnlockData", Type.EmptyTypes);
                var unlock = getUnlock?.Invoke(pyreHeartData, null);
                if (unlock == null) return null;

                var unlockType = unlock.GetType();
                var getKey = unlockType.GetMethod("GetDescriptionKey", Type.EmptyTypes);
                var key = getKey?.Invoke(unlock, null) as string;
                if (string.IsNullOrEmpty(key)) return null;

                var localized = LocalizationHelper.Localize(key);
                if (string.IsNullOrEmpty(localized)) localized = key;

                // Substitute [paramInt] placeholder if present.
                if (localized.Contains("[paramInt]"))
                {
                    var getParam = unlockType.GetMethod("GetParamInt", Type.EmptyTypes);
                    if (getParam != null && getParam.Invoke(unlock, null) is int p)
                    {
                        localized = localized.Replace("[paramInt]", p.ToString());
                    }
                }

                return localized;
            }
            catch { }
            return null;
        }
    }
}
