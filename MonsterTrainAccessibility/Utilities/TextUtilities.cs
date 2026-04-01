using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Shared text processing utilities for screen reader output.
    /// </summary>
    public static class TextUtilities
    {
        /// <summary>
        /// Strip rich text tags from text for screen reader output.
        /// Removes Unity rich text tags like nobr, color, upgradeHighlight, etc.
        /// </summary>
        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = Regex.Replace(text, @"<[^>]+>", "");
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Convert sprite tags to readable text and strip remaining rich text.
        /// Handles: sprite name=Gold, sprite name="Gold", sprite="Gold", etc.
        /// </summary>
        public static string CleanSpriteTagsForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            bool hadSprite = text.Contains("sprite");
            if (hadSprite)
            {
                MonsterTrainAccessibility.LogInfo($"CleanSpriteTagsForSpeech input: '{text}'");
            }

            // Convert sprite tags to readable text
            text = Regex.Replace(
                text,
                @"<sprite\s+name\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                RegexOptions.IgnoreCase);

            // Also handle <sprite=X> or <sprite="X"> format
            text = Regex.Replace(
                text,
                @"<sprite\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                RegexOptions.IgnoreCase);

            // Strip any remaining rich text tags
            text = Regex.Replace(text, @"<[^>]+>", "");

            // Clean up double spaces
            text = Regex.Replace(text, @"\s+", " ");

            string result = text.Trim();
            if (hadSprite)
            {
                MonsterTrainAccessibility.LogInfo($"CleanSpriteTagsForSpeech output: '{result}'");
            }
            return result;
        }

        /// <summary>
        /// Check if text appears to be placeholder/debug text that shouldn't be read.
        /// </summary>
        public static bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string lower = text.ToLower();

            if (lower.Contains("placeholder"))
                return true;
            if (lower.Contains("(should"))
                return true;
            if (lower.Contains("todo"))
                return true;
            if (lower.Contains("fixme"))
                return true;

            return false;
        }

        /// <summary>
        /// Clean up asset names to be more readable.
        /// </summary>
        public static string CleanAssetName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            name = name.Replace("_", " ");
            name = name.Replace("Data", "");
            name = name.Replace("Scenario", "");

            // Add spaces before capital letters
            name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name.Trim();
        }
    }
}
