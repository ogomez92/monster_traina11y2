# Verified Patch Targets

This document lists the game classes and methods that can be patched, verified from the Trainworks modding toolkit.

## Confirmed Classes & Methods (from Trainworks patches)

### Screen Management

| Class | Method | Purpose |
|-------|--------|---------|
| `MainMenuScreen` | `Initialize` | Main menu shown |
| `RunSetupScreen` | `Initialize` | Run setup / Clan selection (MT2) |
| `CardDraftScreen` | `Setup` | Card draft/reward screen |
| `LoadingScreen` | `FadeOutFullScreen` | Screen transitions complete |
| `StoryEventScreen` | `Initialize`/`SetupStory` | Random event screen |
| `RewardScreen` | `Initialize` | Post-battle rewards |
| `RelicDraftScreen` | `Initialize` | Artifact selection |
| `DeckScreen` | `Initialize` | Deck viewing |
| `CompendiumScreen` | `Initialize` | Compendium/logbook |
| `ChampionUpgradeScreen` | `Initialize` | Champion upgrade selection |
| `RunHistoryScreen` | `Initialize` | Previous runs |
| `CreditsScreen` | `Initialize` | Credits screen |
| `DragonsHoardScreen` | `Initialize` | Dragon's Hoard (MT2) |
| `ElixirDraftScreen` | `Initialize` | Elixir selection (MT2) |
| `RunOpeningScreen` | `Initialize` | Run preview/overview |
| `RunSummaryScreen` | `Initialize` | End of run stats |
| `ChallengeOverviewScreen` | `Initialize` | Challenge list |
| `ChallengeDetailsScreen` | `Initialize` | Challenge rules |
| `CardDetailsScreen` | `ShowCard` | Card details popup |
| `MinimapScreen` | `Initialize` | Minimap view |
| `TrainCosmeticsScreen` | `Initialize` | Train customization |
| `GameOverScreen` | `Initialize` | Victory/Defeat screen |
| `SettingsScreen` | `Initialize` | Settings menu |
| `MerchantScreen` | `Initialize` | Shop screen |
| `BattleIntroScreen` | `Initialize` | Pre-battle info |
| `MapScreen` | `Initialize` | Map navigation |

### Game State

| Class | Method | Purpose |
|-------|--------|---------|
| `SaveManager` | `Initialize` | Game save system ready |
| `SaveManager` | `LoadClassesFromStartingConditions` | Run starting |
| `AssetLoadingManager` | `Start` | Assets loaded |

### Combat

| Class | Method | Purpose |
|-------|--------|---------|
| `MonsterManager` | `InstantiateCharacter` | Unit spawned |
| `BattleTriggeredEvents` | `Init` | Battle triggers setup |
| `RelicManager` | `ApplyStartOfRunRelicEffects` | Relic effects applied |

### UI Components

| Class | Method | Purpose |
|-------|--------|---------|
| `CardFrameUI` | `SetUpFrame` | Card UI displayed |
| `CharacterUIMeshSpine` | `Setup` | Character sprite setup |
| `CharacterUIMeshSpine` | `CreateAnimInfo` | Animation info |
| `TooltipUI` | `InitCardExplicitTooltip` | Tooltip shown |

### Targeting

| Class | Method | Purpose |
|-------|--------|---------|
| `TargetHelper` | `CollectCardTargets` | Get valid targets |
| `TargetHelper` | `CheckTargetsOverride` | Target validation |
| `TargetHelper` | `IsCardDropTargetRoom` | Room targeting |

## Classes to Investigate (need ILSpy verification)

These are commonly used in similar mods but need verification against the actual game DLLs:

### Combat Events
- `CombatManager.StartCombat` - Battle begins
- `CombatManager.StartPlayerTurn` - Player turn starts
- `CombatManager.EndPlayerTurn` - Player ends turn
- `CombatManager.EndCombat` - Battle ends

### Card Events
- `CardManager.DrawCards` - Cards drawn
- `CardManager.PlayCard` - Card played
- `CardManager.DiscardCard` - Card discarded

### Character Events
- `CharacterState.Die` - Unit dies
- `CharacterState.AddStatusEffect` - Status applied
- `CharacterState.GetHP` / `GetMaxHP` - Health reading
- `CharacterState.GetAttackDamage` - Attack reading

### Room State
- `RoomState.GetCharactersInRoom` - Get units on floor
- `RoomState.GetCapacity` - Floor capacity

## How to Verify with ILSpy

1. Open ILSpy
2. Load `Assembly-CSharp.dll` from `MonsterTrain2_Data\Managed\`
3. Search for class names listed above
4. Verify method signatures match

## Key Game Types

From Trainworks usage:

```csharp
// Team enum
Team.Type.Monsters  // Player's units
Team.Type.Heroes    // Enemy units (yes, confusing naming)

// Card types
CardType.Monster
CardType.Spell
CardType.Blight

// Status effects accessed via
CharacterState.GetStatusEffects()
StatusEffectState.GetStatusId()
StatusEffectState.GetStacks()

// Localization
string.Localize() // Extension method for localized text
```

## Signals (Event System)

Monster Train uses a signal system for events. Key signals to potentially hook:

- `CardManager.cardPlayedSignal`
- `CardManager.cardPilesChangedSignal`
- `CardManager.deckShuffledSignal`

These would need to be subscribed to rather than patched.
