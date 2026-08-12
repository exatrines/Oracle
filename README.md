# Oracle

[日本語](README.ja.md)

![Major overlay and hotbar icon highlight](docs/screenshots/major-hotbar-highlight-730x380.png)

Oracle is a Dalamud plugin that shows duty timeline cues—so you know which skill to use, and when.

A main way to build timelines is importing casts from **FFLogs**. You can also **record your own actions** in selected duties (AutoRecord) and turn that into a timeline. Match zone and job; when countdown or combat starts, Oracle runs a clock and surfaces upcoming actions on overlays, with optional hotbar icon highlights.

Scene-based Auto Load and Scene Transition cues are **experimental** (still under testing).

## Install

1. Run `/xlsettings` and open the **Experimental** tab
2. Add this URL under **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

3. Run `/xlplugins` and install **Oracle**

## Features

- **Timeline editor** — cues by time offset (actions, memos, or experimental scene transitions), zone / job auto-load (optional experimental scene filter), manual load commands
- **Overlays** — list overlay (upcoming rows) and major overlay (scrolling icon lane)
- **Action highlight** — before / after windows with optional blink; optional hotbar highlight
- **FFLogs import** — pull casts from a report into a new timeline (API credentials in settings)
- **AutoRecord** — record your actions in selected duties, then import into a timeline
- **i18n** — English and Japanese UI strings

## Commands

| Command | Description |
| --- | --- |
| `/oracle` | Toggle timeline settings |
| `/oracle config` | Toggle plugin settings |
| `/oracle overlay timeline` | Toggle timeline overlay |
| `/oracle overlay major` | Toggle major overlay |
| `/oracle overlay icon` | Toggle icon highlight |
| `/oracle autorecord` | Toggle AutoRecord enabled |
| `/oracle load <name>` | Load a timeline |
| `/oracle unload` | Unload the timeline |
| `/oracle preview start [sec]` | Start preview countdown (default 21) |
| `/oracle preview stop` | Stop preview |

## For developers

1. Build: `dotnet build Oracle.sln -c Release -p:Platform=x64`
2. Point Dalamud’s **dev plugin** path at `Oracle/bin/Release/`
3. Enable **Oracle** in the plugin installer (dev)

[MirageUI](https://github.com/exatrines/MirageUI) is included as a git submodule for the shared UI kit.

## License

[AGPL-3.0-or-later](LICENSE)
