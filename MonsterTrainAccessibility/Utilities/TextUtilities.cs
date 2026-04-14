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

            text = Regex.Replace(text, @"<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Fix singular/plural grammar for "1 stacks" / "1 turns" that the game's
        /// localization templates leave in plural form regardless of count.
        /// </summary>
        public static string FixSingularGrammar(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"\b1 stacks\b", "1 stack");
            text = Regex.Replace(text, @"\b1 turns\b", "1 turn");
            text = Regex.Replace(text, @"\b1 times\b", "1 time");
            return text;
        }

        /// <summary>
        /// Strip trailing sentence punctuation and whitespace so callers can
        /// append their own ". " separator without producing ".." artifacts.
        /// </summary>
        public static string TrimTrailingPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return text.TrimEnd('.', ' ', '\t', '\r', '\n');
        }

        /// <summary>
        /// Convert sprite tags to readable text and strip remaining rich text.
        /// Handles: sprite name=Gold, sprite name="Gold", sprite="Gold", etc.
        /// </summary>
        public static string CleanSpriteTagsForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Card text often glues a signed number onto a stat-icon sprite, e.g.
            // "+2<sprite name=\"Attack\">" or "-1<sprite=Health>". Rewrite those to
            // a natural "gains 2 attack" / "loses 1 health" phrase before the
            // generic sprite-tag replacement runs.
            text = Regex.Replace(
                text,
                @"([+-])(\d+)\s*<sprite(?:\s+name)?\s*=\s*[""']?(Attack|Health|Size|Ember|MagicPower)[""']?\s*/?>",
                match =>
                {
                    string verb = match.Groups[1].Value == "+" ? "gains" : "loses";
                    string n = match.Groups[2].Value;
                    string stat = ReadableSpriteName(match.Groups[3].Value).ToLowerInvariant();
                    return $" {verb} {n} {stat} ";
                },
                RegexOptions.IgnoreCase);

            // Convert <sprite name="X"> to a readable form (with leading/trailing space).
            text = Regex.Replace(
                text,
                @"<sprite\s+name\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + ReadableSpriteName(match.Groups[1].Value) + " ",
                RegexOptions.IgnoreCase);

            // Also handle <sprite=X> / <sprite="X"> format.
            text = Regex.Replace(
                text,
                @"<sprite\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + ReadableSpriteName(match.Groups[1].Value) + " ",
                RegexOptions.IgnoreCase);

            // Replace any remaining rich text tags with a space so adjacent tokens
            // (e.g. "Fire.<br>Conjure") don't get glued together.
            text = Regex.Replace(text, @"<[^>]+>", " ");

            // Collapse runs of whitespace, then fix the artifacts that introduces
            // around sentence punctuation (e.g. "Fire . Conjure" -> "Fire. Conjure").
            text = Regex.Replace(text, @"\s+", " ");
            text = Regex.Replace(text, @"\s+([.,;:!?])", "$1");

            return text.Trim();
        }

        /// <summary>
        /// Optional resolver that maps a TMP sprite asset name to a localized display
        /// name by querying the game's I2.Loc term list at runtime. Wired up by
        /// LocalizationHelper at startup; left null in unit/contextless calls.
        /// </summary>
        public static System.Func<string, string> SpriteNameResolver { get; set; }

        /// <summary>
        /// Convert a TextMeshPro sprite asset name (PascalCase) into a human-readable
        /// label. Tries the runtime resolver first (game-localized name), then falls
        /// back to PascalCase splitting.
        /// </summary>
        public static string ReadableSpriteName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return spriteName;

            try
            {
                var resolver = SpriteNameResolver;
                if (resolver != null)
                {
                    string resolved = resolver(spriteName);
                    if (!string.IsNullOrEmpty(resolved))
                        return StripRichTextTags(resolved);
                }
            }
            catch { }

            string spaced = Regex.Replace(spriteName, "([a-z0-9])([A-Z])", "$1 $2");
            spaced = Regex.Replace(spaced, "([A-Z]+)([A-Z][a-z])", "$1 $2");
            return spaced;
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
