# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Monster Train 2 Accessibility Mod - a BepInEx plugin that enables blind players to play Monster Train 2 through screen reader integration (Tolk library) and keyboard navigation.

## Build Commands

```bash
cd MonsterTrainAccessibility
dotnet build -c Release
```

Output: `release/BepInEx/plugins/MonsterTrainAccessibility.dll` (auto-copied to game plugins folder).

The csproj uses `$(MonsterTrainPath)` defaulting to `C:\Program Files (x86)\Steam\steamapps\common\Monster Train 2`. Override with env var `MONSTER_TRAIN_PATH` or `-p:MonsterTrainPath="path"`.

**Build verification**: The game may not be installed on the dev machine. Build always produces assembly reference errors (CS0246, CS0234, CS0012) which are expected. To verify no real errors, filter with:
```bash
dotnet build -c Release 2>&1 | grep -v "CS0246\|CS0234\|CS0012\|warning"
```

## Testing

No automated tests. Test by building, launching MT2, and checking:
- Log: `C:\Program Files (x86)\Steam\steamapps\common\Monster Train 2\BepInEx\LogOutput.log`
- Screen reader announcements with NVDA running

## Game Source Reference

The `game/` directory contains ~1900 decompiled MT2 game source files (Assembly-CSharp). **Always consult these when writing reflection code** to get exact method signatures, field names, parameter types, and enum values. Key files:
- `game/CardState.cs`, `game/CardData.cs` - card system
- `game/CharacterData.cs`, `game/SubtypeData.cs` - unit data
- `game/CollectableRarity.cs` - rarity enum (Common, Uncommon, Rare, Champion, Starter, Unset)
- `game/RelicData.cs` - artifact/relic system
- `game/StoryEventScreen.cs` - event screens
- `game/MerchantGoodUIBase.cs`, `game/BuyButton.cs` - shop system
- `game/AllGameManagers.cs` - central manager access

## Architecture

All game data access uses **runtime reflection** - no public API. Types discovered at runtime, methods cached for performance.

### Data Flow

```
Harmony Patch (detects event) → ScreenStateTracker (tracks screen)
                                      ↓
                                HelpSystem (selects help context)
                                      ↓
Screen Handler → Text Reader (extracts data via reflection)
                                      ↓
                            ScreenReaderOutput (announces via Tolk)
```

### Module Overview

| Module | Purpose |
|--------|---------|
| `Core/` | Infrastructure: screen reader output, input handling, config, focus management, keywords |
| `Battle/` | Battle state readers (hand, floors, enemies, resources) + targeting systems |
| `Screens/` | Screen coordinators (MenuAccessibility, BattleAccessibility) |
| `Screens/Readers/` | Text extraction per screen type (static classes, pure functions) |
| `Patches/Screens/` | One Harmony patch per game screen transition (44 patches) |
| `Patches/Combat/` | One Harmony patch per combat event type (24 patches) |
| `Patches/` | Card event patches, card targeting, character state helper |
| `Help/Contexts/` | One help context per screen (25 contexts, priority-based) |
| `Utilities/` | Shared helpers: text processing, localization, UI visibility, reflection |

### Critical Rules

- **Never use `Speak(text, true)` (interrupt mode)** - it cuts off previous announcements. Always `Speak(text, false)` or `Speak(text)`.
- **Check `FloorTargetingSystem.IsTargeting` before announcing damage/deaths** - the game calculates preview damage when selecting floors; those shouldn't be announced.
- **Patches use `TryPatch()` pattern** with `AccessTools.TypeByName()` for runtime reflection - never `[HarmonyPatch]` attributes.
- **`GameScreen` enum requires `Help.GameScreen.X`** in Patches namespace due to `MonsterTrainAccessibility` being both a namespace and class name.
- **Always verify game API in `game/` source** before writing reflection code. Method names, parameter counts, and return types must match exactly.

### Key Components

**MenuAccessibility** (MonoBehaviour): Polls `EventSystem.current.currentSelectedGameObject`, dispatches to Reader classes via `GetTextFromGameObject()`. Keeps selection tracking, targeting activation, scroll/tutorial monitoring.

**BattleAccessibility** (plain class): Thin coordinator. Creates `BattleManagerCache` + readers (HandReader, FloorReader, ResourceReader, EnemyReader) on battle entry. Delegates all reading to them.

**BattleManagerCache**: Discovers and caches game managers (`CardManager`, `SaveManager`, `RoomManager`, `PlayerManager`, `CombatManager`) and their MethodInfo objects via reflection.

**Text Readers** (`Screens/Readers/`): Static classes that extract readable text from GameObjects. Each is a pure function: GameObject in, string out. The `GetTextFromGameObject()` dispatcher tries them in order: dialog, card, shop, battle intro, map, settings, compendium, clan selection, champion, relic, tooltip, event.

**Help System**: Priority-based context selection. Higher priority wins (0 = fallback, 110 = dialogs highest). Each context checks `ScreenStateTracker.CurrentScreen` and returns help text with available keyboard shortcuts.

**KeywordManager** (`Core/KeywordManager.cs`): Single source of truth for keyword definitions. Dynamically loads from the game at runtime via `StatusEffectManager.StatusIdToLocalizationExpression`, `CharacterTriggerData.TriggerToLocalizationExpression`, and card trait names. Has a fallback dictionary (~75 keywords) for resilience. Both `CardTextReader` and `HandReader` use `KeywordManager.GetKeywords()`.

### Card Text Reader Helpers

