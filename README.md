# Monster Train Accessibility Mod

A comprehensive accessibility mod for Monster Train that enables totally blind players to fully enjoy the game through screen reader integration and complete keyboard navigation.

## Features

- **Full Screen Reader Support**: Works with NVDA, JAWS, Window-Eyes, and Windows Narrator (SAPI)
- **Complete Keyboard Navigation**: Navigate all game elements without a mouse
- **Battle Accessibility**: Read cards in hand, floor status, enemy info and intents
- **Menu Navigation**: Full access to main menu, settings, clan selection
- **Card Draft Support**: Browse and select cards with full descriptions
- **Map Navigation**: Choose your path through events, shops, and battles
- **Configurable Verbosity**: Choose how much detail you want announced
- **Braille Display Support**: Text sent to braille display if available

## Requirements

- Monster Train (Steam version)
- BepInEx 5.4.x mod loader
- A screen reader (NVDA recommended) or Windows Narrator
- Windows operating system

## Installation

1. Download the latest release zip from the [Releases page](https://github.com/ogomez92/monster_traina11y2/releases).
2. Extract the zip and copy **everything inside the `release` folder** directly into your Monster Train 2 game folder, replacing any files when prompted.
   - Default location: `C:\Program Files (x86)\Steam\steamapps\common\Monster Train 2`
3. Start your screen reader (NVDA, JAWS, or enable Windows Narrator).
4. Launch Monster Train 2. You should hear "Monster Train Accessibility loaded" when the game starts.

No mod loader subscription or in-game toggle is required — BepInEx is bundled and loads automatically via `winhttp.dll`.

## Keyboard Controls

### Navigation
| Key | Action |
|-----|--------|
| Arrow Keys | Navigate between items |
| Enter | Select / Activate current item |
| Space | Alternate select key |
| Escape | Go back / Cancel |

### Information Hotkeys
Most mod hotkeys are on F-keys so they never clash with the game's letter keys. F4 and F8 are left alone because the game uses them (Cheat menu and Feedback). R is used for resources since the game doesn't bind it.

| Key | Action |
|-----|--------|
| F1 | Context-sensitive help (shows available keys for current screen) |
| F5 | Re-read current focused item |
| F6 | Read all text on screen (patch notes, descriptions, etc.) |
| F11 | Cycle verbosity level (Minimal/Normal/Verbose) |

### Automatic Text Reading

The mod automatically reads text when:
- **Scroll views**: When you focus on a scrollable area (like patch notes), the content is read automatically
- **Dialogs/popups**: New dialogs, tooltips, and text panels are announced when they appear
- **Content changes**: If text content updates while you're focused on it, the new content is announced

### Battle Hotkeys
| Key | Action |
|-----|--------|
| F7 | Read all cards in hand |
| F2 | Read all floor information |
| F3 | Read enemy information and intents |
| R | Read resources (ember, gold, pyre health, pyre attack) |
| F12 | End turn (redundant alternative; game's native key still works) |
| O | Next ability (Pyre and unit abilities including champion) |
| P | Previous ability |
| Enter | Activate the selected ability |
| L | Game's card preview — also re-reads the selected card |
| N | Game's speed toggle — announces the new speed |

### Floor Targeting (when playing a card that requires floor selection)
| Key | Action |
|-----|--------|
| Page Up/Down | Cycle between floors |
| Enter | Confirm and play card on selected floor |
| Escape | Cancel card play |

## Monster Train Native Keyboard Shortcuts

These are the game's built-in keyboard shortcuts that work with or without the accessibility mod.

### Navigation & Floor Management

| Action | Keyboard Shortcut |
|--------|-------------------|
| Move Floor Up | `Page Up` / `W` / `Up Arrow` |
| Move Floor Down | `Page Down` / `S` / `Down Arrow` |
| View Top Floor | `Home` |
| View Bottom Floor | `End` |
| Zoom In / Out | `Scroll Wheel` |

### Combat & Card Actions

| Action | Keyboard Shortcut |
|--------|-------------------|
| End Turn | `F`
| Undo Action | `Z` / `Ctrl + Z` (Only works for certain non-random actions) |
| Speed Up Gameplay | `N` (Toggles between speeds during combat) |
| Toggle Unit Details | `E` (on a unit) |
| Show Draw Pile | `D` |
| Show Discard Pile | `G` |
| Show Exhaust/Consumed Pile | `X` |

### Menu & System

| Action | Keyboard Shortcut |
|--------|-------------------|
| Open Menu / Settings | `Esc` |
| Open Map | `M` |
| Full Screen Toggle | `Alt + Enter` |

### Note for Controller Users

If you are playing on a PC with a controller, the game has a "Keyboard Mode" that activates when you press a key. If your cursor behaves unexpectedly, check your input settings (Hybrid/Mouse/Controller modes in the options menu).

## Configuration

After first launch, a configuration file is created at:
`BepInEx/config/com.accessibility.monstertrain.cfg`

You can edit this file to customize:
- Key bindings for all controls
- Verbosity level (Minimal, Normal, Verbose)
- Which events to announce (card draws, damage, status effects)
- SAPI fallback settings
- Braille display options

### Verbosity Levels
- **Minimal**: Card names and numbers only
- **Normal**: Standard descriptions with key stats
- **Verbose**: Full details including flavor text

## Battle Navigation

The battle screen uses the game's native navigation. Use hotkeys for information:

- Press **F7** to hear all cards in your hand
- Press **F2** to hear floor status and units
- Press **F3** to hear enemy information
- Press **R** to hear ember, gold, pyre health, and pyre attack
- Press **F1** for context-sensitive help listing all available keys

### Playing Cards
- Navigate to cards using the game's controls
- Press **Enter** to play the selected card
- When a card requires floor placement (like monster cards), floor targeting mode activates:
  - Use **Page Up/Down** to cycle through floors (same as game's native floor navigation)
  - The current floor's units will be announced as you select
  - Press **Enter** to confirm and play the card
  - Press **Escape** to cancel

## Tips for Blind Players

1. **Press F1 for Help**: On any screen, press F1 to hear all available keyboard shortcuts for that context
2. **Start Simple**: Begin with the tutorial to learn the game flow
3. **Read Text**: Press T to read patch notes, event descriptions, or any screen text
4. **Use Battle Hotkeys**: F7 (hand), F2 (floors), F3 (enemies), R (resources) provide quick status updates during battle
5. **Check Ember**: Press R regularly to know your available resources
6. **Enemy Intents**: Press F3 to hear what enemies plan to do next turn
7. **Re-read Items**: Press F5 to re-read the currently focused menu item

## Troubleshooting

### No Speech Output
1. Verify your screen reader is running
2. Check that `Tolk.dll` is in the plugins folder
3. Try enabling SAPI fallback in the config file

### Mod Not Loading
1. Verify BepInEx is installed correctly
2. Check `BepInEx/LogOutput.log` for error messages
3. Ensure .NET Framework is installed

### Keys Not Responding
1. Make sure the game window has focus
2. Check for key conflicts in the config file
3. Try the alternate keys (Space instead of Enter)

## Known Limitations

- Some UI animations may cause brief delays in announcements
- Complex card effects may require verbose mode for full details
- Multiplayer modes have limited accessibility support

## Support

Report issues or request features at:
[GitHub Issues](https://github.com/yourusername/MonsterTrainAccessibility/issues)

## Credits

- **Tolk Library**: Davy Kager (screen reader integration)
- **BepInEx Team**: Mod loading framework
- **Trainworks**: Community modding toolkit reference
- **Shiny Shoe**: Monster Train developers

## License

This mod is provided free of charge for accessibility purposes.
Tolk library is licensed under LGPLv3.

---

*Making games accessible, one train ride at a time.*
