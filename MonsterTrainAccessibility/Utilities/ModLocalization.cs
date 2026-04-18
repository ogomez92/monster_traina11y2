using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Provides localized strings for mod announcements by using the game's own
    /// localization system where possible, falling back to English.
    /// All public methods return localized text in whatever language the game is running.
    /// </summary>
    public static class ModLocalization
    {
        // Cache resolved strings to avoid repeated reflection
        private static readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Clear caches (call if game language changes at runtime).</summary>
        public static void Reset() => _cache.Clear();

        // ──────────────────── Game-concept terms ────────────────────

        /// <summary>Localized "ember" / energy resource name.</summary>
        public static string Ember => ModTerm("Ember");

        /// <summary>Localized "gold" currency name.</summary>
        public static string Gold => ModTerm("Gold");

        /// <summary>Localized "Pyre" name.</summary>
        public static string Pyre => Cached("pyre",
            () => StripAndTrim(LocalizationHelper.Localize("Character_Pyre"))
                  ?? StripAndTrim(LocalizationHelper.Localize("HudTooltip_PyreHeart_Title"))
                  ?? "Pyre");

        /// <summary>Localized pyre attack (e.g. "45 attack" or "10 attack x2").</summary>
        public static string PyreAttack(int attack, int numAttacks)
        {
            string attackWord = ModTerm("Attack");
            if (numAttacks > 1)
                return $"{attack} {attackWord} x{numAttacks}";
            return $"{attack} {attackWord}";
        }

        // ──────────────────── Combat events ────────────────────

        /// <summary>Localized attack buff/debuff: "{unit} gains/loses {n} attack".</summary>
        public static string AttackBuffed(string unitName, int amount)
        {
            if (amount < 0) return Phrase("LosesAttack", unitName, -amount);
            return Phrase("GainsAttack", unitName, amount);
        }

        /// <summary>Localized max HP buff: "{unit} gains {n} max health".</summary>
        public static string MaxHPBuffed(string unitName, int amount)
        {
            if (amount < 0) return Phrase("LosesMaxHealth", unitName, -amount);
            return Phrase("GainsMaxHealth", unitName, amount);
        }

        /// <summary>Localized max HP debuff: "{unit} loses {n} max health".</summary>
        public static string MaxHPDebuffed(string unitName, int amount)
        {
            return Phrase("LosesMaxHealth", unitName, Math.Abs(amount));
        }

        /// <summary>Localized moon phase name (New Moon / Full Moon).</summary>
        public static string MoonPhase(int phase)
        {
            // Game uses "HudTooltip_MoonPhase_{enumName}".Localize()
            // Enum: New=1, Full=2, None=4
            string enumName = phase == 1 ? "New" : phase == 2 ? "Full" : null;
            if (enumName == null) return null;

            string localized = LocalizationHelper.Localize($"HudTooltip_MoonPhase_{enumName}");
            if (!string.IsNullOrEmpty(localized))
                return TextUtilities.StripRichTextTags(localized).Trim();

            return phase == 1 ? "New Moon" : "Full Moon";
        }

        /// <summary>Localized status effect name via the game's StatusEffectManager.</summary>
        public static string StatusEffectName(string statusId, int stacks = 1)
        {
            return Patches.CharacterStateHelper.CleanStatusName(statusId, stacks);
        }

        /// <summary>Localized wave announcement.</summary>
        public static string WaveStarted(int waveNumber)
        {
            string lastWave = LocalizationHelper.Localize("Message_LastWave");
            // We can't know if it's the last wave here, so just use the wave format
            string waveFmt = LocalizationHelper.Localize("Hud_WavesRemaining");
            if (!string.IsNullOrEmpty(waveFmt))
            {
                try { return string.Format(waveFmt, waveNumber); }
                catch { }
            }
            return waveNumber > 0 ? $"Wave {waveNumber}" : "New wave";
        }

        /// <summary>Localized "Deck shuffled".</summary>
        public static string DeckShuffled()
        {
            string deckName = LocalizationHelper.Localize("HudTooltip_Deck_Title");
            if (!string.IsNullOrEmpty(deckName))
                return TextUtilities.StripRichTextTags(deckName).Trim();
            return "Deck shuffled";
        }

        /// <summary>Localized hand pile name.</summary>
        public static string HandPileName =>
            Cached("handpile", () =>
                StripAndTrim(LocalizationHelper.Localize("Hud_HandPileLavel")) ?? "Hand");

        /// <summary>Localized draw pile name.</summary>
        public static string DrawPileName =>
            Cached("drawpile", () =>
                StripAndTrim(LocalizationHelper.Localize("HudTooltip_Deck_Title")) ?? "Deck");

        /// <summary>Localized discard pile name.</summary>
        public static string DiscardPileName =>
            Cached("discardpile", () =>
                StripAndTrim(LocalizationHelper.Localize("HudTooltip_DiscardPile_Title")) ?? "Discard pile");

        /// <summary>Localized exhaust pile name.</summary>
        public static string ExhaustPileName =>
            Cached("exhaustpile", () =>
                StripAndTrim(LocalizationHelper.Localize("HudTooltip_ExhaustedPile_Title")) ?? "Consumed pile");

        /// <summary>Localized eaten pile name.</summary>
        public static string EatenPileName =>
            Cached("eatenpile", () =>
                StripAndTrim(LocalizationHelper.Localize("HudTooltip_EatenPile_Title")) ?? "Eaten pile");

        // ──────────────────── Floor names ────────────────────

        /// <summary>Localized floor display name.</summary>
        public static string FloorName(int userFloor)
        {
            // Try game's own room/floor localization
            string key = null;
            switch (userFloor)
            {
                case 3: key = "HudTooltip_Floor_Top"; break;
                case 2: key = "HudTooltip_Floor_Middle"; break;
                case 1: key = "HudTooltip_Floor_Bottom"; break;
                case 0: return Pyre;
            }

            if (key != null)
            {
                string loc = LocalizationHelper.Localize(key);
                if (!string.IsNullOrEmpty(loc))
                    return StripAndTrim(loc);
            }

            // Fallback English
            switch (userFloor)
            {
                case 3: return "Top floor";
                case 2: return "Middle floor";
                case 1: return "Bottom floor";
                default: return $"Floor {userFloor}";
            }
        }

        // ──────────────────── Mod-owned translations ────────────────────
        // Terms the game doesn't provide. Keyed by language name as returned by
        // I2.Loc.LocalizationManager.CurrentLanguage (e.g. "English", "Spanish").

        private static readonly Dictionary<string, Dictionary<string, string>> _modTranslations =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["English"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "Upgraded",
                    ["Attack"] = "attack",
                    ["Gold"] = "gold",
                    ["Ember"] = "ember",
                    // Combat-event phrases. Placeholders: see PhraseKeys below.
                    ["Healed"] = "{0} healed {1} health",
                    ["HealedBy"] = "{0} healed {1} health from {2}",
                    ["GainsAttack"] = "{0} gains {1} attack",
                    ["LosesAttack"] = "{0} loses {1} attack",
                    ["GainsMaxHealth"] = "{0} gains {1} max health",
                    ["LosesMaxHealth"] = "{0} loses {1} max health",
                    ["GainsStacks"] = "{0} gains {1} {2}",
                    ["Equipped"] = "{0} equipped with {1}",
                    ["Unequipped"] = "{0} unequipped {1}",
                    ["GainsTrigger"] = "{0} gains {1}",
                    ["LosesTrigger"] = "{0} loses {1}",
                    ["PyreHealed"] = "Pyre healed {0} health, {1} remaining",
                },
                ["Spanish"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "Mejorada",
                    ["Attack"] = "ataque",
                    ["Gold"] = "oro",
                    ["Ember"] = "ascua",
                    ["Healed"] = "{0} recupera {1} de vida",
                    ["HealedBy"] = "{0} recupera {1} de vida con {2}",
                    ["GainsAttack"] = "{0} gana {1} de ataque",
                    ["LosesAttack"] = "{0} pierde {1} de ataque",
                    ["GainsMaxHealth"] = "{0} gana {1} de vida máxima",
                    ["LosesMaxHealth"] = "{0} pierde {1} de vida máxima",
                    ["GainsStacks"] = "{0} gana {1} de {2}",
                    ["Equipped"] = "{0} equipado con {1}",
                    ["Unequipped"] = "{0} desequipó {1}",
                    ["GainsTrigger"] = "{0} gana {1}",
                    ["LosesTrigger"] = "{0} pierde {1}",
                    ["PyreHealed"] = "La pira recupera {0} de vida, quedan {1}",
                },
                ["French"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "Améliorée",
                    ["Attack"] = "attaque",
                    ["Gold"] = "or",
                    ["Ember"] = "braise",
                    ["Healed"] = "{0} récupère {1} PV",
                    ["HealedBy"] = "{0} récupère {1} PV grâce à {2}",
                    ["GainsAttack"] = "{0} gagne {1} en attaque",
                    ["LosesAttack"] = "{0} perd {1} en attaque",
                    ["GainsMaxHealth"] = "{0} gagne {1} PV max",
                    ["LosesMaxHealth"] = "{0} perd {1} PV max",
                    ["GainsStacks"] = "{0} gagne {1} {2}",
                    ["Equipped"] = "{0} équipé de {1}",
                    ["Unequipped"] = "{0} déséquipe {1}",
                    ["GainsTrigger"] = "{0} gagne {1}",
                    ["LosesTrigger"] = "{0} perd {1}",
                    ["PyreHealed"] = "Le Bûcher récupère {0} PV, il en reste {1}",
                },
                ["German"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "Verbessert",
                    ["Attack"] = "Angriff",
                    ["Gold"] = "Gold",
                    ["Ember"] = "Glut",
                    ["Healed"] = "{0} erhält {1} Lebenspunkte",
                    ["HealedBy"] = "{0} erhält {1} Lebenspunkte durch {2}",
                    ["GainsAttack"] = "{0} erhält {1} Angriff",
                    ["LosesAttack"] = "{0} verliert {1} Angriff",
                    ["GainsMaxHealth"] = "{0} erhält {1} maximale Lebenspunkte",
                    ["LosesMaxHealth"] = "{0} verliert {1} maximale Lebenspunkte",
                    ["GainsStacks"] = "{0} erhält {1} {2}",
                    ["Equipped"] = "{0} ausgerüstet mit {1}",
                    ["Unequipped"] = "{0} legt {1} ab",
                    ["GainsTrigger"] = "{0} erhält {1}",
                    ["LosesTrigger"] = "{0} verliert {1}",
                    ["PyreHealed"] = "Scheiterhaufen heilt um {0}, verbleibend {1}",
                },
                ["Portuguese (Brazil)"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "Aprimorada",
                    ["Attack"] = "ataque",
                    ["Gold"] = "ouro",
                    ["Ember"] = "brasa",
                    ["Healed"] = "{0} recupera {1} de vida",
                    ["HealedBy"] = "{0} recupera {1} de vida com {2}",
                    ["GainsAttack"] = "{0} ganha {1} de ataque",
                    ["LosesAttack"] = "{0} perde {1} de ataque",
                    ["GainsMaxHealth"] = "{0} ganha {1} de vida máxima",
                    ["LosesMaxHealth"] = "{0} perde {1} de vida máxima",
                    ["GainsStacks"] = "{0} ganha {1} de {2}",
                    ["Equipped"] = "{0} equipado com {1}",
                    ["Unequipped"] = "{0} desequipou {1}",
                    ["GainsTrigger"] = "{0} ganha {1}",
                    ["LosesTrigger"] = "{0} perde {1}",
                    ["PyreHealed"] = "A pira recupera {0} de vida, restam {1}",
                },
                ["Russian"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "Улучшена",
                    ["Attack"] = "атака",
                    ["Gold"] = "золото",
                    ["Ember"] = "уголёк",
                    ["Healed"] = "{0} восстанавливает {1} здоровья",
                    ["HealedBy"] = "{0} восстанавливает {1} здоровья от {2}",
                    ["GainsAttack"] = "{0} получает {1} атаки",
                    ["LosesAttack"] = "{0} теряет {1} атаки",
                    ["GainsMaxHealth"] = "{0} получает {1} макс. здоровья",
                    ["LosesMaxHealth"] = "{0} теряет {1} макс. здоровья",
                    ["GainsStacks"] = "{0} получает {1} {2}",
                    ["Equipped"] = "{0} экипирован(а) предметом {1}",
                    ["Unequipped"] = "{0} снял(а) {1}",
                    ["GainsTrigger"] = "{0} получает {1}",
                    ["LosesTrigger"] = "{0} теряет {1}",
                    ["PyreHealed"] = "Костёр восстанавливает {0} здоровья, осталось {1}",
                },
                ["Simplified Chinese"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "已升级",
                    ["Attack"] = "攻击",
                    ["Gold"] = "金币",
                    ["Ember"] = "余烬",
                    ["Healed"] = "{0}恢复{1}点生命",
                    ["HealedBy"] = "{0}因{2}恢复{1}点生命",
                    ["GainsAttack"] = "{0}获得{1}点攻击",
                    ["LosesAttack"] = "{0}失去{1}点攻击",
                    ["GainsMaxHealth"] = "{0}获得{1}点最大生命",
                    ["LosesMaxHealth"] = "{0}失去{1}点最大生命",
                    ["GainsStacks"] = "{0}获得{1}层{2}",
                    ["Equipped"] = "{0}装备了{1}",
                    ["Unequipped"] = "{0}卸下{1}",
                    ["GainsTrigger"] = "{0}获得{1}",
                    ["LosesTrigger"] = "{0}失去{1}",
                    ["PyreHealed"] = "火葬堆恢复{0}点生命，剩余{1}",
                },
                ["Japanese"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "強化済み",
                    ["Attack"] = "攻撃",
                    ["Gold"] = "ゴールド",
                    ["Ember"] = "エンバー",
                    ["Healed"] = "{0}は{1}回復した",
                    ["HealedBy"] = "{0}は{2}で{1}回復した",
                    ["GainsAttack"] = "{0}は攻撃力{1}を獲得",
                    ["LosesAttack"] = "{0}は攻撃力{1}を失う",
                    ["GainsMaxHealth"] = "{0}は最大HP{1}を獲得",
                    ["LosesMaxHealth"] = "{0}は最大HP{1}を失う",
                    ["GainsStacks"] = "{0}は{2}を{1}獲得",
                    ["Equipped"] = "{0}は{1}を装備",
                    ["Unequipped"] = "{0}は{1}を外した",
                    ["GainsTrigger"] = "{0}は{1}を獲得",
                    ["LosesTrigger"] = "{0}は{1}を失う",
                    ["PyreHealed"] = "パイアは{0}回復、残り{1}",
                },
                ["Korean"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Upgraded"] = "강화됨",
                    ["Attack"] = "공격",
                    ["Gold"] = "골드",
                    ["Ember"] = "엠버",
                    ["Healed"] = "{0}이(가) 체력 {1} 회복",
                    ["HealedBy"] = "{0}이(가) {2}로 체력 {1} 회복",
                    ["GainsAttack"] = "{0}이(가) 공격력 {1} 획득",
                    ["LosesAttack"] = "{0}이(가) 공격력 {1} 상실",
                    ["GainsMaxHealth"] = "{0}이(가) 최대 체력 {1} 획득",
                    ["LosesMaxHealth"] = "{0}이(가) 최대 체력 {1} 상실",
                    ["GainsStacks"] = "{0}이(가) {2} {1} 획득",
                    ["Equipped"] = "{0}이(가) {1} 장착",
                    ["Unequipped"] = "{0}이(가) {1} 해제",
                    ["GainsTrigger"] = "{0}이(가) {1} 획득",
                    ["LosesTrigger"] = "{0}이(가) {1} 상실",
                    ["PyreHealed"] = "파이어가 체력 {0} 회복, 남은 {1}",
                },
            };

        /// <summary>
        /// Format a mod-owned phrase template (e.g. "Healed", "Equipped") in the
        /// current game language with the given args. Falls back to English template
        /// if the current language lacks the key, and to "{key} {arg0} {arg1}..."
        /// if neither language has it.
        /// </summary>
        public static string Phrase(string key, params object[] args)
        {
            string template = ModTerm(key);
            if (string.IsNullOrEmpty(template) || template == key)
                return args != null && args.Length > 0 ? $"{key} {string.Join(" ", args)}" : key;
            try
            {
                return string.Format(template, args ?? Array.Empty<object>());
            }
            catch
            {
                return args != null && args.Length > 0 ? $"{key} {string.Join(" ", args)}" : template;
            }
        }

        /// <summary>
        /// Get a mod-owned translated term for the current game language.
        /// Falls back to English if the language or key isn't found.
        /// </summary>
        public static string ModTerm(string key)
        {
            string lang = GetCurrentLanguage();

            if (!string.IsNullOrEmpty(lang) &&
                _modTranslations.TryGetValue(lang, out var langDict) &&
                langDict.TryGetValue(key, out var value))
                return value;

            // Fallback to English
            if (_modTranslations.TryGetValue("English", out var enDict) &&
                enDict.TryGetValue(key, out var enValue))
                return enValue;

            return key;
        }

        /// <summary>
        /// Get the current game language name via I2.Loc.LocalizationManager.CurrentLanguage.
        /// </summary>
        private static string GetCurrentLanguage()
        {
            try
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var lmType = asm.GetType("I2.Loc.LocalizationManager");
                    if (lmType == null) continue;

                    var prop = lmType.GetProperty("CurrentLanguage",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null)
                        return prop.GetValue(null) as string;
                }
            }
            catch { }
            return "English";
        }

        // ──────────────────── Helpers ────────────────────

        private static string Cached(string key, Func<string> factory)
        {
            if (_cache.TryGetValue(key, out string val))
                return val;
            val = factory();
            _cache[key] = val;
            return val;
        }

        private static string TryFormat(string locKey, params object[] args)
        {
            string fmt = LocalizationHelper.Localize(locKey);
            if (string.IsNullOrEmpty(fmt)) return null;
            try { return string.Format(fmt, args); }
            catch { return null; }
        }

        private static string StripAndTrim(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = TextUtilities.StripRichTextTags(text).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
