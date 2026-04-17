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

**Releasing**: Push a `v*.*.*` tag to trigger the GitHub Actions workflow that zips `release/` and creates a GitHub Release. The workflow also regenerates `release/readme.html` from `README.md` via pandoc.

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
| `Patches/Combat/` | One Harmony patch per combat event type (42 patches) |
| `Patches/` | Card event patches (draw/play/discard/exhaust/shuffle/freeze), card targeting, character state helper |
| `Help/Contexts/` | One help context per screen (25 contexts, priority-based) |
| `Utilities/` | Shared helpers: text processing, localization, UI visibility, reflection |

### Critical Rules

- **Never use `Speak(text, true)` (interrupt mode)** - it cuts off previous announcements. Always `Speak(text, false)` or `Speak(text)`.
- **Check `FloorTargetingSystem.IsTargeting` before announcing damage/deaths** - the game calculates preview damage when selecting floors; those shouldn't be announced.
- **Patches use `TryPatch()` pattern** with `AccessTools.TypeByName()` for runtime reflection - never `[HarmonyPatch]` attributes.
- **`GameScreen` enum requires `Help.GameScreen.X`** in Patches namespace due to `MonsterTrainAccessibility` being both a namespace and class name.
- **Always verify game API in `game/` source** before writing reflection code. Method names, parameter counts, and return types must match exactly.
- **`CardData` has no `GetCardText`/`GetDescription` — only `CardState` does.** To format text for a raw `CardData` (e.g. a unit's ability card), wrap it with `new CardState(cardData, null)` first and call `GetCardText` on the state. `CardData.GetOverrideDescriptionKey()` plus localization is an acceptable fallback.
- **Prefer game API methods over field scanning by name substring.** `CardUI` has `cardCanvas`, `cardFront`, `cardBack`, `cardUIContainer`, `currentCardState` — a `.Contains("card")` lambda grabs the wrong one. Call the public `GetCardState()`/`GetCardData()` methods where available, and if you must scan fields, match exact or suffix (`EndsWith("CardState")`) and verify the runtime type.
- **`ScreenReaderOutput.Speak` applies a global placeholder filter.** Known game-side error placeholders (`"This should be ... something went wrong."`) get stripped before reaching Tolk. If a new placeholder surfaces, extend `StripGamePlaceholders` in `Core/ScreenReaderOutput.cs`, but also fix the source reader.

### Key Components

**MenuAccessibility** (MonoBehaviour): Polls `EventSystem.current.currentSelectedGameObject`, dispatches to Reader classes via `GetTextFromGameObject()`. Keeps selection tracking, targeting activation, scroll/tutorial monitoring.

**BattleAccessibility** (plain class): Thin coordinator. Creates `BattleManagerCache` + readers (HandReader, FloorReader, ResourceReader, EnemyReader) on battle entry. Delegates all reading to them.

**BattleManagerCache**: Discovers and caches game managers (`CardManager`, `SaveManager`, `RoomManager`, `PlayerManager`, `CombatManager`) and their MethodInfo objects via reflection.

**Text Readers** (`Screens/Readers/`): Static classes that extract readable text from GameObjects. Each is a pure function: GameObject in, string out. The `GetTextFromGameObject()` dispatcher tries them in order: dialog, card, shop, battle intro, map, settings, compendium, clan selection, champion, relic, tooltip, event.

**Help System**: Priority-based context selection. Higher priority wins (0 = fallback, 110 = dialogs highest). Each context checks `ScreenStateTracker.CurrentScreen` and returns help text with available keyboard shortcuts.

**KeywordManager** (`Core/KeywordManager.cs`): Single source of truth for keyword definitions. Dynamically loads from the game at runtime via `StatusEffectManager.StatusIdToLocalizationExpression`, `CharacterTriggerData.TriggerToLocalizationExpression`, and card trait names. Has a fallback dictionary (~66 keywords) for resilience. Both `CardTextReader` and `HandReader` use `KeywordManager.GetKeywords()`.

### Combat Event Coverage

**Patched (42 combat patches + 7 card event patches):**

| Category | Patches |
|----------|---------|
| Battle lifecycle | BattleVictoryPatch (win/loss), CombatPhaseChangePatch (phase transitions), WaveStartPatch, AllEnemiesDefeatedPatch |
| Unit lifecycle | UnitSpawnPatch, SpawnPointChangedPatch, UnitDeathPatch, CharacterMovementPatch (ascend/descend) |
| Damage/healing | UpdateHpPatch (HP changes + death), CharacterDamagePatch, DamageAppliedPatch, HealAppliedPatch |
| Stat changes | AttackBuffPatch, AttackDebuffPatch, MaxHPBuffPatch, MaxHPDebuffPatch |
| Status effects | StatusEffectPatch (applied), StatusEffectRemovedPatch |
| Equipment | EquipmentPatch (add/remove) |
| Abilities | TriggerAbilityPatch (slay/incant/etc), MoonPhasePatch (Luna shifts) |
| Pyre | PyreDamagePatch, PyreHealPatch |
| Cards | CardDrawPatch, CardPlayedPatch, CardDiscardedPatch, CardExhaustedPatch, DeckShuffledPatch, CardFreezePatch, CardSelectionErrorPatch |
| Relics | RelicTriggeredPatch |
| UI | EnemyDialoguePatch (speech bubbles), RoomSelectionPatch (floor browsing), PreviewModeDetector (filters preview-mode noise) |
| Targeting | CardTargetingPatches (target selection navigation + mode start) |

| Energy/resources | EnergyModifiedPatch (next turn/every turn), DrawCountModifiedPatch, PyreArmorPatch |
| Upgrades | CardUpgradeAppliedPatch (stat changes from upgrades) |
| Triggers | TriggerAddedPatch, TriggerRemovedPatch (ability gain/loss) |
| Sacrifice | SacrificePatch (distinct from regular death) |
| HP debuff | DebuffHPPatch (direct HP reduction, not damage) |
| Specific draws | DrawSpecificCardPatch, DiscardHandPatch |

### Status Effects & Keywords

**Status effects** (86 in game via `StatusEffectManager`): All discoverable at runtime. The fallback dictionary covers ~30 common ones as safety net. Key IDs:
- **Buffs**: armor, rage, regen, damageshield, lifesteal, spikes, stealth, spellshield, soul, haste, sweep, trample, multistrike, endless, relentless, piercing
- **Debuffs**: poison (frostbite), sap, dazed, rooted, emberdrain, heartless, melee weakness, spell weakness, reap, silenced, fragile, inert, decay
- **MT2-specific**: valor, avarice, titanskin, titanite, unstable, horde, undying, timebomb, conduit, blastresistant, equalizer, mageblade, redcrown, lifelink, pyrebane, pyrebound, duality, sniper, mass, purify, pyrestain, pyregel, fixedattack
- **Moon**: fullmoon, newmoon (Luna clan)
- **Internal**: unit_ability, unit_ability_available, cooldown, corrupt_poison, corrupt_regen, shard_upgrade, pyrelock, unit_grafted_equipment

**Triggers** (70 in `CharacterTriggerData.Trigger` enum): Runtime-discovered via `TriggerToLocalizationExpression`. Fallback only covers 16 user-friendly names (Slay, Revenge, Strike, Extinguish, Summon, Incant, Resolve, Rally, Harvest, Gorge, Inspire, Rejuvenate, Action, Hatch, Hunger, Armored). Notable triggers without fallback names:
- OnDeath, OnSpawn, OnAttacking, OnHeal, OnTeamTurnBegin, OnHit, OnFeed, OnEaten, OnBurnout, OnShift
- CardSpellPlayed, CardMonsterPlayed, CardCorruptPlayed, CardRegalPlayed, CardExhausted
- OnEquipmentAdded/Removed, OnDeathwish, OnValiant, OnTimebomb, OnReanimated
- OnMoonPhaseShift, OnMoonLit, OnMoonShade, OnTroopAdded/Removed, OnSilence/Lost

**Card traits** (71 trait classes in game): Only 19 have hardcoded localization keys in KeywordManager. Runtime discovery handles the rest via trait class name → localization key mapping. Missing from fallback: Juice, Ephemeral, CorruptState, Infusion, MagneticState, Heavy, GraftedEquipment, DamageOverflow, SpellAffinity, Treasure, and ~40 scaling/specialized traits.

### Card Text Reader Helpers

`CardTextReader` provides reusable static helpers for extracting card metadata via reflection:
- `GetRarityString(obj, type)` - `CollectableRarity` enum → localized string (filters Starter/Unset)
- `GetClanFromCardData(cardData, type)` - via `GetLinkedClass()` → `ClassData.GetTitle()`
- `GetUnitSubtype(cardState, type)` - via `CharacterData.GetLocalizedSubtype()` or `GetSubtypes()`
- `GetCardSize(cardState, type)` - via `CardState.GetSize()`
- `GetUnitStats(cardState, type)` - `GetTotalAttackDamage()` (int) + `GetHealth()` (float)
- `GetUnitAbilityDescription(cardState, type)` - via `CharacterData.GetUnitAbilityCardData()` → `GetCardText()` for ability effect text
- `IsCardUpgraded(cardState, type)` - checks `cardModifiers.GetCardUpgrades().Count > 0`
- `FormatCardDetails(cardState)` - full card announcement: "[Upgraded] Name, Rarity Clan Type, subtype, size, cost. Description. Stats. Ability description. Keywords."

### Utilities

- **`TextUtilities`**: `StripRichTextTags()` (used everywhere), `CleanSpriteTagsForSpeech()`, `IsPlaceholderText()`, `CleanAssetName()`
- **`LocalizationHelper`**: Cached `Localize()` via reflection (unified, replaces 3 former duplicate implementations), `TryLocalize()`, `ResolveEffectPlaceholders()`
- **`ModLocalization`**: Game-concept terms (`Ember`, `Gold`, `Pyre`, floor names, pile names), combat event formatters, `ModTerm(key)` for mod-owned translations (see below)
- **`UITextHelper`**: Visibility checks (`IsActuallyVisible`, `IsHiddenByCanvasGroup`, `IsHiddenDialog`), `GetTextFromComponent()`, `FindChildRecursive()`, `PanelBlacklist`
- **`ReflectionHelper`**: `GetTypeFromAssemblies()`, `FindManager()`, safe field/property/method accessors

### Mod-Owned Translations

`ModLocalization._modTranslations` is a per-language dictionary for terms the game doesn't provide localization keys for. Use `ModLocalization.ModTerm("key")` to get the term in the current game language, falling back to English.

Currently supported languages: English, Spanish, French, German, Portuguese (Brazil), Russian, Simplified Chinese, Japanese, Korean. Language detected at runtime via `I2.Loc.LocalizationManager.CurrentLanguage`.

To add a new translated term: add the key to each language's dictionary in `_modTranslations`. To add a new language: add a new dictionary entry keyed by the I2.Loc language name.

### Adding a New Screen

1. Create `Patches/Screens/XxxScreenPatch.cs` - follow ElixirDraftScreenPatch pattern
2. Add `GameScreen.Xxx` enum value to `Help/ScreenStateTracker.cs`
3. Create `Help/Contexts/XxxHelp.cs` implementing `IHelpContext`
4. Register patch in `MonsterTrainAccessibility.ApplyPatches()` and help context in `RegisterHelpContexts()`
5. If the screen needs special text extraction, create `Screens/Readers/XxxTextReader.cs` and add it to `MenuAccessibility.GetTextFromGameObject()`

### Hotkeys

**Global:** F1 (help), F5 (re-read), F6 (read all text), F11 (cycle verbosity)
**Battle:** F7 (hand), F2 (floors), F3 (enemies), R (resources), F12 (end turn)
**Abilities:** O (cycle abilities), P (activate)
**Floor Targeting:** Page Up/Down (cycle floors), Enter (confirm), Escape (cancel)

Note: Old letter-key bindings (C, T, V, H, L, N) are auto-migrated to F-keys via `AccessibilityConfig`. The game's native keys L (card preview) and N (speed toggle) are unaffected.

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
Room Index 0 = Floor 1 (Bottom)  roomIndex = userFloor - 1
Room Index 1 = Floor 2 (Middle)
Room Index 2 = Floor 3 (Top)
Room Index 3 = Pyre Room
```

### Localization

Use `LocalizationHelper.TryLocalize(text)` or `LocalizationHelper.Localize(key)`. Best practice:
1. Try `GetName()`/`GetTitle()`/`GetDescription()` first - usually return localized text
2. If those return keys (contain `-` and `_`), localize with `LocalizationHelper.Localize()`
3. Fall back to type-name-based display names
4. For map nodes, use `GetTooltipTitle()` / `GetTooltipBody()` on `MapNodeData` - these localize internally
5. **Always clean sprite tags** from localized text with `TextUtilities.CleanSpriteTagsForSpeech()` - game format strings like `GoldFormat` embed `<sprite name="Gold">` tags
6. For mod-specific terms not in the game, use `ModLocalization.ModTerm("key")`

### Text Extraction Dispatcher

`MenuAccessibility.GetTextFromGameObject()` tries extractors in order:
1. Scrollbar, RunOpeningScreen, dialog buttons, upgrade choices, CardUI, shop items
2. Enemy previews, battle intro, relics, Dragon's Hoard rewards, reward items, map nodes
3. Settings, toggles, compendium, clan selection, pyre hearts, champion choices
4. Localized tooltip buttons, branch choices, tooltip providers
5. `GetTextWithContext()` - handles short button labels (1-2 char = icon, use GameObject name)

`GetContextLabelFromHierarchy()` excluded container names: container, panel, holder, group, content, root, options, input area, section, buttons, layout, wrapper. Add to this list to fix unwanted "ParentName: X" announcements.

### Map Screen Structure

Map nodes use two different component systems:
- **`MapNodeUI`** (main map screen): Has `data` field (MapNodeData subclass) and `tooltipProvider`. Use `GetData().GetTooltipTitle()` for localized name. Field is named `data` (NOT `mapNodeData`). Also has `GetMapNodeDataName()` but that returns the **asset name** (e.g. "SpellUpgradeMerchant"), not the localized title.
- **`MinimapNodeMarker`** (minimap): Has `mapNodeData` field. The `tooltipProvider` only receives the body (title is null); read `mapNodeData.GetTooltipTitle()` for the title, or the `label` TMP_Text field.
- **`MapBattleNodeUI`**: Has `defaultTooltipTitle` localization key (e.g. "ScreenMap_Battle_TooltipTitle").
- Branch choices: `BranchChoiceUI/Left button` and `BranchChoiceUI/Right button` under `MapSection_XX`. Scan sibling `MapNodeUI` nodes named "Left node N", "Right node N", "Shared node N" to list all stops on each branch.

### Shop Item Structure

`MerchantGoodDetailsUI` → `rewardUI` (RewardDetailsUI) → contains `relicUI` (RelicInfoUI), `cardUI` (CardUI), `sinRelicUI` (RelicInfoUI), `genericRewardUI` (RewardIconUI). For enhancers/upgrade stones, the `RewardData` backing field is `EnhancerRewardData` but the visible text is on `RelicInfoUI.titleLabel` and `RelicInfoUI.descriptionLabel` TMP components (the `relicData` backing field is null). Read the UI labels directly.

**Critical: `RewardDetailsUI` keeps all sub-UIs alive and only populates the one matching the reward type.** Inactive sub-UIs retain stale default labels — in particular, `RelicInfoUI` ships with the placeholder `"This should be the blessing description, but something went wrong."` on its TMP label asset. For card rewards, always detect the reward type first via the `<RewardData>k__BackingField` (e.g. `CardRewardData`) and route directly to `cardUI`, bypassing `relicUI` entirely. `ReadRelicInfoUILabels` also detects broken placeholder text and returns null so callers can fall through.

### TMP Sprite Name Resolution

Card text often contains `<sprite name="Health">`, `<sprite name="Attack">`, etc. `LocalizationHelper.GetSpriteDisplayName` maps these to readable words. For stat/resource icons (Health, Attack, Size, Ember, MagicPower, Gold, Xcost, Capacity, DragonsHoard) a hardcoded dictionary wins before falling back to fuzzy I2.Loc term search — fuzzy substring matching often returns unrelated terms (e.g. "Health" → some "Consumer" subtype term). Add new stat icon names to the dictionary rather than relying on term search.

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
