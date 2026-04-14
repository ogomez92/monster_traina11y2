using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Unified localization helper. Caches the game's Localize extension method
    /// and provides localization utilities used across the mod.
    /// </summary>
    public static class LocalizationHelper
    {
        private static MethodInfo _localizeMethod;
        private static bool _localizeMethodSearched;
        private static Type _localizedIntegerType;
        private static ConstructorInfo _localizedIntegerCtor;
        private static bool _localizedIntegerSearched;
        private static MethodInfo _getTermsListMethod;
        private static bool _getTermsListSearched;
        private static System.Collections.Generic.List<string> _cachedTerms;
        private static readonly System.Collections.Generic.Dictionary<string, string> _termSearchCache =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Scan all I2.Loc terms for one containing <paramref name="substring"/> and ending
        /// with one of the preferred suffixes (in order). Returns the term key, or null.
        /// </summary>
        public static string FindTerm(string substring, params string[] suffixPriority)
        {
            if (string.IsNullOrEmpty(substring)) return null;

            string cacheKey = substring + "|" + string.Join(",", suffixPriority ?? Array.Empty<string>());
            if (_termSearchCache.TryGetValue(cacheKey, out string cached))
                return cached;

            var terms = GetAllTerms();
            if (terms == null) return null;

            string fallback = null;
            string best = null;
            int bestRank = int.MaxValue;
            foreach (var term in terms)
            {
                if (term == null) continue;
                if (term.IndexOf(substring, StringComparison.OrdinalIgnoreCase) < 0) continue;
                fallback ??= term;
                if (suffixPriority != null)
                {
                    for (int i = 0; i < suffixPriority.Length && i < bestRank; i++)
                    {
                        if (term.EndsWith(suffixPriority[i], StringComparison.OrdinalIgnoreCase))
                        {
                            best = term;
                            bestRank = i;
                            break;
                        }
                    }
                }
            }

            string result = best ?? fallback;
            _termSearchCache[cacheKey] = result;
            return result;
        }

        /// <summary>
        /// Look up a sprite name (TMP sprite asset name like "DragonsHoard") and return
        /// its localized display name by searching I2.Loc terms. Returns null if no
        /// suitable term is found — caller should fall back to a heuristic.
        /// </summary>
        public static string GetSpriteDisplayName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;

            // Status effect IDs lower-case match well via StatusEffectManager,
            // but generic sprite icons (currency, resources) we look up by term.
            string termKey = FindTerm(spriteName, "_Name", "_Title", "_Header", "_TooltipTitle", "_CardText");
            if (string.IsNullOrEmpty(termKey)) return null;

            return Localize(termKey);
        }

        private static System.Collections.Generic.List<string> GetAllTerms()
        {
            if (_cachedTerms != null) return _cachedTerms;
            EnsureGetTermsListCached();
            if (_getTermsListMethod == null) return null;

            try
            {
                var parameters = _getTermsListMethod.GetParameters();
                var args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                _cachedTerms = _getTermsListMethod.Invoke(null, args) as System.Collections.Generic.List<string>;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetAllTerms error: {ex.Message}");
            }
            return _cachedTerms;
        }

        private static void EnsureGetTermsListCached()
        {
            if (_getTermsListSearched) return;
            _getTermsListSearched = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = assembly.GetName().Name;
                if (!asmName.Contains("Assembly-CSharp") && !asmName.Contains("I2")) continue;
                try
                {
                    var lmType = assembly.GetType("I2.Loc.LocalizationManager");
                    if (lmType == null) continue;
                    var method = lmType.GetMethod("GetTermsList", BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        _getTermsListMethod = method;
                        return;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Localize a key, supplying an integer parameter so that the game's
        /// {codeint0} placeholder gets substituted. Used for status effect
        /// tooltips like Pyregel where the magnitude lives in the data.
        /// Falls back to plain Localize if the LocalizedInteger type is unavailable.
        /// </summary>
        public static string LocalizeWithInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                EnsureLocalizeMethodCached();
                EnsureLocalizedIntegerCached();

                if (_localizeMethod == null) return null;

                if (_localizedIntegerCtor != null)
                {
                    var liInstance = _localizedIntegerCtor.Invoke(new object[] { value });
                    var parameters = _localizeMethod.GetParameters();
                    var args = new object[parameters.Length];
                    args[0] = key;
                    if (parameters.Length >= 2)
                    {
                        args[1] = liInstance;
                        for (int i = 2; i < parameters.Length; i++)
                            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    }

                    var result = _localizeMethod.Invoke(null, args) as string;
                    if (!string.IsNullOrEmpty(result))
                    {
                        if (result.StartsWith("KEY>>") && result.EndsWith("<<")) return null;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"LocalizeWithInt error for key '{key}': {ex.Message}");
            }

            return Localize(key);
        }

        private static void EnsureLocalizedIntegerCached()
        {
            if (_localizedIntegerSearched) return;
            _localizedIntegerSearched = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = assembly.GetName().Name;
                if (!asmName.Contains("Assembly-CSharp")) continue;
                try
                {
                    _localizedIntegerType = assembly.GetType("LocalizedInteger");
                    if (_localizedIntegerType != null)
                    {
                        _localizedIntegerCtor = _localizedIntegerType.GetConstructor(new[] { typeof(int) });
                        return;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Localize a key using the game's localization system.
        /// Returns the localized string, or null if localization fails.
        /// </summary>
        public static string Localize(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                EnsureLocalizeMethodCached();

                if (_localizeMethod != null)
                {
                    var parameters = _localizeMethod.GetParameters();
                    var args = new object[parameters.Length];
                    args[0] = key;
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    }

                    var result = _localizeMethod.Invoke(null, args) as string;
                    if (!string.IsNullOrEmpty(result))
                    {
                        // Game returns "KEY>>Some_Key<<" as a sentinel for missing entries.
                        if (result.StartsWith("KEY>>") && result.EndsWith("<<"))
                            return null;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Localize error for key '{key}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Try to localize a string. If it looks like a localization key, attempt localization.
        /// Returns the original text if it doesn't appear to be a key or localization fails.
        /// </summary>
        public static string TryLocalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            // Check if it looks like a localization key
            if (!text.Contains("_") && text != text.ToUpperInvariant())
                return text;

            var localized = Localize(text);
            if (!string.IsNullOrEmpty(localized) && localized != text)
                return localized;

            return text;
        }

        /// <summary>
        /// Resolve placeholders like {[effect0.status0.power]} in localized text.
        /// </summary>
        public static string ResolveEffectPlaceholders(string text, object relicData, Type relicType)
        {
            if (string.IsNullOrEmpty(text) || relicData == null) return text;

            try
            {
                var getEffectsMethod = relicType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null)
                {
                    var baseType = relicType.BaseType;
                    while (baseType != null && getEffectsMethod == null)
                    {
                        getEffectsMethod = baseType.GetMethod("GetEffects", Type.EmptyTypes);
                        baseType = baseType.BaseType;
                    }
                }

                if (getEffectsMethod != null)
                {
                    var effects = getEffectsMethod.Invoke(relicData, null) as IList;
                    if (effects != null && effects.Count > 0)
                    {
                        var regex = new Regex(@"\{\[effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");
                        text = regex.Replace(text, match =>
                        {
                            int effectIndex = int.Parse(match.Groups[1].Value);
                            string property = match.Groups[3].Value;
                            int statusIndex = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : -1;

                            if (effectIndex < effects.Count)
                            {
                                var effect = effects[effectIndex];
                                if (effect != null)
                                {
                                    var effectType = effect.GetType();

                                    if (statusIndex >= 0 && property.ToLower() == "power")
                                    {
                                        var statusEffectsField = effectType.GetField("paramStatusEffects",
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (statusEffectsField != null)
                                        {
                                            var statusEffects = statusEffectsField.GetValue(effect) as Array;
                                            if (statusEffects != null && statusIndex < statusEffects.Length)
                                            {
                                                var statusEffect = statusEffects.GetValue(statusIndex);
                                                if (statusEffect != null)
                                                {
                                                    var countField = statusEffect.GetType().GetField("count",
                                                        BindingFlags.Public | BindingFlags.Instance);
                                                    if (countField != null)
                                                    {
                                                        var count = countField.GetValue(statusEffect);
                                                        return count?.ToString() ?? match.Value;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var propField = effectType.GetField("param" + char.ToUpper(property[0]) + property.Substring(1),
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (propField != null)
                                        {
                                            var value = propField.GetValue(effect);
                                            return value?.ToString() ?? match.Value;
                                        }
                                    }
                                }
                            }
                            return match.Value;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveEffectPlaceholders error: {ex.Message}");
            }

            return text;
        }

        private static void EnsureLocalizeMethodCached()
        {
            if (_localizeMethodSearched) return;
            _localizeMethodSearched = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name;
                if (!assemblyName.Contains("Assembly-CSharp") && !assemblyName.Contains("Trainworks"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
                            continue;

                        var method = type.GetMethod("Localize", BindingFlags.Public | BindingFlags.Static);
                        if (method != null && method.ReturnType == typeof(string))
                        {
                            var parameters = method.GetParameters();
                            if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                            {
                                _localizeMethod = method;
                                MonsterTrainAccessibility.LogInfo($"Found Localize method in {type.FullName}");
                                return;
                            }
                        }
                    }
                }
                catch { }

                if (_localizeMethod != null) break;
            }
        }
    }
}
