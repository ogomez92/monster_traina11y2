using HarmonyLib;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts keyword and tooltip definitions for card text. Walks card effects,
    /// trait states, status effects, and bold-tagged keywords in card descriptions
    /// to build a list of localized "Keyword: explanation" strings.
    /// </summary>
    public static class CardKeywordReader
    {
        private static Dictionary<string, string> _keywordCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _keywordCacheInitialized = false;

        /// <summary>
        /// Build the full "Keywords: ..." string for a card by aggregating tooltips
        /// from trait states, effect data, status effects, and description text.
        /// Returns formatted string of keyword definitions.
        /// </summary>
        public static string GetCardKeywordTooltips(object cardState, object cardData, string cardDescription = null)
        {
            try
            {
                var tooltips = new List<string>();

                // Method 1: Get trait states from CardState - these have localized tooltip methods
                if (cardState != null)
                {
                    var stateType = cardState.GetType();

                    // Get trait states - CardTraitState objects have GetCardTooltipTitle/Text methods
                    var getTraitStatesMethod = stateType.GetMethod("GetTraitStates", Type.EmptyTypes);
                    if (getTraitStatesMethod != null)
                    {
                        var traitStates = getTraitStatesMethod.Invoke(cardState, null) as System.Collections.IEnumerable;
                        if (traitStates != null)
                        {
                            foreach (var traitState in traitStates)
                            {
                                if (traitState == null) continue;
                                string traitTooltip = ExtractTraitStateTooltip(traitState);
                                if (!string.IsNullOrEmpty(traitTooltip) && !tooltips.Any(t => t.StartsWith(traitTooltip.Split(':')[0] + ":")))
                                {
                                    tooltips.Add(traitTooltip);
                                }
                            }
                        }
                    }

                    // Also try GetEffectTooltipData or similar methods
                    var getTooltipsMethod = stateType.GetMethods()
                        .FirstOrDefault(m => m.Name.Contains("Tooltip") && m.GetParameters().Length == 0);
                    if (getTooltipsMethod != null)
                    {
                        var tooltipResult = getTooltipsMethod.Invoke(cardState, null);
                        if (tooltipResult is System.Collections.IList tooltipList)
                        {
                            foreach (var tooltip in tooltipList)
                            {
                                string tooltipText = ExtractTooltipText(tooltip);
                                if (!string.IsNullOrEmpty(tooltipText) && !tooltips.Contains(tooltipText))
                                    tooltips.Add(tooltipText);
                            }
                        }
                    }
                }

                // Method 2: Get tooltips from CardData's effects
                if (cardData != null)
                {
                    var dataType = cardData.GetType();

                    // Get card effects - each effect can have additionalTooltips
                    var getEffectsMethod = dataType.GetMethod("GetEffects", Type.EmptyTypes);
                    if (getEffectsMethod != null)
                    {
                        var effects = getEffectsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (effects != null)
                        {
                            foreach (var effect in effects)
                            {
                                // Get additionalTooltips from each effect
                                ExtractTooltipsFromEffect(effect, tooltips);

                                // Also get status effect tooltips from paramStatusEffects
                                ExtractStatusEffectTooltips(effect, tooltips);
                            }
                        }
                    }

                    // Also check card traits for tooltips (fallback for CardTraitData)
                    var getTraitsMethod = dataType.GetMethod("GetTraits", Type.EmptyTypes);
                    if (getTraitsMethod != null)
                    {
                        var traits = getTraitsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (traits != null)
                        {
                            foreach (var trait in traits)
                            {
                                ExtractTraitTooltip(trait, tooltips);
                            }
                        }
                    }
                }

                // Method 3: Parse keywords from card description and look up definitions
                // Use the passed description if available, otherwise try to fetch it
                string desc = cardDescription;
                if (string.IsNullOrEmpty(desc))
                {
                    desc = GetCardDescriptionForKeywordParsing(cardState, cardData);
                }
                if (!string.IsNullOrEmpty(desc))
                {
                    ExtractKeywordsFromDescription(desc, tooltips);
                }

                if (tooltips.Count > 0)
                {
                    // If the card description already begins with "Ability:",
                    // drop the generic "Ability" keyword tooltip to avoid repetition.
                    bool descHasAbilityPrefix = !string.IsNullOrEmpty(cardDescription)
                        && cardDescription.TrimStart().StartsWith("Ability:", StringComparison.OrdinalIgnoreCase);

                    var cleaned = tooltips
                        .Distinct()
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Where(t => !(descHasAbilityPrefix && t.StartsWith("Ability:", StringComparison.OrdinalIgnoreCase)))
                        .Select(t => TextUtilities.TrimTrailingPunctuation(TextUtilities.FixSingularGrammar(t)))
                        .Where(t => !string.IsNullOrEmpty(t));
                    return string.Join(". ", cleaned);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting keyword tooltips: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract keywords from card description text and look up their definitions.
        /// Uses KeywordManager for definitions (loaded from game localization + fallbacks).
        /// </summary>
        public static void ExtractKeywordsFromDescription(string description, List<string> tooltips)
        {
            if (string.IsNullOrEmpty(description)) return;

            // First, try to extract keywords from bold tags and look them up dynamically
            ExtractBoldKeywordsWithGameLookup(description, tooltips);

            // Use KeywordManager as the single source of truth for keyword definitions.
            // Normalize curly/typographic apostrophes to ASCII on both sides so keys
            // like "Dragon's Hoard" match the game's "Dragon\u2019s Hoard" text.
            var knownKeywords = Core.KeywordManager.GetKeywords();
            string normalizedDesc = description.Replace('\u2019', '\'').Replace('\u2018', '\'');

            foreach (var keyword in knownKeywords)
            {
                string normalizedKey = keyword.Key.Replace('\u2019', '\'').Replace('\u2018', '\'');
                if (System.Text.RegularExpressions.Regex.IsMatch(normalizedDesc,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(normalizedKey)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (!tooltips.Contains(keyword.Value))
                    {
                        tooltips.Add(keyword.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Extract tooltip from a CardTraitState object using its localized methods
        /// </summary>
        public static string ExtractTraitStateTooltip(object traitState)
        {
            if (traitState == null) return null;

            try
            {
                var traitType = traitState.GetType();

                // Try GetCardTooltipTitle() - returns localized title like "Avance"
                string title = null;
                var getTitleMethod = traitType.GetMethod("GetCardTooltipTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    title = getTitleMethod.Invoke(traitState, null) as string;
                }

                // If no title method, try GetCardText()
                if (string.IsNullOrEmpty(title))
                {
                    var getCardTextMethod = traitType.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        title = getCardTextMethod.Invoke(traitState, null) as string;
                    }
                }

                // Try GetCardTooltipText() - returns localized description like "Se coloca en primera posición"
                string body = null;
                var getBodyMethod = traitType.GetMethod("GetCardTooltipText", Type.EmptyTypes);
                if (getBodyMethod != null)
                {
                    body = getBodyMethod.Invoke(traitState, null) as string;
                }

                // If methods didn't work, try localization key fallback
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
                {
                    string traitTypeName = traitType.Name;

                    // Try to get localized text using the game's key pattern
                    if (string.IsNullOrEmpty(title))
                    {
                        string titleKey = $"{traitTypeName}_CardText";
                        title = LocalizationHelper.Localize(titleKey);
                        if (title == titleKey) title = null; // Localization failed
                    }

                    if (string.IsNullOrEmpty(body))
                    {
                        string bodyKey = $"{traitTypeName}_TooltipText";
                        body = LocalizationHelper.Localize(bodyKey);
                        if (body == bodyKey) body = null; // Localization failed
                    }
                }

                // Clean up text
                title = TextUtilities.StripRichTextTags(title);
                body = TextUtilities.StripRichTextTags(body);

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body))
                {
                    MonsterTrainAccessibility.LogInfo($"Found trait tooltip: {title}: {body}");
                    return $"{title}: {body}";
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    return title;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting trait state tooltip: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract tooltip text from a tooltip data object
        /// </summary>
        public static string ExtractTooltipText(object tooltip)
        {
            if (tooltip == null) return null;

            try
            {
                var tooltipType = tooltip.GetType();
                string title = null;
                string body = null;

                // Try GetTitle/GetBody methods
                var getTitleMethod = tooltipType.GetMethod("GetTitle", Type.EmptyTypes);
                var getBodyMethod = tooltipType.GetMethod("GetBody", Type.EmptyTypes)
                                 ?? tooltipType.GetMethod("GetDescription", Type.EmptyTypes);

                if (getTitleMethod != null)
                    title = getTitleMethod.Invoke(tooltip, null) as string;
                if (getBodyMethod != null)
                    body = getBodyMethod.Invoke(tooltip, null) as string;

                // Try title/body fields
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? tooltipType.GetField("_title", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltip) as string;
                }

                if (string.IsNullOrEmpty(body))
                {
                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 ?? tooltipType.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (bodyField != null)
                        body = bodyField.GetValue(tooltip) as string;
                }

                // Localize if needed
                title = LocalizeIfNeeded(title);
                body = LocalizeIfNeeded(body);

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body))
                {
                    return $"{TextUtilities.StripRichTextTags(title)}: {TextUtilities.StripRichTextTags(body)}";
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    return TextUtilities.StripRichTextTags(title);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract tooltips from a card effect (CardEffectData)
        /// </summary>
        public static void ExtractTooltipsFromEffect(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get additionalTooltips field
                var tooltipsField = effectType.GetField("additionalTooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField != null)
                {
                    var additionalTooltips = tooltipsField.GetValue(effect) as System.Collections.IList;
                    if (additionalTooltips != null)
                    {
                        foreach (var tooltip in additionalTooltips)
                        {
                            string text = ExtractAdditionalTooltipData(tooltip);
                            if (!string.IsNullOrEmpty(text) && !tooltips.Contains(text))
                                tooltips.Add(text);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Extract tooltip from AdditionalTooltipData
        /// </summary>
        public static string ExtractAdditionalTooltipData(object tooltipData)
        {
            if (tooltipData == null) return null;

            try
            {
                var type = tooltipData.GetType();

                // AdditionalTooltipData has titleKey, descriptionKey, or title/description
                string title = null;
                string description = null;

                // Try titleKey/descriptionKey first
                var titleKeyField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var descKeyField = type.GetField("descriptionKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (titleKeyField != null)
                {
                    string key = titleKeyField.GetValue(tooltipData) as string;
                    title = LocalizationHelper.Localize(key);
                }

                if (descKeyField != null)
                {
                    string key = descKeyField.GetValue(tooltipData) as string;
                    description = LocalizationHelper.Localize(key);
                }

                // Also try direct title/description
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = type.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltipData) as string;
                }

                if (string.IsNullOrEmpty(description))
                {
                    var descField = type.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (descField != null)
                        description = descField.GetValue(tooltipData) as string;
                }

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(description))
                {
                    return $"{TextUtilities.StripRichTextTags(title)}: {TextUtilities.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract status effect tooltips from a card effect's paramStatusEffects
        /// </summary>
        public static void ExtractStatusEffectTooltips(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get paramStatusEffects field
                var statusField = effectType.GetField("paramStatusEffects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusField != null)
                {
                    var statusEffects = statusField.GetValue(effect) as System.Collections.IList;
                    if (statusEffects != null)
                    {
                        foreach (var statusEffect in statusEffects)
                        {
                            string tooltip = GetStatusEffectTooltip(statusEffect);
                            if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                                tooltips.Add(tooltip);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get tooltip for a status effect stack/application
        /// </summary>
        public static string GetStatusEffectTooltip(object statusEffectParam)
        {
            if (statusEffectParam == null) return null;

            try
            {
                var type = statusEffectParam.GetType();

                // Get the statusId
                string statusId = null;
                var statusIdField = type.GetField("statusId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusIdField != null)
                {
                    statusId = statusIdField.GetValue(statusEffectParam) as string;
                }

                if (!string.IsNullOrEmpty(statusId))
                {
                    // Look up the status effect data
                    return GetStatusEffectDefinition(statusId);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract trait tooltips from CardTraitData
        /// </summary>
        public static void ExtractTraitTooltip(object trait, List<string> tooltips)
        {
            if (trait == null) return;

            try
            {
                var traitType = trait.GetType();

                // Get trait name
                var getNameMethod = traitType.GetMethod("GetTraitStateName", Type.EmptyTypes)
                                 ?? traitType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string traitName = getNameMethod.Invoke(trait, null) as string;
                    if (!string.IsNullOrEmpty(traitName))
                    {
                        // Look up trait definition
                        string tooltip = GetTraitDefinition(traitName);
                        if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                            tooltips.Add(tooltip);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get tooltip definition for a card trait
        /// </summary>
        public static string GetTraitDefinition(string traitName)
        {
            // Common card traits with their definitions
            var traitDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "permafrost", "Permafrost: Card remains in hand when drawn" },
                { "frozen", "Frozen: Cannot be played until unfrozen" },
                { "consume", "Consume: Removed from deck after playing" },
                { "holdover", "Holdover: Returns to hand at end of turn" },
                { "purge", "Purge: Removed from deck permanently" },
                { "exhaust", "Exhaust: Removed from deck for this battle" },
                { "intrinsic", "Intrinsic: Always drawn on first turn" },
                { "etch", "Etch: Permanently upgrade this card" }
            };

            if (traitDefinitions.TryGetValue(traitName, out string definition))
            {
                return definition;
            }

            // Try localization
            string key = $"CardTrait_{traitName}_Tooltip";
            string localized = LocalizationHelper.Localize(key);
            if (!string.IsNullOrEmpty(localized) && localized != key)
            {
                return $"{FormatTraitName(traitName)}: {TextUtilities.StripRichTextTags(localized)}";
            }

            return null;
        }

        /// <summary>
        /// Format a trait name for display
        /// </summary>
        public static string FormatTraitName(string traitName)
        {
            if (string.IsNullOrEmpty(traitName)) return traitName;

            // Remove "State" suffix and format
            traitName = traitName.Replace("State", "");
            return System.Text.RegularExpressions.Regex.Replace(traitName, "([a-z])([A-Z])", "$1 $2");
        }

        /// <summary>
        /// Get card description for keyword parsing
        /// </summary>
        public static string GetCardDescriptionForKeywordParsing(object cardState, object cardData)
        {
            string desc = null;

            try
            {
                if (cardState != null)
                {
                    var type = cardState.GetType();
                    var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        desc = getCardTextMethod.Invoke(cardState, null) as string;
                    }
                }

                if (string.IsNullOrEmpty(desc) && cardData != null)
                {
                    var dataType = cardData.GetType();
                    var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        desc = getDescMethod.Invoke(cardData, null) as string;
                    }
                }
            }
            catch { }

            return desc;
        }

        /// <summary>
        /// Extract bold keywords from text and look up their descriptions from the game
        /// </summary>
        public static void ExtractBoldKeywordsWithGameLookup(string description, List<string> tooltips)
        {
            if (string.IsNullOrEmpty(description)) return;

            // Extract text inside <b> tags - these are typically keywords
            var boldMatches = System.Text.RegularExpressions.Regex.Matches(description, @"<b>([^<]+)</b>");
            foreach (System.Text.RegularExpressions.Match match in boldMatches)
            {
                string keyword = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(keyword)) continue;

                // Skip numbers and short strings
                if (keyword.Length <= 1 || int.TryParse(keyword, out _)) continue;

                // Try to get description from game
                string keywordDescription = GetKeywordDescriptionFromGame(keyword);
                if (!string.IsNullOrEmpty(keywordDescription))
                {
                    string tooltip = $"{keyword}: {keywordDescription}";
                    if (!tooltips.Any(t => t.StartsWith(keyword + ":", StringComparison.OrdinalIgnoreCase)))
                    {
                        tooltips.Add(tooltip);
                        MonsterTrainAccessibility.LogInfo($"Found keyword from game: {tooltip}");
                    }
                }
            }
        }

        /// <summary>
        /// Get a keyword's description from the game's data
        /// </summary>
        public static string GetKeywordDescriptionFromGame(string keywordName)
        {
            if (string.IsNullOrEmpty(keywordName)) return null;

            // Check cache first
            if (_keywordCache.TryGetValue(keywordName, out string cached))
            {
                return cached;
            }

            try
            {
                // Initialize cache if needed
                if (!_keywordCacheInitialized)
                {
                    InitializeKeywordCache();
                    _keywordCacheInitialized = true;

                    // Check cache again after init
                    if (_keywordCache.TryGetValue(keywordName, out cached))
                    {
                        return cached;
                    }
                }

                // Try standard localization key patterns for keywords/status effects
                string[] keyPatterns = new[]
                {
                    $"StatusEffect_{keywordName}_CardText",
                    $"StatusEffect_{keywordName}_Description",
                    $"Keyword_{keywordName}_Description",
                    $"Tooltip_{keywordName}_Body",
                    $"CardTrait_{keywordName}_CardText",
                    $"CardTrait_{keywordName}_Description",
                    $"Trigger_{keywordName}_Description",
                    $"{keywordName}_Description",
                    $"{keywordName}_Tooltip"
                };

                foreach (var pattern in keyPatterns)
                {
                    string localized = LocalizationHelper.Localize(pattern);
                    if (!string.IsNullOrEmpty(localized) && localized != pattern && !localized.Contains("KEY>"))
                    {
                        _keywordCache[keywordName] = localized;
                        return localized;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error looking up keyword '{keywordName}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Initialize the keyword cache by loading all status effects from the game
        /// </summary>
        public static void InitializeKeywordCache()
        {
            try
            {
                // Try to find StatusEffectManager or similar
                var allManagersType = AccessTools.TypeByName("AllGameManagers");
                if (allManagersType == null) return;

                var instanceProp = allManagersType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null) return;

                var instance = instanceProp.GetValue(null);
                if (instance == null) return;

                // Try to get StatusEffectManager
                var statusManagerProp = allManagersType.GetProperty("StatusEffectManager");
                if (statusManagerProp != null)
                {
                    var statusManager = statusManagerProp.GetValue(instance);
                    if (statusManager != null)
                    {
                        // Try to get all status effects
                        var getDataMethod = statusManager.GetType().GetMethod("GetAllStatusEffectData") ??
                                           statusManager.GetType().GetMethod("GetStatusEffectDatas");
                        if (getDataMethod != null)
                        {
                            var allData = getDataMethod.Invoke(statusManager, null) as System.Collections.IEnumerable;
                            if (allData != null)
                            {
                                foreach (var data in allData)
                                {
                                    if (data == null) continue;
                                    var dataType = data.GetType();

                                    // Get name
                                    var getNameMethod = dataType.GetMethod("GetStatusId") ?? dataType.GetMethod("GetName");
                                    string name = getNameMethod?.Invoke(data, null) as string;
                                    if (string.IsNullOrEmpty(name)) continue;

                                    // Get description
                                    var getDescMethod = dataType.GetMethod("GetDescription") ?? dataType.GetMethod("GetCardText");
                                    string desc = getDescMethod?.Invoke(data, null) as string;
                                    if (!string.IsNullOrEmpty(desc) && !desc.Contains("KEY>"))
                                    {
                                        desc = TextUtilities.StripRichTextTags(desc);
                                        _keywordCache[name] = desc;
                                    }
                                }
                                MonsterTrainAccessibility.LogInfo($"Loaded {_keywordCache.Count} keywords from game");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error initializing keyword cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the definition of a status effect by its ID
        /// </summary>
        public static string GetStatusEffectDefinition(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            try
            {
                // Try to get from StatusEffectManager
                var managerType = ReflectionHelper.GetTypeFromAssemblies("StatusEffectManager");
                if (managerType != null)
                {
                    var instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        var manager = instanceProp.GetValue(null);
                        if (manager != null)
                        {
                            var getAllMethod = managerType.GetMethod("GetAllStatusEffectsData", Type.EmptyTypes);
                            if (getAllMethod != null)
                            {
                                var allData = getAllMethod.Invoke(manager, null);
                                if (allData != null)
                                {
                                    var getDataMethod = allData.GetType().GetMethod("GetStatusEffectData", Type.EmptyTypes);
                                    if (getDataMethod != null)
                                    {
                                        var dataList = getDataMethod.Invoke(allData, null) as System.Collections.IList;
                                        if (dataList != null)
                                        {
                                            foreach (var data in dataList)
                                            {
                                                var dataType = data.GetType();
                                                var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                                                if (getIdMethod != null)
                                                {
                                                    string id = getIdMethod.Invoke(data, null) as string;
                                                    if (id == statusId)
                                                    {
                                                        // Found it - get name and description
                                                        return GetStatusEffectNameAndDescription(data, dataType);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback: try localization directly
                string locKey = GetStatusEffectLocKey(statusId);
                string name = LocalizationHelper.Localize($"{locKey}_CardText");
                string desc = LocalizationHelper.Localize($"{locKey}_CardTooltipText");

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(desc) && name != $"{locKey}_CardText")
                {
                    return $"{TextUtilities.StripRichTextTags(name)}: {TextUtilities.StripRichTextTags(desc)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get name and description from StatusEffectData
        /// </summary>
        public static string GetStatusEffectNameAndDescription(object statusData, Type dataType)
        {
            try
            {
                string name = null;
                string description = null;

                // Try GetDisplayName or similar
                var getNameMethod = dataType.GetMethod("GetDisplayName", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(statusData, null) as string;
                }

                // Try GetDescription
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetTooltipText", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(statusData, null) as string;
                }

                // Fallback to localization
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                {
                    var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                    if (getIdMethod != null)
                    {
                        string statusId = getIdMethod.Invoke(statusData, null) as string;
                        string locKey = GetStatusEffectLocKey(statusId);

                        if (string.IsNullOrEmpty(name))
                            name = LocalizationHelper.Localize($"{locKey}_CardText");
                        if (string.IsNullOrEmpty(description))
                            description = LocalizationHelper.Localize($"{locKey}_CardTooltipText");
                    }
                }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(description))
                {
                    return $"{TextUtilities.StripRichTextTags(name)}: {TextUtilities.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get localization key prefix for a status effect ID
        /// </summary>
        public static string GetStatusEffectLocKey(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            // Standard format: StatusEffect_[StatusId] with first letter capitalized
            if (statusId.Length == 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]);
            else if (statusId.Length > 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]) + statusId.Substring(1);

            return null;
        }

        /// <summary>
        /// Localize a string if it looks like a localization key
        /// </summary>
        public static string LocalizeIfNeeded(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // If it looks like a localization key, try to localize
            if (text.Contains("_") && !text.Contains(" "))
            {
                string localized = LocalizationHelper.Localize(text);
                if (!string.IsNullOrEmpty(localized) && localized != text)
                    return localized;
            }

            return text;
        }
    }
}
