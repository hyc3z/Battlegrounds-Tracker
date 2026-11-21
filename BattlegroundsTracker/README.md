# Battlegrounds Tracker

A Battlegrounds-only fork of [Hearthstone Deck Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker).
The deck tracker, statistics, arena, mercenaries, replay, plugin and update infrastructure of the
upstream project have been removed; what remains is the Hearthstone game state reader and the
Battlegrounds overlays.

## Features

- **Bob's Buddy**: combat simulations (win/tie/loss rates and damage distribution) using the same
  simulator (`lib/BobsBuddy.dll`) as the upstream tracker.
- **Battlegrounds overlay**
  - Hero picking stats (HSReplay.net / Tier7 account required for some panels), hero power picking.
  - Quest, trinket and anomaly guides, composition guides and the composition tab.
  - Tavern minion browser with tier/tribe filters, tier list and triples counter.
  - Opponent info, leaderboard hover information and placement distribution.
  - Tavern markers / minion pinning and the inspiration panel.
  - Session recap window and in-game session panel.
  - Chinese client module (HDTTools integration).
- **Game integration**: reads the Hearthstone log through the original HDT log reader; HearthMirror
  (via HearthWatcher) is used for game/scene state.
- Tray icon, overlay hotkeys and a settings window with the Battlegrounds overlay options.

## Screenshots

Main window:

![Main window](Docs/main-window.png)

Battlegrounds overlay options:

![Battlegrounds options](Docs/bg-options.png)

## Removed from the upstream project

- Deck manager, deck importing/exporting and collection syncing.
- Constructed/arena/mercenaries tracking, mulligan guides and secrets tracking.
- Statistics, replays, HSReplay.net uploading and the HSReplay.net account page (only the minimal
  OAuth client used by the Battlegrounds Tier7 features is kept).
- Plugin system, auto updater (Squirrel / Velopack), Sentry crash reporting and telemetry.
- Translations (English only) and the unit test projects / CI workflows.

A few small compatibility types for the remaining (never populated) constructed data model are kept
so the internal game-model code keeps compiling; they do not appear in the UI and do not collect,
store or send any data.

## Requirements

- Windows (x64).
- .NET Framework 4.7.2 (or newer).
- Hearthstone, with the log configuration the tracker writes automatically.

## Build

```powershell
.\build.ps1
```

The script locates the .NET SDK (for `Microsoft.NET.Sdk.WindowsDesktop`) and the Visual Studio Build
Tools MSBuild automatically and sets the environment variables the SDK-style WPF project needs.
The output is written to `bin\x64\<Configuration>\BattlegroundsTracker.exe`.

If PowerShell scripts are disabled on your machine, run the wrapper instead:

```cmd
build.cmd
```

Notes:

- The project builds with the .NET Framework MSBuild and a .NET SDK that provides
  `Microsoft.NET.Sdk.WindowsDesktop`; a full Visual Studio installation is not required.
- If the build fails with `MSB3821` (mark of the web on `Properties/Resources.resx`), either unblock
  the repository files or set
  `HKLM\SOFTWARE\Microsoft\.NETFramework\SDK\AllowProcessOfUntrustedResourceFiles` to `true`.

## Run

Start `bin\x64\Debug\BattlegroundsTracker.exe`. The application lives in the tray; the main window
contains the overlay settings. The overlay only shows while Hearthstone is running.

Settings and logs are stored in `%APPDATA%\BattlegroundsTracker\`. The tracker uses its own directory,
so it can run next to an installed Hearthstone Deck Tracker without sharing its configuration. If the
AppData directory is not writable (portable or restricted environment), the tracker falls back to
storing `config.xml` and `Logs\` next to the executable.

An HSReplay.net account is optional: it is only needed for the Tier7 Battlegrounds features. Log in
from the HSReplay.net account section of the main window.

## Layout

| Path | Contents |
| --- | --- |
| `Hearthstone/` | Game model (entities, players, cards, Battlegrounds state) |
| `LogReader/` | Hearthstone log parser |
| `Live/` | Battlegrounds board state watcher |
| `BobsBuddy/` | Bob's Buddy integration |
| `Controls/Overlay/Battlegrounds/` | Battlegrounds overlay views and view models |
| `Utility/Battlegrounds/` | Battlegrounds helper services (minion DB, China module) |
| `Windows/` | Main window, overlay window and the Battlegrounds windows |

## License

Copyright © [HearthSim](https://hearthsim.net). All Rights Reserved. See the repository root for the
original license and third-party notices.