`CardTextReader` provides reusable static helpers for extracting card metadata via reflection:
- `GetRarityString(obj, type)` - `CollectableRarity` enum → localized string (filters Starter/Unset)
- `GetClanFromCardData(cardData, type)` - via `GetLinkedClass()` → `ClassData.GetTitle()`
- `GetUnitSubtype(cardState, type)` - via `CharacterData.GetLocalizedSubtype()` or `GetSubtypes()`
- `GetCardSize(cardState, type)` - via `CardState.GetSize()`
- `GetUnitStats(cardState, type)` - `GetTotalAttackDamage()` (int) + `GetHealth()` (float)
- `FormatCardDetails(cardState)` - full card announcement: "Name, Rarity Clan Type, subtype, size, cost. Description. Stats. Keywords."

### Utilities

- **`TextUtilities`**: `StripRichTextTags()` (used everywhere), `CleanSpriteTagsForSpeech()`, `IsPlaceholderText()`, `CleanAssetName()`
- **`LocalizationHelper`**: Cached `Localize()` via reflection (unified, replaces 3 former duplicate implementations), `TryLocalize()`, `ResolveEffectPlaceholders()`
- **`UITextHelper`**: Visibility checks (`IsActuallyVisible`, `IsHiddenByCanvasGroup`, `IsHiddenDialog`), `GetTextFromComponent()`, `FindChildRecursive()`, `PanelBlacklist`
- **`ReflectionHelper`**: `GetTypeFromAssemblies()`, `FindManager()`, safe field/property/method accessors

### Adding a New Screen

1. Create `Patches/Screens/XxxScreenPatch.cs` - follow ElixirDraftScreenPatch pattern
2. Add `GameScreen.Xxx` enum value to `Help/ScreenStateTracker.cs`
3. Create `Help/Contexts/XxxHelp.cs` implementing `IHelpContext`
4. Register patch in `MonsterTrainAccessibility.ApplyPatches()` and help context in `RegisterHelpContexts()`
5. If the screen needs special text extraction, create `Screens/Readers/XxxTextReader.cs` and add it to `MenuAccessibility.GetTextFromGameObject()`

### Hotkeys

**Global:** F1 (help), C (re-read), T (read all text), V (cycle verbosity)
**Battle:** H (hand), L (floors/levels), N (all units), R (resources)
**Floor Targeting:** Page Up/Down (cycle floors), Enter (confirm), Escape (cancel)

Note: F and E conflict with game's native shortcuts (F = Unit Details, E = End Turn).

## Game Specifics

### Key Types
```csharp
Team.Type.Monsters  // Player's units
Team.Type.Heroes    // Enemy units (confusing naming)
CardType.Monster / CardType.Spell / CardType.Blight
CollectableRarity: Common=0, Uncommon=1, Rare=2, Champion=3, Starter=4, Unset=-1
```

### Key Game API (CardState)
```csharp
GetTitle()                        // Localized card name
GetCardType()                     // CardType enum
GetRarity() / GetRarityType()     // CollectableRarity enum
GetCostWithoutAnyModifications()  // Base ember cost (int)
GetCost(stats, monsters, relics, room)  // Computed cost with modifiers
GetCardText(cardStats, saveManager, includeTraits)  // Effect text (NOT parameterless)
GetSpawnCharacterData()           // CharacterData? for unit cards
GetTotalAttackDamage()            // int (NOT GetAttackDamage)
GetHealth()                       // float (NOT int)
GetSize(ignoreTempUpgrade=false)  // int, clamped 1-6
GetTraitStates()                  // List of CardTraitState
GetCardDataRead()                 // CardData reference (NOT GetCardDataRead on all types)
```

### Key Game API (CharacterData)
```csharp
GetName()                  // Localized name
GetAttackDamage()          // int
GetHealth()                // int (unlike CardState which returns float)
GetSize()                  // int
GetLocalizedSubtype()      // First localized subtype or null
GetSubtypes()              // List<SubtypeData> via SubtypeManager
```

### Floor/Room Index Mapping
```
Room Index 0 = Floor 3 (Top)     roomIndex = 3 - userFloor
Room Index 1 = Floor 2 (Middle)
Room Index 2 = Floor 1 (Bottom)
Room Index 3 = Pyre Room
```

### Localization

Use `LocalizationHelper.TryLocalize(text)` or `LocalizationHelper.Localize(key)`. Best practice:
1. Try `GetName()`/`GetTitle()`/`GetDescription()` first - usually return localized text
2. If those return keys (contain `-` and `_`), localize with `LocalizationHelper.Localize()`
3. Fall back to type-name-based display names

### Text Extraction Dispatcher

`MenuAccessibility.GetTextFromGameObject()` tries extractors in order:
1. Dialog buttons, CardUI, shop items, battle intro, map nodes
2. Toggles, logbook items, clan selection, champion choices
3. Localized tooltip buttons, branch choices
4. `GetTextWithContext()` - handles short button labels (1-2 char = icon, use GameObject name)

`GetContextLabelFromHierarchy()` excluded container names: container, panel, holder, group, content, root, options, input area, section, buttons, layout, wrapper. Add to this list to fix unwanted "ParentName: X" announcements.

### Debugging

Log at `C:\Program Files (x86)\Steam\steamapps\common\Monster Train 2\BepInEx\LogOutput.log` shows:
- `Components on 'GameObjectName':` - component hierarchy
- `=== Fields on TypeName ===` - field dumps
- Text extraction results

```csharp
// Log all fields on an unknown component
foreach (var field in componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
{
    var val = field.GetValue(component);
    MonsterTrainAccessibility.LogInfo($"  {field.Name} = {val?.GetType().Name ?? "null"}");
}
```
