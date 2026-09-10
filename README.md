# Oracle

[日本語](README.ja.md)

![Major overlay and hotbar icon highlight](docs/screenshots/major-hotbar-highlight-730x380.png)

Oracle is a Dalamud plugin that shows duty timeline cues—so you know which skill to use, and when.

A main way to build timelines is importing casts from **FFLogs**. You can also **record your own actions** in selected duties (AutoRecord) and turn that into a timeline. Match zone and job, optionally an Auto Load boss preset; when countdown or combat starts, Oracle runs a clock and surfaces upcoming actions on overlays, with optional hotbar icon highlights.

## Install

1. Run `/xlsettings` and open the **Experimental** tab
2. Add this URL under **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

3. Run `/xlplugins` and install **Oracle**

## Features

- **Timeline editor** — cues by time offset (actions, memos, or sync), zone / job auto-load (optional boss DataID preset), manual load commands
- **Overlays** — list overlay (upcoming actions) and major overlay (scrolling icon lane); memos and sync stay in the editor
- **Action highlight** — before / after windows with optional blink; optional hotbar highlight
- **FFLogs import** — player casts from a report, optional enemy-attack memos, and Import sync when the zone has presets (API credentials in settings)
- **AutoRecord** — record your actions and enemy cast/status events in selected duties, then import into a timeline
- **Timer Sync Presets** — per-zone enemy casts and statuses that resync the clock; used by Import sync
- **Phase Presets** — per-zone boss DataID sets used out of combat. Empty Phase = Any (zone / job only). Built-in DSR P2 and M12S P2; stored override until Reset to default. Older scene-split files become Any; more than one Any for the same zone and job will not auto-load
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
