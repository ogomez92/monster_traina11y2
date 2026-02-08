using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonsterTrainAccessibility.Battle;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Help.Contexts;
using MonsterTrainAccessibility.Patches;
using MonsterTrainAccessibility.Screens;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTrainAccessibility
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("MonsterTrain2.exe")]
    public class MonsterTrainAccessibility : BaseUnityPlugin
    {
        public const string GUID = "com.accessibility.monstertrain";
        public const string NAME = "Monster Train Accessibility";
        public const string VERSION = "1.0.0";

        // Static reference for global access
        public static MonsterTrainAccessibility Instance { get; private set; }

        // Logging
        internal static ManualLogSource Log { get; private set; }

        // Core modules
        public static ScreenReaderOutput ScreenReader { get; private set; }
        public static VirtualFocusManager FocusManager { get; private set; }
        public static InputInterceptor InputHandler { get; private set; }
        public static AccessibilityConfig AccessibilitySettings { get; private set; }

        // Help and targeting systems
        public static HelpSystem HelpSystem { get; private set; }
        public static FloorTargetingSystem FloorTargeting { get; private set; }
        public static UnitTargetingSystem UnitTargeting { get; private set; }

        // Screen-specific handlers
        public static MenuAccessibility MenuHandler { get; private set; }
        public static BattleAccessibility BattleHandler { get; private set; }
        public static CardDraftAccessibility DraftHandler { get; private set; }
        public static MapAccessibility MapHandler { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo($"{NAME} v{VERSION} is loading...");

            try
            {
                // Initialize configuration first
                AccessibilitySettings = new AccessibilityConfig(Config);

                // Initialize screen reader output (Tolk)
                ScreenReader = new ScreenReaderOutput();
                ScreenReader.Initialize();

                // Initialize focus management
                FocusManager = new VirtualFocusManager();

                // Initialize help system
                HelpSystem = new HelpSystem();
                RegisterHelpContexts();

                // Initialize screen handlers (non-MonoBehaviour ones)
                BattleHandler = new BattleAccessibility();
                DraftHandler = new CardDraftAccessibility();
                MapHandler = new MapAccessibility();

                // Apply Harmony patches
                _harmony = new Harmony(GUID);
                ApplyPatches();

                Log.LogInfo($"{NAME} loaded successfully!");

                // Subscribe to scene loading events - create handlers when first real scene loads
                SceneManager.sceneLoaded += OnSceneLoaded;
                Log.LogInfo("Subscribed to scene load events");

                // Don't create handlers here - wait until first scene loads
                // CreateHandlers();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to initialize {NAME}: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        private bool _handlersCreated = false;
        private bool _announcedReady = false;

        private void Start()
        {
            Log.LogInfo("Plugin Start() called - MonoBehaviour is active");
            // Don't create handlers here - they get destroyed during scene transitions
            // Wait for the first real scene to load
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"Scene loaded: {scene.name} (mode: {mode})");

            // Create handlers after a real game scene loads (not during initial BepInEx loading)
            // Wait for a scene that indicates the game is actually running
            if (!_handlersCreated && (scene.name == "main" || scene.name == "main_menu" || scene.name == "hud"))
            {
                Log.LogInfo($"Creating handlers on scene: {scene.name}");
                CreateHandlers();
                _handlersCreated = true;
            }

            // Announce ready once handlers are created
            if (_handlersCreated && !_announcedReady)
            {
                _announcedReady = true;
                ScreenReader?.Speak("Monster Train Accessibility ready", false);
            }

            // Verify handlers still exist
            if (_handlersCreated && MenuHandler == null)
            {
                Log.LogWarning("MenuHandler was destroyed! Recreating...");
                CreateHandlers();
            }
        }

        private void ApplyPatches()
        {
            // Screen transition patches - Core screens
            MainMenuScreenPatch.TryPatch(_harmony);
            BattleIntroScreenPatch.TryPatch(_harmony);
            CombatStartPatch.TryPatch(_harmony);
            CardDraftScreenPatch.TryPatch(_harmony);
            ClassSelectionScreenPatch.TryPatch(_harmony);
            MapScreenPatch.TryPatch(_harmony);
            MerchantScreenPatch.TryPatch(_harmony);
            EnhancerSelectionScreenPatch.TryPatch(_harmony);
            GameOverScreenPatch.TryPatch(_harmony);
            SettingsScreenPatch.TryPatch(_harmony);
            ScreenManagerPatch.TryPatch(_harmony);
            DialogPatch.TryPatch(_harmony);
            FtueTooltipPatch.TryPatch(_harmony);

            // Screen transition patches - Additional screens
            StoryEventScreenPatch.TryPatch(_harmony);
            RewardScreenPatch.TryPatch(_harmony);
            RelicDraftScreenPatch.TryPatch(_harmony);
            DeckScreenPatch.TryPatch(_harmony);
            CompendiumScreenPatch.TryPatch(_harmony);
            ChampionUpgradeScreenPatch.TryPatch(_harmony);
            RunHistoryScreenPatch.TryPatch(_harmony);
            CreditsScreenPatch.TryPatch(_harmony);
            DragonsHoardScreenPatch.TryPatch(_harmony);
            ElixirDraftScreenPatch.TryPatch(_harmony);
            RunOpeningScreenPatch.TryPatch(_harmony);
            RunSummaryScreenPatch.TryPatch(_harmony);
            ChallengeScreenPatch.TryPatch(_harmony);
            CardDetailsScreenPatch.TryPatch(_harmony);
            MinimapScreenPatch.TryPatch(_harmony);
            TrainCosmeticsScreenPatch.TryPatch(_harmony);
            StatHighlightPatch.TryPatch(_harmony);
            WinStreakPatch.TryPatch(_harmony);
            UnlockScreenPatch.TryPatch(_harmony);
            KeyMappingScreenPatch.TryPatch(_harmony);

            // Combat event patches
            PlayerTurnStartPatch.TryPatch(_harmony);
            PlayerTurnEndPatch.TryPatch(_harmony);
            DamageAppliedPatch.TryPatch(_harmony);
            CharacterDamagePatch.TryPatch(_harmony);
            UnitDeathPatch.TryPatch(_harmony);
            StatusEffectPatch.TryPatch(_harmony);
            BattleVictoryPatch.TryPatch(_harmony);
            UnitSpawnPatch.TryPatch(_harmony);
            EnemyAscendPatch.TryPatch(_harmony);
            PyreDamagePatch.TryPatch(_harmony);
            EnemyDialoguePatch.TryPatch(_harmony);
            CombatPhasePatch.TryPatch(_harmony);
            HealAppliedPatch.TryPatch(_harmony);
            RelicTriggeredPatch.TryPatch(_harmony);
            StatusEffectRemovedPatch.TryPatch(_harmony);
            CombatPhaseChangePatch.TryPatch(_harmony);
            AllEnemiesDefeatedPatch.TryPatch(_harmony);
            MaxHPBuffPatch.TryPatch(_harmony);
            UpdateHpPatch.TryPatch(_harmony);

            // Card event patches
            CardDrawPatch.TryPatch(_harmony);
            CardPlayedPatch.TryPatch(_harmony);
            CardDiscardedPatch.TryPatch(_harmony);
            DeckShuffledPatch.TryPatch(_harmony);
            CardExhaustedPatch.TryPatch(_harmony);
            CardSelectionErrorPatch.TryPatch(_harmony);
            HandChangedPatch.TryPatch(_harmony);

            // Card targeting patches
            CardTargetingPatches.TryPatch(_harmony);
        }

        private void CreateHandlers()
        {
            try
            {
                Log.LogInfo("CreateHandlers: Creating handler GameObject");
                // Create a persistent GameObject for all MonoBehaviour handlers
                var handlerGO = new GameObject("MonsterTrainAccessibility_Handlers");
                DontDestroyOnLoad(handlerGO);

                Log.LogInfo("CreateHandlers: Adding InputInterceptor");
                InputHandler = handlerGO.AddComponent<InputInterceptor>();
                Log.LogInfo($"CreateHandlers: InputHandler = {(InputHandler != null ? "OK" : "NULL")}");

                Log.LogInfo("CreateHandlers: Adding MenuAccessibility");
                MenuHandler = handlerGO.AddComponent<MenuAccessibility>();
                Log.LogInfo($"CreateHandlers: MenuHandler = {(MenuHandler != null ? "OK" : "NULL")}");

                Log.LogInfo("CreateHandlers: Adding FloorTargetingSystem");
                FloorTargeting = handlerGO.AddComponent<FloorTargetingSystem>();

                Log.LogInfo("CreateHandlers: Adding UnitTargetingSystem");
                UnitTargeting = handlerGO.AddComponent<UnitTargetingSystem>();

                Log.LogInfo("CreateHandlers: Complete");
            }
            catch (Exception ex)
            {
                Log.LogError($"CreateHandlers FAILED: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        /// <summary>
        /// Register all help contexts with the help system
        /// </summary>
        private void RegisterHelpContexts()
        {
            HelpSystem.RegisterContexts(
                new GlobalHelp(),              // Priority 0 - fallback
                new MainMenuHelp(),            // Priority 40
                new ChallengesHelp(),          // Priority 45 - challenges screen
                new ClanSelectionHelp(),       // Priority 50
                new CompendiumHelp(),          // Priority 50 - compendium/logbook
                new MapHelp(),                 // Priority 60
                new DeckViewHelp(),            // Priority 65 - deck viewing
                new ShopHelp(),                // Priority 70
                new EventHelp(),               // Priority 70
                new RewardsHelp(),             // Priority 75 - post-battle rewards
                new CardDraftHelp(),           // Priority 80
                new ChampionUpgradeHelp(),     // Priority 80 - champion upgrade
                new ArtifactSelectionHelp(),   // Priority 80 - artifact/relic selection
                new BattleIntroHelp(),         // Priority 85 - pre-battle screen
                new BattleHelp(),              // Priority 90
                new TutorialHelp(),            // Priority 95 - tutorial popups
                new BattleTargetingHelp(),     // Priority 100 - floor targeting
                new UnitTargetingHelp(),       // Priority 101 - unit targeting (highest)
                new DialogHelp()               // Priority 110 - dialog popups
            );
            Log.LogInfo("Registered help contexts");
        }

        // Debug: track game state
        private float _lastStateCheckTime = 0f;
        private string _lastDetectedScreen = "";
        private bool _hasAnnouncedFirstScreen = false;

        private void Update()
        {
            // Check game state every 2 seconds for debugging
            if (Time.time - _lastStateCheckTime > 2f)
            {
                _lastStateCheckTime = Time.time;
                TryDetectCurrentScreen();
            }
        }

        private void TryDetectCurrentScreen()
        {
            try
            {
                // Try to find any active screen by looking for common screen types
                string currentScreen = "Unknown";

                // Look for AllGameManagers to see if game systems are loaded
                var allManagersType = AccessTools.TypeByName("AllGameManagers");
                if (allManagersType != null)
                {
                    var instanceProp = allManagersType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        var instance = instanceProp.GetValue(null);
                        if (instance != null)
                        {
                            // Game managers are loaded - try to get screen manager
                            var screenManagerProp = allManagersType.GetProperty("ScreenManager");
                            if (screenManagerProp != null)
                            {
                                var screenManager = screenManagerProp.GetValue(instance);
                                if (screenManager != null)
                                {
                                    // Try to get current screen
                                    var smType = screenManager.GetType();
                                    var currentScreenProp = smType.GetProperty("CurrentScreen") ?? smType.GetProperty("ActiveScreen");
                                    if (currentScreenProp != null)
                                    {
                                        var screen = currentScreenProp.GetValue(screenManager);
                                        if (screen != null)
                                        {
                                            currentScreen = screen.GetType().Name;
                                        }
                                    }
                                    else
                                    {
                                        // Try GetCurrentScreen method
                                        var getCurrentMethod = smType.GetMethod("GetCurrentScreen") ?? smType.GetMethod("GetActiveScreen");
                                        if (getCurrentMethod != null && getCurrentMethod.GetParameters().Length == 0)
                                        {
                                            var screen = getCurrentMethod.Invoke(screenManager, null);
                                            if (screen != null)
                                            {
                                                currentScreen = screen.GetType().Name;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // If screen changed, log and announce
                if (currentScreen != _lastDetectedScreen)
                {
                    Log.LogInfo($"Detected screen change: {_lastDetectedScreen} -> {currentScreen}");
                    _lastDetectedScreen = currentScreen;

                    // Announce the first detected screen
                    if (!_hasAnnouncedFirstScreen && currentScreen != "Unknown")
                    {
                        _hasAnnouncedFirstScreen = true;
                        string readable = currentScreen.Replace("Screen", "");
                        ScreenReader?.Speak($"{readable}. Press F1 for help.", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error detecting screen: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            Log.LogInfo("OnDestroy called - plugin shutting down");

            // Unsubscribe from scene events
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Clean up
            ScreenReader?.Shutdown();
            _harmony?.UnpatchSelf();

            // Destroy the handler GameObject (contains both InputHandler and MenuHandler)
            if (InputHandler != null && InputHandler.gameObject != null)
            {
                Destroy(InputHandler.gameObject);
            }
        }

        // Utility method for other classes to log
        public static void LogInfo(string message)
        {
            Log?.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            Log?.LogWarning(message);
        }

        public static void LogError(string message)
        {
            Log?.LogError(message);
        }
    }
}
