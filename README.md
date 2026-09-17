# Oracle

[日本語](docs/README.ja.md)

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

- Set up a timeline of which skills to use and when
- Import your skills from an FFLogs report
- Record a pull in-game and turn it into a timeline
- Automatically load a timeline that matches the duty when it starts
- See upcoming skills as a list or a scrolling row of icons
- Light up the skill on your hotbar when it's time to use it

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
| `/oracle preview pause` | Pause or resume preview |
| `/oracle preview stop` | Stop preview |

## For developers

1. Build: `dotnet build Oracle.sln -c Release -p:Platform=x64`
2. Point Dalamud’s **dev plugin** path at `Oracle/bin/Release/`
3. Enable **Oracle** in the plugin installer (dev)

[MirageUI](https://github.com/exatrines/MirageUI) is included as a git submodule for the shared UI kit.

## License

[AGPL-3.0-or-later](LICENSE)
