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

## Testing

No automated tests. Test by building, launching MT2, and checking:
- Log: `C:\Program Files (x86)\Steam\steamapps\common\Monster Train 2\BepInEx\LogOutput.log`
- Screen reader announcements with NVDA running

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
| `Patches/Screens/` | One Harmony patch per game screen transition (43 patches) |
| `Patches/Combat/` | One Harmony patch per combat event type (20 patches) |
| `Patches/` | Card event patches, card targeting, character state helper |
| `Help/Contexts/` | One help context per screen (25 contexts, priority-based) |
| `Utilities/` | Shared helpers: text processing, localization, UI visibility, reflection |

### Critical Rules

- **Never use `Speak(text, true)` (interrupt mode)** - it cuts off previous announcements. Always `Speak(text, false)` or `Speak(text)`.
- **Check `FloorTargetingSystem.IsTargeting` before announcing damage/deaths** - the game calculates preview damage when selecting floors; those shouldn't be announced.
- **Patches use `TryPatch()` pattern** with `AccessTools.TypeByName()` for runtime reflection - never `[HarmonyPatch]` attributes.
- **`GameScreen` enum requires `Help.GameScreen.X`** in Patches namespace due to `MonsterTrainAccessibility` being both a namespace and class name.

### Key Components

**MenuAccessibility** (MonoBehaviour): Polls `EventSystem.current.currentSelectedGameObject`, dispatches to Reader classes via `GetTextFromGameObject()`. Keeps selection tracking, targeting activation, scroll/tutorial monitoring.

**BattleAccessibility** (plain class): Thin coordinator. Creates `BattleManagerCache` + readers (HandReader, FloorReader, ResourceReader, EnemyReader) on battle entry. Delegates all reading to them.

**BattleManagerCache**: Discovers and caches game managers (`CardManager`, `SaveManager`, `RoomManager`, `PlayerManager`, `CombatManager`) and their MethodInfo objects via reflection.

**Text Readers** (`Screens/Readers/`): Static classes that extract readable text from GameObjects. Each is a pure function: GameObject in, string out. The `GetTextFromGameObject()` dispatcher tries them in order: dialog, card, shop, battle intro, map, settings, compendium, clan selection, champion, relic, tooltip, event.

**Help System**: Priority-based context selection. Higher priority wins. Each context checks `ScreenStateTracker.CurrentScreen` and returns help text with available keyboard shortcuts.

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
1. Try `GetName()`/`GetDescription()` first - usually return localized text
2. If those return keys (contain `-` and `_`), localize with `LocalizationHelper.Localize()`
3. Fall back to type-name-based display names

### Keyword Dictionaries

Keywords (status effects, card mechanics) are in:
- `Screens/Readers/CardTextReader.cs`: `ExtractKeywordsFromDescription()`
- `Battle/HandReader.cs`: `knownKeywords` dictionary

Add new keywords to BOTH locations. Format: `{ "KeywordName", "KeywordName: Brief explanation" }`

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
