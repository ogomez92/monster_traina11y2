using HarmonyLib;
using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MonsterTrainAccessibility.Screens.Readers;
using MonsterTrainAccessibility.Utilities;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for menus by reading Unity's EventSystem selection.
    /// Instead of maintaining a fake menu, we track what the game has selected
    /// and read the text from the actual UI elements.
    /// </summary>
    public class MenuAccessibility : MonoBehaviour
    {
        private GameObject _lastSelectedObject;
        private float _pollInterval = 0.1f;
        private float _pollTimer = 0f;
        private bool _isActive = true;

        // Text content monitoring
        private string _lastScrollContent = null;
        private string _lastScreenTextHash = null;
        private float _textCheckInterval = 0.5f;
        private float _textCheckTimer = 0f;

        // Upgrade selection screen tracking
        private bool _upgradeScreenHelperAnnounced = false;
        private string _lastUpgradeScreenCheck = null;

        // Dialog tracking - to avoid re-announcing dialog text when navigating between buttons
        private string _lastAnnouncedDialogText = null;

        // Blacklist of panel names that should be ignored when scanning for text
        private static readonly HashSet<string> _panelBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "quitconfirmation", "exitdialog", "confirmdialog", "confirmationpopup",
            "quitpanel", "exitpanel", "confirmquit", "quitgame", "exitgame"
        };

        private void Awake()
        {
            MonsterTrainAccessibility.LogInfo("MenuAccessibility.Awake() called");
        }

        private void Start()
        {
            MonsterTrainAccessibility.LogInfo("MenuAccessibility.Start() called - component is active");
        }

        private void OnDestroy()
        {
            MonsterTrainAccessibility.LogInfo("MenuAccessibility.OnDestroy() called - COMPONENT BEING DESTROYED");
        }

        private void Update()
        {
            if (!_isActive)
                return;

            _pollTimer += Time.unscaledDeltaTime;
            _textCheckTimer += Time.unscaledDeltaTime;

            // Check selection changes frequently
            if (_pollTimer >= _pollInterval)
            {
                _pollTimer = 0f;
                CheckForSelectionChange();
            }

            // Check for text content changes less frequently
            if (_textCheckTimer >= _textCheckInterval)
            {
                _textCheckTimer = 0f;
                CheckForTextChanges();
            }
        }

        /// <summary>
        /// Check if any monitored text content has changed
        /// </summary>
        private void CheckForTextChanges()
        {
            try
            {
                // If we're focused on a scrollbar or scroll area, monitor its content
                if (_lastSelectedObject != null && IsScrollbar(_lastSelectedObject))
                {
                    string currentContent = GetScrollViewContentText(_lastSelectedObject);
                    if (!string.IsNullOrEmpty(currentContent) && currentContent != _lastScrollContent)
                    {
                        _lastScrollContent = currentContent;

                        // Announce the new content (truncated for initial announcement)
                        string announcement = currentContent;
                        if (announcement.Length > 500)
                        {
                            announcement = announcement.Substring(0, 500) + "...";
                        }
                        MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                    }
                }

                // Also check for any new large text panels appearing
                CheckForNewTextPanels();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking text changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if new text panels have appeared (dialogs, popups, etc.)
        /// </summary>
        private void CheckForNewTextPanels()
        {
            try
            {
                // First, check for tutorial panels specifically (highest priority)
                CheckForTutorialText();

                // Check for upgrade selection screen
                CheckForUpgradeSelectionScreen();

                // Get a hash of current visible text to detect changes
                var sb = new StringBuilder();
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (var root in rootObjects)
                {
                    if (root.activeInHierarchy)
                    {
                        // Look for popup/dialog/panel objects that might contain text
                        FindLargeTextContent(root.transform, sb);
                    }
                }

                string currentHash = sb.ToString();

                // If content changed significantly, announce it
                if (!string.IsNullOrEmpty(currentHash) && currentHash != _lastScreenTextHash)
                {
                    // Only announce if there's substantial new content
                    if (_lastScreenTextHash == null ||
                        currentHash.Length > _lastScreenTextHash.Length + 50)
                    {
                        _lastScreenTextHash = currentHash;

                        // Find what's new and announce it
                        string newContent = currentHash;
                        if (_lastScreenTextHash != null && currentHash.StartsWith(_lastScreenTextHash))
                        {
                            newContent = currentHash.Substring(_lastScreenTextHash.Length);
                        }

                        if (newContent.Length > 50) // Only announce substantial text
                        {
                            if (newContent.Length > 500)
                            {
                                newContent = newContent.Substring(0, 500) + "...";
                            }
                            MonsterTrainAccessibility.ScreenReader?.Queue(newContent.Trim());
                        }
                    }
                    _lastScreenTextHash = currentHash;
                }
            }
            catch { }
        }

        /// <summary>
        /// Check for and announce tutorial text when it appears
        /// </summary>
        private void CheckForTutorialText()
        {
            try
            {
                string newTutorialText = Help.Contexts.TutorialHelp.CheckForNewTutorialText();
                if (!string.IsNullOrEmpty(newTutorialText))
                {
                    // Announce tutorial text with "Tutorial:" prefix
                    string announcement = "Tutorial: " + newTutorialText;
                    if (announcement.Length > 600)
                    {
                        announcement = announcement.Substring(0, 600) + "...";
                    }
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                    MonsterTrainAccessibility.LogInfo("Tutorial text announced");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking tutorial text: {ex.Message}");
            }
        }

        /// <summary>
        /// Check for upgrade selection screen (when player needs to select a card to upgrade)
        /// </summary>
        private void CheckForUpgradeSelectionScreen()
        {
            try
            {
                // Never fire during battle. Card descriptions in hand contain words like
                // "apply" and "upgrade" that trip the heuristic, and there's no upgrade
                // selection screen during combat anyway.
                if (MonsterTrainAccessibility.BattleHandler?.IsInBattle == true)
                {
                    if (_lastUpgradeScreenCheck != null)
                    {
                        _lastUpgradeScreenCheck = null;
                        _upgradeScreenHelperAnnounced = false;
                    }
                    return;
                }

                // Look for upgrade selection screen indicators
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (var root in rootObjects)
                {
                    if (!root.activeInHierarchy) continue;

                    // Look for screens that indicate card upgrade selection
                    string screenId = FindUpgradeSelectionScreen(root.transform);
                    if (!string.IsNullOrEmpty(screenId))
                    {
                        // Check if this is a new screen (not the same we already announced)
                        if (screenId != _lastUpgradeScreenCheck)
                        {
                            _lastUpgradeScreenCheck = screenId;
                            _upgradeScreenHelperAnnounced = false;
                        }

                        // Announce helper if not already done
                        if (!_upgradeScreenHelperAnnounced)
                        {
                            _upgradeScreenHelperAnnounced = true;
                            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Select Card to Upgrade");
                            MonsterTrainAccessibility.ScreenReader?.Queue("Use arrow keys to browse cards and press Enter to apply the upgrade.");
                            MonsterTrainAccessibility.LogInfo("Upgrade selection screen detected");
                        }
                        return;
                    }
                }

                // If no upgrade screen found, reset the tracking
                if (_lastUpgradeScreenCheck != null)
                {
                    _lastUpgradeScreenCheck = null;
                    _upgradeScreenHelperAnnounced = false;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking upgrade selection screen: {ex.Message}");
            }
        }

        /// <summary>
        /// Find upgrade selection screen by looking for characteristic UI elements
        /// </summary>
        private string FindUpgradeSelectionScreen(Transform root)
        {
            // Look for GameObjects with names indicating upgrade selection
            var upgradeIndicators = new[] {
                "UpgradeSelectionScreen", "EnhancerSelectionScreen", "CardUpgradeScreen",
                "UpgradeScreen", "CardSelection", "SelectCardScreen",
                "UpgradeCardSelection", "EnhancerCardList", "CardListScreen",
                "UpgradeCards", "CardPicker", "CardChoiceScreen"
            };

            foreach (var indicator in upgradeIndicators)
            {
                var found = UITextHelper.FindChildRecursive(root, indicator);
                if (found != null && found.gameObject.activeInHierarchy)
                {
                    MonsterTrainAccessibility.LogInfo($"Found upgrade indicator: {indicator}");
                    return indicator + "_" + found.GetInstanceID();
                }
            }

            // Check all components to find CardUI elements and upgrade-related text
            var allComponents = root.GetComponentsInChildren<Component>(false);

            // Count active CardUI elements
            var cardUIs = allComponents
                .Where(c => c.GetType().Name == "CardUI" && c.gameObject.activeInHierarchy)
                .ToList();

            // If we have multiple cards visible and we're not in battle, this might be upgrade selection
            if (cardUIs.Count >= 3)
            {
                // Check we're not in battle (don't have floor/room elements active)
                bool inBattle = allComponents.Any(c =>
                    (c.GetType().Name.Contains("RoomState") || c.GetType().Name.Contains("CombatManager"))
                    && c.gameObject.activeInHierarchy);

                if (!inBattle)
                {
                    // Look for any upgrade-related text
                    foreach (var comp in allComponents)
                    {
                        if (!comp.gameObject.activeInHierarchy) continue;

                        string typeName = comp.GetType().Name;
                        if (typeName.Contains("Text"))
                        {
                            string content = null;
                            var textProp = comp.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                content = textProp.GetValue(comp) as string;
                            }

                            if (!string.IsNullOrEmpty(content))
                            {
                                string lowerContent = content.ToLower();
                                // Look for upgrade-related keywords
                                if (lowerContent.Contains("upgrade") ||
                                    lowerContent.Contains("choose") ||
                                    lowerContent.Contains("select") ||
                                    lowerContent.Contains("apply") ||
                                    lowerContent.Contains("spell") && lowerContent.Contains("unit"))
                                {
                                    MonsterTrainAccessibility.LogInfo($"Found upgrade text: {content.Substring(0, Math.Min(50, content.Length))}");
                                    return "UpgradeScreen_Cards_" + cardUIs.Count;
                                }
                            }
                        }
                    }

                    // Even without specific text, multiple cards outside battle suggests upgrade selection
                    // Check if we're in shop context (GameScreen.Shop or just left shop)
                    if (Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Shop ||
                        Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Map)
                    {
                        MonsterTrainAccessibility.LogInfo($"Multiple cards ({cardUIs.Count}) detected outside battle - likely upgrade screen");
                        return "UpgradeScreen_MultiCard_" + cardUIs.Count;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find a child transform by name (case-insensitive, partial match)
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string nameContains)
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

        // Cache the last Dialog component found
        /// <summary>
        /// Find large text content in dialogs/popups
        /// </summary>
        private void FindLargeTextContent(Transform transform, StringBuilder sb)
        {
            if (transform == null || !UITextHelper.IsActuallyVisible(transform.gameObject)) return;

            string name = transform.name.ToLower();

            // Look for common dialog/popup/panel names
            bool isTextPanel = name.Contains("dialog") || name.Contains("popup") ||
                              name.Contains("panel") || name.Contains("modal") ||
                              name.Contains("tooltip") || name.Contains("description") ||
                              name.Contains("content") || name.Contains("text");

            if (isTextPanel)
            {
                // Get all text from this panel
                string text = GetTMPText(transform.gameObject);
                if (!string.IsNullOrEmpty(text) && text.Length > 20)
                {
                    sb.AppendLine(text);
                }

                var uiText = transform.GetComponent<Text>();
                if (uiText != null && !string.IsNullOrEmpty(uiText.text) && uiText.text.Length > 20)
                {
                    sb.AppendLine(uiText.text);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                FindLargeTextContent(child, sb);
            }
        }

        /// <summary>
        /// Check if the game's UI selection has changed and announce it
        /// </summary>
        private void CheckForSelectionChange()
        {
            if (EventSystem.current == null)
                return;

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

            // Selection changed?
            if (currentSelected != _lastSelectedObject)
            {
                _lastSelectedObject = currentSelected;

                // Reset scroll content tracking when selection changes
                _lastScrollContent = null;

                // Auto-cancel stale targeting: the game ends floor/unit selection silently
                // (card plays, mouse click, etc.) without signalling our system. If the
                // current selection is no longer on a targeting element, drop targeting
                // state so information hotkeys like R stop being gated out.
                bool isFloorTargetingElement = currentSelected != null
                    && currentSelected.name.Contains("Tower")
                    && currentSelected.name.Contains("Selectable");
                bool isUnitTargetingElement = currentSelected != null && IsUnitTargetingElement(currentSelected);

                if (!isFloorTargetingElement && MonsterTrainAccessibility.FloorTargeting?.IsTargeting == true)
                {
                    MonsterTrainAccessibility.LogInfo("Selection left floor targeting element, force-cancelling FloorTargetingSystem");
                    MonsterTrainAccessibility.FloorTargeting.ForceCancel();
                }
                if (!isUnitTargetingElement && MonsterTrainAccessibility.UnitTargeting?.IsTargeting == true)
                {
                    MonsterTrainAccessibility.LogInfo("Selection left unit targeting element, force-cancelling UnitTargetingSystem");
                    MonsterTrainAccessibility.UnitTargeting.ForceCancel();
                }

                if (currentSelected != null && UITextHelper.IsActuallyVisible(currentSelected))
                {
                    // Check if this is floor targeting mode (Tower Selectable = floor selection for playing cards)
                    if (isFloorTargetingElement)
                    {
                        // Activate floor targeting system
                        ActivateFloorTargeting();
                        return;
                    }

                    // Check if this is unit targeting mode (targeting a specific character)
                    if (isUnitTargetingElement)
                    {
                        ActivateUnitTargeting();
                        return;
                    }

                    // Check if we're still in a dialog context - if not, clear the tracking
                    // so the dialog text will be announced again if the same dialog appears
                    if (!DialogTextReader.IsInDialogContext(currentSelected))
                    {
                        _lastAnnouncedDialogText = null;
                    }

                    string text = GetTextFromGameObject(currentSelected);
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Clean sprite tags before announcing
                        text = TextUtilities.CleanSpriteTagsForSpeech(text);
                        MonsterTrainAccessibility.ScreenReader?.AnnounceFocus(text);
                    }

                    // If this is a scroll area, remember the initial content
                    if (IsScrollbar(currentSelected))
                    {
                        _lastScrollContent = GetScrollViewContentText(currentSelected);
                    }
                }
            }
        }

        /// <summary>
        /// Check if the current element indicates unit targeting mode
        /// </summary>
        private bool IsUnitTargetingElement(GameObject go)
        {
            string name = go.name.ToLower();

            // Common patterns for unit targeting UI elements
            if (name.Contains("character") && name.Contains("select"))
                return true;
            if (name.Contains("target") && (name.Contains("select") || name.Contains("overlay")))
                return true;
            if (name.Contains("unit") && name.Contains("select"))
                return true;

            // Check for CharacterUI or similar components that might indicate targeting
            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                string compName = comp.GetType().Name.ToLower();
                if (compName.Contains("charactertarget") || compName.Contains("targetselect"))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Activate floor targeting mode when the game enters floor selection
        /// </summary>
        private void ActivateFloorTargeting()
        {
            var targeting = MonsterTrainAccessibility.FloorTargeting;
            if (targeting != null && !targeting.IsTargeting)
            {
                MonsterTrainAccessibility.LogInfo("Detected floor selection mode, activating FloorTargetingSystem");
                // Start targeting without callbacks - we'll let the game handle actual card playing
                // Our system just provides audio feedback for floor navigation
                targeting.StartTargeting(null, (floor) => {
                    // User confirmed floor - do nothing, game handles it
                    MonsterTrainAccessibility.LogInfo($"Floor {floor} confirmed by user");
                }, () => {
                    // User cancelled - do nothing, game handles it
                    MonsterTrainAccessibility.LogInfo("Floor targeting cancelled by user");
                });
            }
        }

        /// <summary>
        /// Activate unit targeting mode when the game enters unit selection
        /// </summary>
        private void ActivateUnitTargeting()
        {
            var targeting = MonsterTrainAccessibility.UnitTargeting;
            if (targeting != null && !targeting.IsTargeting)
            {
                MonsterTrainAccessibility.LogInfo("Detected unit selection mode, activating UnitTargetingSystem");
                targeting.StartTargeting(null, (index) => {
                    MonsterTrainAccessibility.LogInfo($"Unit {index} confirmed by user");
                }, () => {
                    MonsterTrainAccessibility.LogInfo("Unit targeting cancelled by user");
                });
            }
        }

        /// <summary>
        /// Extract readable text from a UI GameObject.
        /// Tries multiple approaches to find text.
        /// </summary>
        private string GetTextFromGameObject(GameObject go)
        {
            if (go == null)
                return null;

            string text = null;

            // Check if this is a scrollbar - if so, try to find the scroll view's content
            if (IsScrollbar(go))
            {
                text = GetScrollViewContentText(go);
                if (!string.IsNullOrEmpty(text))
                {
                    // Truncate very long text for the focus announcement
                    if (text.Length > 300)
                    {
                        text = text.Substring(0, 300) + "... Press T to read full text.";
                    }
                    return text;
                }
            }

            // Check for RunOpeningScreen (Boss Battles screen at start of run) - before dialog check
            text = BattleIntroTextReader.GetRunOpeningScreenText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check for dialog buttons (Yes/No buttons inside Dialog popups)
            text = DialogTextReader.GetDialogButtonText(go, ref _lastAnnouncedDialogText);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Champion upgrade choice tiles must run BEFORE the CardUI check, because
            // the tile embeds a CardUI for the post-upgrade card — if CardTextReader wins,
            // the upgrade's own title (e.g. "Golden Crown") and description get skipped.
            text = UpgradeChoiceTextReader.GetUpgradeChoiceText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check for cards in hand (CardUI component) - full card details
            text = CardTextReader.GetCardUIText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check for shop items (MerchantGoodDetailsUI, MerchantServiceUI)
            text = ShopTextReader.GetShopItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Enemy silhouettes on BattleIntroScreen — walk the TooltipProviderComponent's
            // structured TooltipContent list instead of letting the generic tooltip reader
            // flatten the first entry into garbled text.
            text = BattleIntroEnemyReader.GetEnemyPreviewText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1. Check for Fight button on BattleIntro screen - get battle name
            text = BattleIntroTextReader.GetBattleIntroText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1.6. Check for RelicInfoUI (artifact selection on RelicDraftScreen)
            text = RelicTextReader.GetRelicInfoText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1.7. Check for Dragon's Hoard reward selection tiles
            text = DragonsHoardRewardReader.GetDragonsHoardRewardItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for map node (battle/event/shop nodes on the map)
            text = MapTextReader.GetMapNodeText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for settings screen elements (dropdowns, sliders, toggles with SettingsEntry parent)
            text = SettingsTextReader.GetSettingsElementText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.5. Check for toggle/checkbox components
            text = SettingsTextReader.GetToggleText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3. Check for logbook/compendium items
            text = CompendiumTextReader.GetLogbookItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.5 Check for clan selection icons
            text = ClanSelectionTextReader.GetClanSelectionText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.5b Check for run setup clan selection tiles (RunSetupClanSelectionItemUI)
            text = ClanSelectionTextReader.GetRunSetupClanItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.5c Check for run setup pyre heart selection tiles (RunSetupPyreHeartSelectionItemUI)
            text = PyreHeartTextReader.GetRunSetupPyreHeartItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.6 Check for champion choice buttons
            text = ClanSelectionTextReader.GetChampionChoiceText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.7 Check for buttons with LocalizedTooltipProvider (mutator options, etc.)
            text = TooltipTextReader.GetLocalizedTooltipButtonText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 4. Check for map branch choice elements
            text = MapTextReader.GetBranchChoiceText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 4.5 Check for TooltipProviderComponent (e.g. SubclanVictoryItem in checklist)
            text = TooltipTextReader.GetTooltipTextWithBody(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 5. Try to get text with context (handles short button labels)
            text = GetTextWithContext(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 6. Try the GameObject name as fallback (but make it more readable)
            text = CleanGameObjectName(go.name);

            return text?.Trim();
        }

        // Cached localization method
        /// <summary>
        /// Cache for keyword lookups - maps keyword names to their descriptions
        /// </summary>
        /// <summary>
        /// Get text with additional context for buttons with short labels
        /// </summary>
        private string GetTextWithContext(GameObject go)
        {
            string directText = GetDirectText(go);

            // If text is very short (1-2 chars), it's likely an icon character - use cleaned name instead
            if (!string.IsNullOrEmpty(directText) && directText.Length <= 2)
            {
                string cleanedName = CleanGameObjectName(go.name);
                if (!string.IsNullOrEmpty(cleanedName) && cleanedName.Length > 2)
                {
                    return cleanedName;
                }
            }

            // If text is short (<=4 chars) or empty, look for a better label
            if (string.IsNullOrEmpty(directText) || directText.Length <= 4)
            {
                // Prefer the GameObject's own cleaned name first — for buttons like
                // "Settings Option" with a short label "Esc" (keyboard hint), the
                // object name is far more informative than a hierarchy fallback that
                // would produce "Main Menu Screen: Esc".
                string ownName = CleanGameObjectName(go.name);
                if (!string.IsNullOrEmpty(ownName) && ownName.Length > 4 && !IsGenericContainerName(ownName))
                {
                    return ownName;
                }

                // Otherwise fall back to walking the hierarchy
                string contextLabel = GetContextLabelFromHierarchy(go);
                if (!string.IsNullOrEmpty(contextLabel))
                {
                    if (string.IsNullOrEmpty(directText))
                    {
                        return contextLabel;
                    }
                    return $"{contextLabel}: {directText}";
                }
            }

            return directText;
        }

        private static readonly string[] _genericContainerNames = new[]
        {
            "container", "panel", "holder", "group", "content", "root",
            "options", "input area", "section", "buttons", "layout", "wrapper",
            "item", "entry", "element"
        };

        private bool IsGenericContainerName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string lower = name.ToLowerInvariant().Trim();
            foreach (var g in _genericContainerNames)
            {
                if (lower == g) return true;
            }
            return false;
        }

        /// <summary>
        /// Get direct text from a GameObject (immediate text components)
        /// </summary>
        private string GetDirectText(GameObject go)
        {
            // Try TMP text first
            string text = GetTMPText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text.Trim();
            }

            // Try Unity UI Text
            var uiText = go.GetComponentInChildren<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                return uiText.text.Trim();
            }

            return null;
        }

        /// <summary>
        /// Get context label from hierarchy (parent/sibling/grandparent)
        /// </summary>
        private string GetContextLabelFromHierarchy(GameObject go)
        {
            try
            {
                Transform current = go.transform;

                // Walk up the hierarchy looking for meaningful labels
                for (int depth = 0; depth < 4 && current.parent != null; depth++)
                {
                    Transform parent = current.parent;

                    // Check siblings of current
                    foreach (Transform sibling in parent)
                    {
                        if (sibling == current) continue;

                        string sibText = GetTMPTextDirect(sibling.gameObject);
                        if (string.IsNullOrEmpty(sibText))
                        {
                            var uiText = sibling.GetComponent<Text>();
                            sibText = uiText?.text;
                        }

                        // Accept text that's longer than what we already have
                        if (!string.IsNullOrEmpty(sibText) && sibText.Trim().Length > 4)
                        {
                            return sibText.Trim();
                        }
                    }

                    // Check parent's name
                    string parentName = parent.name;
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        // Clean up common UI suffixes
                        string cleaned = CleanGameObjectName(parentName);
                        if (!string.IsNullOrEmpty(cleaned) && cleaned.Length > 4)
                        {
                            // Make sure it's not just a generic container name
                            string lower = cleaned.ToLower();
                            if (!lower.Contains("container") && !lower.Contains("panel") &&
                                !lower.Contains("holder") && !lower.Contains("group") &&
                                !lower.Contains("content") && !lower.Contains("root") &&
                                !lower.Contains("options") && !lower.Contains("input area") &&
                                !lower.Contains("input") && !lower.Contains("area") &&
                                !lower.Contains("section") && !lower.Contains("buttons") &&
                                !lower.Contains("layout") && !lower.Contains("wrapper"))
                            {
                                return cleaned;
                            }
                        }
                    }

                    current = parent;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Check if a GameObject is a scrollbar
        /// </summary>
        private bool IsScrollbar(GameObject go)
        {
            if (go == null) return false;

            string name = go.name.ToLower();
            if (name.Contains("scrollbar") || name.Contains("scroll bar"))
                return true;

            return go.GetComponent<Scrollbar>() != null;
        }

        /// <summary>
        /// Try to find and read text from a scroll view's content area
        /// </summary>
        private string GetScrollViewContentText(GameObject scrollbarGo)
        {
            try
            {
                // Try to find the ScrollRect in parent or siblings
                Transform parent = scrollbarGo.transform.parent;
                while (parent != null)
                {
                    var scrollRect = parent.GetComponent<ScrollRect>();
                    if (scrollRect != null && scrollRect.content != null)
                    {
                        return GetAllTextFromTransform(scrollRect.content);
                    }

                    // Also check siblings
                    foreach (Transform sibling in parent)
                    {
                        var siblingScrollRect = sibling.GetComponent<ScrollRect>();
                        if (siblingScrollRect != null && siblingScrollRect.content != null)
                        {
                            return GetAllTextFromTransform(siblingScrollRect.content);
                        }
                    }

                    parent = parent.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scroll content: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get all text from a transform and its children
        /// </summary>
        private string GetAllTextFromTransform(Transform root)
        {
            var sb = new StringBuilder();
            CollectAllText(root, sb);
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Recursively collect text from all Text and TMP components
        /// </summary>
        private void CollectAllText(Transform transform, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;

            // Get Text component on this object
            var uiText = transform.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                string cleaned = uiText.text.Trim();
                if (!string.IsNullOrEmpty(cleaned))
                {
                    sb.AppendLine(cleaned);
                }
            }

            // Get TMP text
            string tmpText = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(tmpText))
            {
                string cleaned = tmpText.Trim();
                if (!string.IsNullOrEmpty(cleaned))
                {
                    sb.AppendLine(cleaned);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectAllText(child, sb);
            }
        }

        /// <summary>
        /// Get TMP text from a specific GameObject (not children)
        /// </summary>
        private string GetTMPTextDirect(GameObject go)
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

        /// <summary>
        /// Clean up GameObject name to be more readable
        /// </summary>
        private string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Replace(" Option", "");
            name = name.Replace(" Item", "");
            name = name.Trim();

            // Add spaces before capital letters (CamelCase to words)
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name;
        }

        /// <summary>
        /// Try to get TextMeshPro text using reflection
        /// </summary>
        private string GetTMPText(GameObject go)
        {
            try
            {
                // Look for TMP_Text component (base class for both TextMeshProUGUI and TextMeshPro)
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    var type = component.GetType();

                    // Check if it's a TextMeshPro type
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                return text;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting TMP text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read all visible text on the current screen (press T)
        /// </summary>
        public void ReadAllScreenText()
        {
            try
            {
                var collectedTexts = new HashSet<string>();
                var sb = new StringBuilder();

                // Find all root objects and search for text
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    if (root.activeInHierarchy)
                    {
                        CollectAllTextUnique(root.transform, collectedTexts, sb);
                    }
                }

                // Also find all TMP components directly (they may be in DontDestroyOnLoad)
                FindAllTMPText(collectedTexts, sb);

                // Also check Unity UI Text components
                var allTextComponents = FindObjectsOfType<Text>();
                foreach (var textComp in allTextComponents)
                {
                    if (textComp.gameObject.activeInHierarchy && !string.IsNullOrEmpty(textComp.text))
                    {
                        string cleanText = textComp.text.Trim();
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText))
                        {
                            collectedTexts.Add(cleanText);
                            if (sb.Length > 0) sb.Append(", ");
                            sb.Append(cleanText);
                        }
                    }
                }

                string allText = sb.ToString().Trim();

                if (string.IsNullOrEmpty(allText))
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("No text found on screen", false);
                }
                else
                {
                    // Clean up - remove duplicate empty lines
                    allText = System.Text.RegularExpressions.Regex.Replace(allText, @"(\r?\n){3,}", "\n\n");
                    MonsterTrainAccessibility.ScreenReader?.Speak(allText, false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading screen text: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read screen text", false);
            }
        }

        /// <summary>
        /// Find all TextMeshPro components using reflection
        /// </summary>
        private void FindAllTMPText(HashSet<string> collectedTexts, StringBuilder sb)
        {
            try
            {
                // Find the TMP_Text type
                Type tmpTextType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    tmpTextType = assembly.GetType("TMPro.TMP_Text");
                    if (tmpTextType != null) break;
                }

                if (tmpTextType == null) return;

                // Find all instances using FindObjectsOfType
                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[0]);
                var genericMethod = findMethod.MakeGenericMethod(tmpTextType);
                var allTMP = genericMethod.Invoke(null, null) as Array;

                if (allTMP == null) return;

                var textProperty = tmpTextType.GetProperty("text");
                if (textProperty == null) return;

                foreach (var tmp in allTMP)
                {
                    if (tmp == null) continue;

                    // Check if active
                    var component = tmp as Component;
                    if (component == null || !component.gameObject.activeInHierarchy) continue;

                    string text = textProperty.GetValue(tmp) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        string cleanText = text.Trim();
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText))
                        {
                            collectedTexts.Add(cleanText);
                            if (sb.Length > 0) sb.Append(", ");
                            sb.Append(cleanText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding TMP text: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively collect text, avoiding duplicates
        /// </summary>
        private void CollectAllTextUnique(Transform transform, HashSet<string> collected, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;

            // Get Text component on this object
            var uiText = transform.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                string cleaned = uiText.text.Trim();
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned))
                {
                    collected.Add(cleaned);
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(cleaned);
                }
            }

            // Get TMP text
            string tmpText = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(tmpText))
            {
                string cleaned = tmpText.Trim();
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned))
                {
                    collected.Add(cleaned);
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(cleaned);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectAllTextUnique(child, collected, sb);
            }
        }

        /// <summary>
        /// Called when the main menu screen is shown
        /// </summary>
        public void OnMainMenuEntered(object screen)
        {
            MonsterTrainAccessibility.LogInfo("Main menu entered");
            _isActive = true;
            _lastSelectedObject = null;

            // Announce that we're at the main menu
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Main Menu");

            // Read the currently selected item if any
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                string text = GetTextFromGameObject(EventSystem.current.currentSelectedGameObject);
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.ScreenReader?.Queue(text);
                }
            }
        }

        /// <summary>
        /// Called when the game over screen (victory/defeat) is shown
        /// </summary>
        public void OnGameOverScreenEntered(object screen)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Game over screen handler called");

                // Small delay to let UI populate
                StartCoroutine(ReadGameOverScreenDelayed(screen));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in OnGameOverScreenEntered: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator ReadGameOverScreenDelayed(object screen)
        {
            // Wait for UI to populate
            yield return new UnityEngine.WaitForSeconds(0.5f);

            try
            {
                var sb = new StringBuilder();

                // Try to extract data from the screen object via reflection
                if (screen != null)
                {
                    var screenType = screen.GetType();
                    MonsterTrainAccessibility.LogInfo($"Game over screen type: {screenType.Name}");

                    // Log all fields for debugging
                    foreach (var field in screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        try
                        {
                            var value = field.GetValue(screen);
                            MonsterTrainAccessibility.LogInfo($"  Field: {field.Name} = {value}");
                        }
                        catch { }
                    }
                }

                // Find all text elements in active game over screen
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    if (!root.activeInHierarchy) continue;

                    // Look for game over screen components
                    string rootName = root.name.ToLower();
                    if (rootName.Contains("gameover") || rootName.Contains("summary") ||
                        rootName.Contains("victory") || rootName.Contains("defeat") ||
                        rootName.Contains("result") || rootName.Contains("endrun"))
                    {
                        CollectGameOverText(root.transform, sb);
                    }
                    else
                    {
                        // Also check children with these names
                        foreach (Transform child in root.transform)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;
                            string childName = child.name.ToLower();
                            if (childName.Contains("gameover") || childName.Contains("summary") ||
                                childName.Contains("victory") || childName.Contains("defeat") ||
                                childName.Contains("result") || childName.Contains("endrun"))
                            {
                                CollectGameOverText(child, sb);
                            }
                        }
                    }
                }

                // If we didn't find structured data, try to read all visible TMP text
                if (sb.Length == 0)
                {
                    sb.Append(ReadAllVisibleTextOnScreen());
                }

                string announcement = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Game over announcement: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
                else
                {
                    // Fallback: just announce we're on the game over screen
                    MonsterTrainAccessibility.ScreenReader?.Speak("Run complete. Press T to read stats.", false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading game over screen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete.", false);
            }
        }

        private void CollectGameOverText(Transform root, StringBuilder sb)
        {
            try
            {
                // Collect all meaningful text in order
                var textElements = new List<(int order, string label, string value)>();

                CollectTextRecursive(root, textElements, 0);

                // Sort by order and format
                textElements.Sort((a, b) => a.order.CompareTo(b.order));

                foreach (var elem in textElements)
                {
                    if (!string.IsNullOrEmpty(elem.label) && !string.IsNullOrEmpty(elem.value))
                    {
                        sb.Append($"{elem.label}: {elem.value}. ");
                    }
                    else if (!string.IsNullOrEmpty(elem.value))
                    {
                        sb.Append($"{elem.value}. ");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error collecting game over text: {ex.Message}");
            }
        }

        private void CollectTextRecursive(Transform transform, List<(int order, string label, string value)> textElements, int depth)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy || depth > 15)
                return;

            string objName = transform.name.ToLower();

            // Skip certain elements
            if (objName.Contains("button") && !objName.Contains("label"))
                return;

            // Get text from this element
            string text = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(text))
            {
                text = TextUtilities.StripRichTextTags(text.Trim());

                // Determine if this is a label or value based on name/position
                bool isLabel = objName.Contains("label") || objName.Contains("title") ||
                              objName.Contains("header") || objName.Contains("name");

                // Calculate order based on position (y position inverted since UI goes top-down)
                int order = 0;
                if (transform is RectTransform rt)
                {
                    order = (int)(-rt.anchoredPosition.y * 10 + rt.anchoredPosition.x);
                }

                if (!string.IsNullOrEmpty(text) && text.Length > 0)
                {
                    // Filter out very short or noise text
                    if (text.Length >= 1 && !text.All(c => c == '/' || c == ',' || char.IsWhiteSpace(c)))
                    {
                        textElements.Add((order, isLabel ? text : null, isLabel ? null : text));
                    }
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectTextRecursive(child, textElements, depth + 1);
            }
        }

        private string ReadAllVisibleTextOnScreen()
        {
            var sb = new StringBuilder();
            var seenTexts = new HashSet<string>();

            try
            {
                // Find all TMP components in the scene
                var tmpComponents = UnityEngine.Object.FindObjectsOfType<Component>();
                var textList = new List<(float y, string text)>();

                foreach (var comp in tmpComponents)
                {
                    if (comp == null || !comp.gameObject.activeInHierarchy)
                        continue;

                    var type = comp.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProp = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                        if (textProp != null)
                        {
                            string text = textProp.GetValue(comp) as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                text = TextUtilities.StripRichTextTags(text.Trim());
                                if (!string.IsNullOrEmpty(text) && text.Length > 1 && !seenTexts.Contains(text))
                                {
                                    seenTexts.Add(text);

                                    // Get Y position for ordering
                                    float yPos = 0;
                                    if (comp.transform is RectTransform rt)
                                    {
                                        yPos = rt.position.y;
                                    }

                                    textList.Add((yPos, text));
                                }
                            }
                        }
                    }
                }

                // Sort by Y position (top to bottom)
                textList.Sort((a, b) => b.y.CompareTo(a.y));

                foreach (var item in textList)
                {
                    sb.Append(item.text);
                    sb.Append(". ");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading all visible text: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Force re-read the current selection
        /// </summary>
        public void RereadCurrentSelection()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                string text = GetTextFromGameObject(EventSystem.current.currentSelectedGameObject);
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
                }
                else
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Unknown item", false);
                }
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Nothing selected", false);
            }
        }

        /// <summary>
        /// Pause menu reading (e.g., during loading)
        /// </summary>
        public void Pause()
        {
            _isActive = false;
        }

        /// <summary>
        /// Resume menu reading
        /// </summary>
        public void Resume()
        {
            _isActive = true;
            _lastSelectedObject = null; // Force re-announce
        }
    }

    /// <summary>
    /// Info about a clan/class for selection
    /// </summary>
    public class ClanInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Results from a completed run
    /// </summary>
    public class RunResults
    {
        public bool Won { get; set; }
        public int Score { get; set; }
        public int CovenantLevel { get; set; }
        public int FloorsCleared { get; set; }
        public int EnemiesDefeated { get; set; }
        public int CardsPlayed { get; set; }
    }
}
