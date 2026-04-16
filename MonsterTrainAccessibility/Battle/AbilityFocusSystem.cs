using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Non-modal ability browser. Ctrl+Left/Right cycles through the Pyre ability
    /// and every friendly unit that has an active ability (champion included).
    /// Ctrl+Enter activates. No separate mode — card focus stays with the EventSystem.
    /// </summary>
    public class AbilityFocusSystem : MonoBehaviour
    {
        public static AbilityFocusSystem Instance { get; private set; }

        /// <summary>
        /// Always false now — kept for compatibility with InputInterceptor guard check removal.
        /// </summary>
        public bool IsActive => false;

        private readonly List<AbilityItem> _items = new List<AbilityItem>();
        private int _index = -1;
        private float _inputCooldown = 0f;
        private const float COOLDOWN = 0.15f;

        /// <summary>
        /// Whether the Ctrl+Left/Right hint has been announced this battle.
        /// Reset on battle entry via <see cref="ResetHint"/>.
        /// </summary>
        private bool _hintAnnounced;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// Call when a new battle starts to reset state.
        /// </summary>
        public void ResetHint()
        {
            _hintAnnounced = false;
            _items.Clear();
            _index = -1;
        }

        /// <summary>
        /// Called when a unit ability becomes available. Announces the hint once per battle.
        /// </summary>
        public void OnAbilityBecameAvailable()
        {
            if (_hintAnnounced) return;
            _hintAnnounced = true;
            MonsterTrainAccessibility.ScreenReader?.Queue(
                "Press O and P to browse abilities, Enter to activate.");
        }

        private void Update()
        {
            if (_inputCooldown > 0) _inputCooldown -= Time.unscaledDeltaTime;
            if (_inputCooldown > 0) return;

            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
            {
                _items.Clear();
                _index = -1;
                return;
            }

            // O — cycle to next ability
            if (Input.GetKeyDown(KeyCode.O))
            {
                BuildItems();
                if (_items.Count == 0)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("No abilities available", false);
                }
                else
                {
                    _index = (_index + 1) % _items.Count;
                    AnnounceCurrent();
                }
                _inputCooldown = COOLDOWN;
            }
            // P — cycle to previous ability
            else if (Input.GetKeyDown(KeyCode.P))
            {
                BuildItems();
                if (_items.Count == 0)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("No abilities available", false);
                }
                else
                {
                    _index = (_index - 1 + _items.Count) % _items.Count;
                    AnnounceCurrent();
                }
                _inputCooldown = COOLDOWN;
            }
            // Any arrow key without O/P — user is navigating cards, clear ability selection
            // so Enter goes back to normal card play.
            else if (_index >= 0 &&
                     (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                      Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                _index = -1;
                _items.Clear();
            }
            // Enter — activate selected ability.
            // Temporarily deselect the current card so the EventSystem's Submit
            // handler doesn't also fire (which would try to play the focused card).
            else if (_index >= 0 && _index < _items.Count &&
                     (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                var es = EventSystem.current;
                var saved = es?.currentSelectedGameObject;
                if (es != null) es.SetSelectedGameObject(null);
                Activate();
                // Restore selection next frame so the player can keep navigating cards.
                if (saved != null) StartCoroutine(RestoreSelection(saved));
                _inputCooldown = COOLDOWN;
            }
        }

        private void AnnounceCurrent()
        {
            if (_index < 0 || _index >= _items.Count) return;
            var item = _items[_index];
            string status = item.IsActivatable() ? "" : ", not ready";

            // Scan the ability description for known keywords and append first-time
            // definitions via KeywordManager (which tracks per-session).
            string keywordSuffix = BuildKeywordSuffix(item.Label);

            MonsterTrainAccessibility.ScreenReader?.Speak(
                $"{item.Label}{status}. {_index + 1} of {_items.Count}{keywordSuffix}", false);
        }

        private string BuildKeywordSuffix(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var dict = Core.KeywordManager.GetKeywords();
            if (dict == null || dict.Count == 0) return "";

            string normalized = text.Replace('\u2019', '\'').Replace('\u2018', '\'');
            var newDefinitions = new List<string>();

            foreach (var kv in dict)
            {
                string normalizedKey = kv.Key.Replace('\u2019', '\'').Replace('\u2018', '\'');
                if (System.Text.RegularExpressions.Regex.IsMatch(normalized,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(normalizedKey)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    // GetKeywordAnnouncement returns full definition on first call,
                    // just the name on subsequent calls.
                    string announcement = Core.KeywordManager.GetKeywordAnnouncement(kv.Key);
                    if (announcement != kv.Key)
                        newDefinitions.Add(announcement);
                }
            }

            if (newDefinitions.Count == 0) return "";
            return " Keywords: " + string.Join(". ", newDefinitions) + ".";
        }

        private void Activate()
        {
            if (_index < 0 || _index >= _items.Count) return;
            var item = _items[_index];
            try
            {
                item.Activate();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"AbilityFocusSystem activate error: {ex}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Error activating ability", false);
            }
        }

        private IEnumerator RestoreSelection(GameObject go)
        {
            yield return null; // wait one frame so EventSystem skips this Submit
            if (go != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(go);
        }

        #region Item collection

        private void BuildItems()
        {
            _items.Clear();

            // 1. Pyre ability always first.
            try
            {
                var cache = BattleManagerCacheRef;
                object pyreHeart = null;
                if (cache?.RoomManager != null)
                {
                    var getPyreRoom = cache.RoomManager.GetType().GetMethod("GetPyreRoom", Type.EmptyTypes);
                    var pyreRoom = getPyreRoom?.Invoke(cache.RoomManager, null);
                    var getPyreHeart = pyreRoom?.GetType().GetMethod("GetPyreHeart", Type.EmptyTypes);
                    pyreHeart = getPyreHeart?.Invoke(pyreRoom, null);
                }
                string pyreName = GetUnitAbilityName(pyreHeart) ?? "unknown";
                string pyreDesc = GetUnitAbilityDescription(pyreHeart);
                string pyreLabel = "Pyre ability: " + pyreName;
                if (!string.IsNullOrEmpty(pyreDesc)) pyreLabel += ". " + pyreDesc;

                _items.Add(new AbilityItem
                {
                    Label = pyreLabel,
                    IsActivatable = () => true,
                    Activate = () => MonsterTrainAccessibility.BattleHandler?.ActivatePyreAbility()
                });
            }
            catch { }

            // 2. Every friendly unit on any floor that has a unit ability (excluding pyre heart — already above).
            try
            {
                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle == null) return;
                for (int floor = 1; floor <= 3; floor++)
                {
                    var units = GetFriendlyUnitsOnFloor(floor);
                    foreach (var unit in units)
                    {
                        if (IsPyreHeart(unit)) continue;
                        if (!HasUnitAbility(unit)) continue;
                        var captured = unit;
                        string unitName = UnitInfoHelper.GetUnitName(unit, BattleManagerCacheRef);
                        string abilityName = GetUnitAbilityName(unit) ?? "ability";
                        string abilityDesc = GetUnitAbilityDescription(unit);
                        string label = $"{unitName} on floor {floor}, {abilityName}";
                        if (!string.IsNullOrEmpty(abilityDesc)) label += ". " + abilityDesc;
                        _items.Add(new AbilityItem
                        {
                            Label = label,
                            IsActivatable = () => CanActivateUnitAbility(captured),
                            Activate = () => ActivateUnitAbility(captured)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"BuildItems error: {ex}");
            }
        }

        private BattleManagerCache BattleManagerCacheRef
        {
            get
            {
                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle == null) return null;
                var f = battle.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance);
                return f?.GetValue(battle) as BattleManagerCache;
            }
        }

        private List<object> GetFriendlyUnitsOnFloor(int userFloor)
        {
            var result = new List<object>();
            var cache = BattleManagerCacheRef;
            if (cache == null) return result;
            var floorReader = MonsterTrainAccessibility.BattleHandler?.GetType()
                .GetField("_floorReader", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(MonsterTrainAccessibility.BattleHandler) as FloorReader;
            if (floorReader == null) return result;

            int roomIndex = userFloor - 1;
            var room = floorReader.GetRoom(roomIndex);
            if (room == null) return result;

            foreach (var unit in UnitInfoHelper.GetUnitsInRoom(room))
            {
                if (!UnitInfoHelper.IsEnemyUnit(unit, cache))
                    result.Add(unit);
            }
            return result;
        }

        private bool HasUnitAbility(object unit)
        {
            try
            {
                var m = unit.GetType().GetMethod("HasUnitAbility", Type.EmptyTypes);
                var r = m?.Invoke(unit, null);
                return r is bool b && b;
            }
            catch { return false; }
        }

        private bool CanActivateUnitAbility(object unit)
        {
            try
            {
                var m = unit.GetType().GetMethod("CanActivateUnitAbility", Type.EmptyTypes);
                var r = m?.Invoke(unit, null);
                return r is bool b && b;
            }
            catch { return false; }
        }

        private bool IsPyreHeart(object unit)
        {
            try
            {
                var m = unit.GetType().GetMethod("IsPyreHeart", Type.EmptyTypes);
                var r = m?.Invoke(unit, null);
                return r is bool b && b;
            }
            catch { return false; }
        }

        private string GetUnitAbilityName(object unit)
        {
            try
            {
                var card = GetUnitAbilityCard(unit);
                if (card == null) return null;
                var getTitle = card.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                var title = getTitle?.Invoke(card, null) as string;
                return string.IsNullOrEmpty(title) ? null : Utilities.TextUtilities.StripRichTextTags(title);
            }
            catch { return null; }
        }

        private object GetUnitAbilityCard(object unit)
        {
            try
            {
                var m = unit.GetType().GetMethod("GetUnitAbilityCardState", Type.EmptyTypes);
                return m?.Invoke(unit, null);
            }
            catch { return null; }
        }

        private string GetUnitAbilityDescription(object unit)
        {
            try
            {
                var card = GetUnitAbilityCard(unit);
                if (card == null) return null;
                var cardType = card.GetType();

                // Try parameterized GetCardText first (fills in dynamic values)
                var methods = cardType.GetMethods();
                foreach (var method in methods)
                {
                    if (method.Name != "GetCardText") continue;
                    var ps = method.GetParameters();
                    if (ps.Length == 0) continue;
                    try
                    {
                        var args = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++)
                        {
                            if (ps[i].ParameterType == typeof(bool))
                                args[i] = true;
                            else if (ps[i].ParameterType.IsValueType)
                                args[i] = Activator.CreateInstance(ps[i].ParameterType);
                            else
                                args[i] = null;
                        }
                        var desc = method.Invoke(card, args) as string;
                        if (!string.IsNullOrEmpty(desc))
                            return Utilities.TextUtilities.StripRichTextTags(
                                Utilities.TextUtilities.CleanSpriteTagsForSpeech(desc));
                    }
                    catch { }
                }

                // Fallback: parameterless GetCardText
                var simple = cardType.GetMethod("GetCardText", Type.EmptyTypes);
                if (simple != null)
                {
                    var desc = simple.Invoke(card, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        return Utilities.TextUtilities.StripRichTextTags(
                            Utilities.TextUtilities.CleanSpriteTagsForSpeech(desc));
                }
            }
            catch { }
            return null;
        }

        private void ActivateUnitAbility(object unit)
        {
            try
            {
                if (!CanActivateUnitAbility(unit))
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Ability not ready", false);
                    return;
                }
                var cache = BattleManagerCacheRef;
                if (cache?.CardManager == null)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("No card manager", false);
                    return;
                }

                // Prefer the coroutine path (focuses the room first) when we have a host.
                var coroutineMethod = unit.GetType().GetMethod("ActivateUnitAbilityCoroutine", new[] { cache.CardManager.GetType() });
                if (coroutineMethod != null)
                {
                    var enumerator = coroutineMethod.Invoke(unit, new[] { cache.CardManager }) as IEnumerator;
                    if (enumerator != null)
                    {
                        StartCoroutine(enumerator);
                        string name = GetUnitAbilityName(unit) ?? "ability";
                        MonsterTrainAccessibility.ScreenReader?.Speak($"Activated {name}", false);
                        return;
                    }
                }

                // Fallback: direct non-coroutine call.
                var directMethod = unit.GetType().GetMethod("ActivateUnitAbility", new[] { cache.CardManager.GetType() });
                if (directMethod != null)
                {
                    var result = directMethod.Invoke(unit, new[] { cache.CardManager });
                    bool ok = result is bool b && b;
                    string name = GetUnitAbilityName(unit) ?? "ability";
                    MonsterTrainAccessibility.ScreenReader?.Speak(ok ? $"Activated {name}" : $"{name} not ready", false);
                    return;
                }

                MonsterTrainAccessibility.ScreenReader?.Speak("Activation method not found", false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ActivateUnitAbility error: {ex}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Error activating", false);
            }
        }

        #endregion

        private class AbilityItem
        {
            public string Label;
            public Func<bool> IsActivatable;
            public Action Activate;
        }
    }
}
