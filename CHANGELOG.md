# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.2] - 2026-08-12

### Added

- `/oracle unload` to clear the loaded timeline
- `/oracle overlay major` and `/oracle overlay icon` toggles
- FFLogs API setup guide note: Public Client must be off (unchecked)
- Japanese README (`README.ja.md`) with cross-links

### Changed

- Chat commands reworked to a smaller surface:
  - `/oracle` — timeline settings
  - `/oracle config` — plugin settings
  - `/oracle overlay timeline|major|icon`
  - `/oracle autorecord` — enable/disable AutoRecord
  - `/oracle load <name>` / `/oracle unload`
  - `/oracle preview start [sec]` / `/oracle preview stop`
- `/oracle preview start` defaults to a 21s countdown (replaces the old fixed −20s preview offset)
- Dalamud command help lists each subcommand on its own line
- Plugin Punchline / Description updated; removed the `tank` tag (all roles)
- README: custom plugin repo install first, Screenshots before For developers, license link only (no extra license blurb or disclaimer)

### Removed

- `/or` command alias
- Legacy commands: `help`, `countdown` / `cd`, `reset`, `s` / `setting(s)`, bare `overlay`, AutoRecord overlay toggle via chat, and `/oracle setting …` shortcuts

## [1.0.1] - 2026-08-12

### Changed

- Major overlay defaults: before 8s, after 3s, 35 px/sec (icon size remains 32)

## [1.0.0] - 2026-08-11

### Added

- Timeline documents with zone / job / SceneId auto-load and manual load commands
- Timeline editor (cue table: time, action or memo, reorder / copy / paste)
- Timeline list overlay and major (scrolling) overlay
- Shared action highlight (before / after) and optional hotbar icon highlight
- FFLogs report import (OAuth client settings + per-job default import actions)
- AutoRecord (zone-filtered combat recording, overlay, import into timelines)
- Configurable UI language (Follow Dalamud / English / Japanese)
- English and Japanese UI strings (`Data/I18n`)
- MirageUI-based settings and editor shell

### Changed

- Renamed the plugin from ForeCast to **Oracle** (commands: `/oracle`, `/or`)

[Unreleased]: https://github.com/exatrines/Oracle/compare/v1.0.2...HEAD
[1.0.2]: https://github.com/exatrines/Oracle/releases/tag/v1.0.2
[1.0.1]: https://github.com/exatrines/Oracle/releases/tag/v1.0.1
[1.0.0]: https://github.com/exatrines/Oracle/releases/tag/v1.0.0
