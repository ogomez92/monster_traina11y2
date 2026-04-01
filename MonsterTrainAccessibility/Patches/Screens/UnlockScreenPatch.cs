using HarmonyLib;
using System;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Announce unlocks (level ups, new cards/relics, clan unlocks, etc.) as they appear.
    /// </summary>
    public static class UnlockScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("UnlockScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("UnlockScreen not found");
                    return;
                }

                var showMethod = AccessTools.Method(targetType, "ShowNextUnlock");
                if (showMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(UnlockScreenPatch).GetMethod(nameof(ShowNextUnlockPostfix)));
                    harmony.Patch(showMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched UnlockScreen.ShowNextUnlock");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch UnlockScreen: {ex.Message}");
            }
        }

        public static void ShowNextUnlockPostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;

                var currentItemProp = instanceType.GetProperty("currentItem", bindingFlags);
                if (currentItemProp == null) return;

                var currentItem = currentItemProp.GetValue(__instance);
                if (currentItem == null) return;

                var itemType = currentItem.GetType();

                var sourceField = itemType.GetField("source");
                string source = sourceField?.GetValue(currentItem)?.ToString() ?? "";

                var headerContentField = itemType.GetField("headerTextContent");
                string headerContent = headerContentField?.GetValue(currentItem) as string ?? "";

                var headerLevelField = itemType.GetField("headerLevel");
                int headerLevel = -1;
                if (headerLevelField != null)
                {
                    var lvl = headerLevelField.GetValue(currentItem);
                    if (lvl is int l) headerLevel = l;
                }

                string unlockedCardName = null;
                var cardDataField = itemType.GetField("unlockedCardData");
                if (cardDataField != null)
                {
                    var cardData = cardDataField.GetValue(currentItem);
                    if (cardData != null)
                    {
                        var getNameMethod = cardData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                            unlockedCardName = getNameMethod.Invoke(cardData, null) as string;
                    }
                }

                string unlockedRelicName = null;
                var relicDataField = itemType.GetField("unlockedRelicData");
                if (relicDataField != null)
                {
                    var relicData = relicDataField.GetValue(currentItem);
                    if (relicData != null)
                    {
                        var getNameMethod = relicData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                            unlockedRelicName = getNameMethod.Invoke(relicData, null) as string;
                    }
                }

                string featureTitle = null;
                var featureDataField = itemType.GetField("unlockedFeatureData");
                if (featureDataField != null)
                {
                    var featureData = featureDataField.GetValue(currentItem);
                    if (featureData != null)
                    {
                        var titleField = featureData.GetType().GetField("title");
                        if (titleField != null)
                            featureTitle = titleField.GetValue(featureData) as string;
                    }
                }

                string announcement = BuildUnlockAnnouncement(source, headerContent, headerLevel,
                    unlockedCardName, unlockedRelicName, featureTitle);

                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Unlock: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in UnlockScreenPatch: {ex.Message}");
            }
        }

        private static string BuildUnlockAnnouncement(string source, string headerContent, int headerLevel,
            string cardName, string relicName, string featureTitle)
        {
            var sb = new StringBuilder();

            switch (source)
            {
                case "ClanLevelUp":
                    sb.Append($"{headerContent} reached level {headerLevel}! ");
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;

                case "NewClan":
                    sb.Append($"New clan unlocked: {featureTitle ?? headerContent}! ");
                    break;

                case "CovenantUnlocked":
                    sb.Append($"Covenant mode unlocked! {featureTitle}");
                    break;

                case "ChallengeLevelUp":
                    sb.Append($"New covenant level unlocked! {featureTitle}");
                    break;

                case "CardMastery":
                    sb.Append("Card mastery achieved! ");
                    break;

                case "DivineCardMastery":
                    sb.Append("Divine card mastery achieved! ");
                    break;

                case "FeatureUnlocked":
                    sb.Append($"Feature unlocked: {featureTitle}! ");
                    break;

                case "MasteryCardFrameUnlocked":
                    sb.Append("Mastery card frame unlocked! ");
                    break;

                default:
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    else if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    else if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;
            }

            sb.Append("Press Enter to continue.");
            return sb.ToString().Trim();
        }
    }
}
