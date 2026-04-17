using MonsterTrainAccessibility.Battle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// MonoBehaviour that handles accessibility hotkeys.
    /// Navigation is handled by the game's EventSystem - we just provide info hotkeys.
    /// </summary>
    public class InputInterceptor : MonoBehaviour
    {
        /// <summary>
        /// Whether input handling is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Cooldown to prevent key repeat spam
        /// </summary>
        private float _inputCooldown = 0f;
        private const float INPUT_COOLDOWN_TIME = 0.15f;
        private bool _announceGameSpeedNextFrame = false;

        private void AnnounceGameSpeed()
        {
            try
            {
                var battle = MonsterTrainAccessibility.BattleHandler;
                var cacheField = battle?.GetType().GetField("_cache",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var cache = cacheField?.GetValue(battle) as Battle.BattleManagerCache;
                var saveManager = cache?.SaveManager;
                if (saveManager == null)
                {
                    // Fallback to finding SaveManager on any GameObject in scene.
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("SaveManager");
                        if (t != null)
                        {
                            saveManager = UnityEngine.Object.FindObjectOfType(t);
                            break;
                        }
                    }
                }
                if (saveManager == null) return;

                var method = saveManager.GetType().GetMethod("GetActiveGameSpeed", System.Type.EmptyTypes);
                var result = method?.Invoke(saveManager, null);
                if (result == null) return;

                string label;
                switch (result.ToString())
                {
                    case "Normal": label = "Normal speed"; break;
                    case "Fast": label = "Fast speed"; break;
                    case "Ultra": label = "Ultra speed"; break;
                    case "SuperUltra": label = "Super ultra speed"; break;
                    case "Instant": label = "Instant speed"; break;
                    default: label = $"Speed {result}"; break;
                }
                MonsterTrainAccessibility.ScreenReader?.Speak(label, false);
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"AnnounceGameSpeed error: {ex.Message}");
            }
        }

        private void Update()
        {
            if (!IsEnabled)
                return;

            // Check if game has focus
            if (!Application.isFocused)
                return;

            // Passive listeners for game-native keys. These always run — we don't
            // consume the input, we just announce what the game did so screen-reader
            // users know it worked.
            if (Input.GetKeyDown(KeyCode.N))
            {
                // Game speed toggle runs during its own Update; wait one frame then read.
                _announceGameSpeedNextFrame = true;
            }
            else if (_announceGameSpeedNextFrame)
            {
                _announceGameSpeedNextFrame = false;
                AnnounceGameSpeed();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                // Game shows the card preview for the hovered/selected card — re-read
                // the current selection so the player hears the full card details.
                MonsterTrainAccessibility.MenuHandler?.RereadCurrentSelection();
            }

            // Update cooldown
            if (_inputCooldown > 0)
            {
                _inputCooldown -= Time.unscaledDeltaTime;
                return;
            }

            var config = MonsterTrainAccessibility.AccessibilitySettings;

            if (config == null)
                return;

            // Skip most input handling if floor or unit targeting is active
            // (those systems own their input).
            if (FloorTargetingSystem.Instance?.IsTargeting == true ||
                UnitTargetingSystem.Instance?.IsTargeting == true)
            {
                // Only allow help key during targeting
                if (Input.GetKeyDown(config.HelpKey.Value))
                {
                    MonsterTrainAccessibility.HelpSystem?.ShowHelp();
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
                return;
            }

            // Help key (F1) - always available
            if (Input.GetKeyDown(config.HelpKey.Value))
            {
                MonsterTrainAccessibility.HelpSystem?.ShowHelp();
                _inputCooldown = INPUT_COOLDOWN_TIME;
                return;
            }

            // Information hotkeys - these don't interfere with game navigation
            if (Input.GetKeyDown(config.ReadCurrentKey.Value))
            {
                RereadCurrentSelection();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ReadTextKey.Value))
            {
                ReadAllScreenText();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ReadHandKey.Value))
            {
                ReadHand();
            }
            else if (Input.GetKeyDown(config.ReadFloorsKey.Value))
            {
                ReadFloors();
            }
            else if (Input.GetKeyDown(config.ReadEnemiesKey.Value))
            {
                ReadEnemies();
            }
            else if (Input.GetKeyDown(config.ReadResourcesKey.Value))
            {
                MonsterTrainAccessibility.LogInfo($"ReadResources key ({config.ReadResourcesKey.Value}) pressed");
                ReadResources();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ReadGoldKey.Value))
            {
                ReadGold();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ToggleVerbosityKey.Value))
            {
                config.CycleVerbosity();
            }
            else if (Input.GetKeyDown(config.EndTurnKey.Value))
            {
                EndTurn();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.F10))
            {
                DebugDumper.DumpScreenToLog();
                MonsterTrainAccessibility.ScreenReader?.Speak("Screen dumped to log", false);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }

        }

        /// <summary>
        /// End the player's turn
        /// </summary>
        private void EndTurn()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.EndTurn();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Re-read the currently selected UI element
        /// </summary>
        private void RereadCurrentSelection()
        {
            MonsterTrainAccessibility.MenuHandler?.RereadCurrentSelection();
        }

        /// <summary>
        /// Read all text on screen (patch notes, descriptions, etc.)
        /// </summary>
        private void ReadAllScreenText()
        {
            MonsterTrainAccessibility.MenuHandler?.ReadAllScreenText();
        }

        /// <summary>
        /// Read the player's current hand
        /// </summary>
        private void ReadHand()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceHand();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read all floor information
        /// </summary>
        private void ReadFloors()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceAllFloors();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read enemy information and intents
        /// </summary>
        private void ReadEnemies()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceEnemies();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read current resources (ember, pyre health, gold)
        /// </summary>
        private void ReadResources()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceResources();
            }
            else
            {
                // Could also read gold/other resources outside battle
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read current gold amount
        /// </summary>
        private void ReadGold()
        {
            int gold = GetCurrentGold();
            if (gold >= 0)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"{gold} {Utilities.ModLocalization.Gold}", false);
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Gold not available");
            }
        }

        /// <summary>
        /// Get the player's current gold from SaveManager
        /// </summary>
        public static int GetCurrentGold()
        {
            try
            {
                // Find SaveManager instance
                var saveManagerType = System.Type.GetType("SaveManager, Assembly-CSharp");
                if (saveManagerType == null)
                {
                    // Try finding it in loaded assemblies
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        saveManagerType = assembly.GetType("SaveManager");
                        if (saveManagerType != null) break;
                    }
                }

                if (saveManagerType != null)
                {
                    // Try to get instance
                    var instanceProp = saveManagerType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    object saveManager = instanceProp?.GetValue(null);

                    if (saveManager == null)
                    {
                        // Try FindObjectOfType
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                            null, new[] { typeof(System.Type) }, null);
                        if (findMethod != null)
                        {
                            saveManager = findMethod.Invoke(null, new object[] { saveManagerType });
                        }
                    }

                    if (saveManager != null)
                    {
                        // Try GetGold method
                        var getGoldMethod = saveManagerType.GetMethod("GetGold", System.Type.EmptyTypes);
                        if (getGoldMethod != null)
                        {
                            var result = getGoldMethod.Invoke(saveManager, null);
                            if (result is int gold)
                            {
                                return gold;
                            }
                        }

                        // Try gold field/property
                        var goldProp = saveManagerType.GetProperty("Gold") ??
                                      saveManagerType.GetProperty("CurrentGold");
                        if (goldProp != null)
                        {
                            var result = goldProp.GetValue(saveManager);
                            if (result is int gold)
                            {
                                return gold;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting gold: {ex.Message}");
            }

            return -1;
        }

        /// <summary>
        /// Temporarily disable input handling
        /// </summary>
        public void Pause()
        {
            IsEnabled = false;
        }

        /// <summary>
        /// Re-enable input handling
        /// </summary>
        public void Resume()
        {
            IsEnabled = true;
        }
    }
}
