using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonsterTrainAccessibility.Battle;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Help.Contexts;
using MonsterTrainAccessibility.Patches;
using MonsterTrainAccessibility.Patches.Combat;
using MonsterTrainAccessibility.Patches.Screens;
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

        private static Harmony _harmony;
        private static bool _initialized = false;
        private static bool _handlersCreated = false;
#pragma warning disable CS0414
        private static bool _quitting;
#pragma warning restore CS0414

        private void Awake()
        {
            // Only initialize once — if the plugin MonoBehaviour is recreated, skip
            if (_initialized)
            {
                Log = Logger;
                Log.LogInfo("Plugin Awake() called again — already initialized, skipping");
                return;
            }

            Instance = this;
            Log = Logger;

            Log.LogInfo($"{NAME} v{VERSION} is loading...");

            try
            {
                // Initialize configuration first
                AccessibilitySettings = new AccessibilityConfig(Config);

                // Wire the sprite-name resolver so card text icons (e.g. <sprite name="DragonsHoard">)
                // get replaced with the game's localized display name instead of a lower-cased asset id.
                Utilities.TextUtilities.SpriteNameResolver = Utilities.LocalizationHelper.GetSpriteDisplayName;

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

                // Create MonoBehaviour handlers immediately on a persistent GameObject
                CreateHandlers();

                Log.LogInfo($"{NAME} loaded successfully!");

                // Subscribe to scene loading events for handler recovery
                SceneManager.sceneLoaded += OnSceneLoaded;
                Log.LogInfo("Subscribed to scene load events");

                _initialized = true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to initialize {NAME}: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log?.LogInfo($"Scene loaded: {scene.name} (mode: {mode})");
            EnsureHandlers();
        }

        /// <summary>
        /// Verify handlers still exist and recreate if destroyed.
        /// Called from scene loads and Harmony patches for resilient recovery.
        /// </summary>
        public static void EnsureHandlers()
        {
            if (_handlersCreated && MenuHandler == null)
            {
                Log?.LogWarning("MenuHandler was destroyed! Recreating...");
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

            // Soul Savior, Soulforge, and additional screen patches
            ChallengeProgressScreenPatch.TryPatch(_harmony);
            EndlessMutatorDraftScreenPatch.TryPatch(_harmony);
            RegionSelectionScreenPatch.TryPatch(_harmony);
            SoulDraftScreenPatch.TryPatch(_harmony);
            SoulProgressionScreenPatch.TryPatch(_harmony);
            SoulSaviorMapScreenPatch.TryPatch(_harmony);
            SoulSaviorRunSetupScreenPatch.TryPatch(_harmony);
            SoulforgeScreenPatch.TryPatch(_harmony);
            RunOpeningSoulSaviorScreenPatch.TryPatch(_harmony);
            FirstTimeSettingsScreenPatch.TryPatch(_harmony);
            CharacterDialogueScreenPatch.TryPatch(_harmony);

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
            TriggerAbilityPatch.TryPatch(_harmony);
            AttackBuffPatch.TryPatch(_harmony);
            AttackDebuffPatch.TryPatch(_harmony);
            MaxHPDebuffPatch.TryPatch(_harmony);
            CharacterMovementPatch.TryPatch(_harmony);
            CardFreezePatch.TryPatch(_harmony);
            RoomSelectionPatch.TryPatch(_harmony);
            SpawnPointChangedPatch.TryPatch(_harmony);
            EquipmentPatch.TryPatch(_harmony);
            MoonPhasePatch.TryPatch(_harmony);
            PyreHealPatch.TryPatch(_harmony);
            WaveStartPatch.TryPatch(_harmony);

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

        private static void CreateHandlers()
        {
            try
            {
                Log.LogInfo("CreateHandlers: Creating handler GameObject");
                // Create a persistent GameObject for all MonoBehaviour handlers
                var handlerGO = new GameObject("MonsterTrainAccessibility_Handlers");
                UnityEngine.Object.DontDestroyOnLoad(handlerGO);

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

                _handlersCreated = true;
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
                new RegionSelectionHelp(),     // Priority 50 - region selection
                new SettingsHelp(),            // Priority 55 - settings screen
                new CompendiumHelp(),          // Priority 50 - compendium/logbook
                new SoulProgressionHelp(),     // Priority 50 - soul progression
                new MapHelp(),                 // Priority 60
                new SoulSaviorHelp(),          // Priority 60 - soul savior map
                new DeckViewHelp(),            // Priority 65 - deck viewing
                new ShopHelp(),                // Priority 70
                new SoulforgeHelp(),           // Priority 70 - soulforge crafting
                new EventHelp(),               // Priority 70
                new RewardsHelp(),             // Priority 75 - post-battle rewards
                new CardDraftHelp(),           // Priority 80
                new ChampionUpgradeHelp(),     // Priority 80 - champion upgrade
                new SoulDraftHelp(),           // Priority 80 - soul draft
                new MutatorDraftHelp(),        // Priority 80 - mutator draft
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

        private void OnDestroy()
        {
            // The plugin MonoBehaviour gets destroyed on scene transitions.
            // Do NOT clean up here — Harmony patches and handlers must survive.
            Log?.LogInfo("Plugin OnDestroy called (scene transition — not cleaning up)");
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            Log?.LogInfo("Application quitting — cleaning up");

            SceneManager.sceneLoaded -= OnSceneLoaded;
            ScreenReader?.Shutdown();
            _harmony?.UnpatchSelf();

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
