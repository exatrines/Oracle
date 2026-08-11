# Oracle

[日本語](README.ja.md)

Dalamud plugin that shows **duty timeline cues** for mitigation and skill timing.

Match a timeline to zone, job, and scene. When the countdown hits (or combat starts), Oracle runs a clock and surfaces upcoming actions on overlays and optional hotbar highlights.

## Install

Add this URL as a **custom plugin repository** in Dalamud (Settings → Experimental):

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

Then install **Oracle** from the plugin installer.

## Features

- **Timeline editor** — cues by time offset (actions or memos), zone / job / SceneId auto-load, manual load commands
- **Overlays** — list overlay (upcoming rows) and major overlay (scrolling icon lane)
- **Action highlight** — before / after windows with optional blink; optional hotbar highlight
- **FFLogs import** — pull casts from a report into a new timeline (API credentials in settings)
- **AutoRecord** — record your actions in selected duties, then import into a timeline
- **i18n** — English and Japanese UI strings

## Commands

| Command | Description |
| --- | --- |
| `/oracle` / `/or` | Toggle the timeline window |
| `/oracle s` | Toggle plugin settings |
| `/oracle overlay` | Toggle the timeline list overlay |
| `/oracle ar` | Toggle the AutoRecord overlay |
| `/oracle load <name>` | Load a timeline by name |
| `/oracle countdown <sec>` | Inject a test countdown |
| `/oracle preview` / `preview stop` | Preview a timeline without combat |
| `/oracle reset` | Reset the clock |
| `/oracle help` | Print help |

AutoRecord-related settings toggles are also available under `/oracle setting …` (see in-game help).

## Screenshots

![Major overlay and hotbar icon highlight](docs/screenshots/major-hotbar-highlight-730x380.png)

## For developers

1. Build: `dotnet build Oracle.sln -c Release -p:Platform=x64`
2. Point Dalamud’s **dev plugin** path at `Oracle/bin/Release/`
3. Enable **Oracle** in the plugin installer (dev)

[MirageUI](https://github.com/exatrines/MirageUI) is included as a git submodule for the shared UI kit.

## License

[AGPL-3.0-or-later](LICENSE)
