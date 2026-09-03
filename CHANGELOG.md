# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.1.0] - 2026-09-03

### Added

- **Sync** cues: resync the running clock to an enemy cast start or complete, or to a status apply or remove. Each cue uses one edge (start or complete / apply or remove)
- **Content Sync Presets** (Settings → Import): built-in rows for Dragonsong's Reprise, The Omega Protocol, Futures Rewritten, and Dancing Mad. A stored override hides the built-in list until Reset to default
- FFLogs and AutoRecord import: **Import sync** when the selected zone has presets (shown only then, on by default). Imports the first matching event per preset row
- Cue table times as `mm:ss.mmm` (stored and synced at 0.1s)
- **Plugin log** (settings): optional Dalamud log of countdown, combat, enemy casts, status, action effects, and scene changes
- AutoRecord records enemy cast start, cast complete (ActionEffect, same filter as the live clock), and non-friendly status apply/remove, in addition to your actions

### Changed

- Timeline, Major, and hotbar overlays show **Action** cues only. Memos and sync stay in the editor
- Import: enemy-attack memos stay a separate checkbox; cast and status sync are one **Import sync** control

### Removed

- Experimental **Scene Transition** cue type. Those rows (and retired BossHpZero) are dropped when a timeline file is loaded. Auto Load scene filter is unchanged

## [1.0.6] - 2026-08-31

### Added

- FFLogs import: optional **Import enemy attacks as memos** (off by default). Party damage taken from enemies is clustered per ability (1s gap); auto-attacks and ticks are skipped. Independent of player-cast import
- Memo names come from FFLogs in the plugin UI language (English or Japanese). Pasted report URLs may use any host (`www`, `ja`, `cn`, …)

## [1.0.5] - 2026-08-31

### Fixed

- AutoRecord no longer stores a cue target for party-wide / AoE actions (for example area heals). A job is recorded only when exactly one other player was hit

## [1.0.4] - 2026-08-28

### Added

- Cue table: right-click a row to **Insert Row Above** / **Insert Row Below** (empty action, same time)
- Action cues can store a **target** (job or role). AutoRecord and FFLogs import fill the job from the party member who was hit; the editor picker defaults to none
- Timeline overlay shows the target icon next to the action; Major overlay draws a role-colored frame and a job/role badge on the action icon
- Shared **Complete window** setting (Highlight Setting): a used action hides a matching cue in this window (default 10s). Timeline, Major, and Hotbar share it

### Changed

- Cue table time edits apply only on confirm (Enter or the check control), not while typing
- Timeline overlay **Lookahead** is list display only; Major uses Before/After; hotbar highlight uses the highlight duration

## [1.0.3] - 2026-08-12

### Added

- Experimental **Scene Transition** cue type (Before → After); on that scene edge, resync the running clock to the cue time without leaving combat
- Auto Load **scene filter** checkbox (unchecked = any scene; checked = match SceneId, including `0` as a valid scene)
- AutoRecord overlay shows the live scene next to the content name

### Changed

- Auto Load no longer switches timelines while the clock is running or while in combat; scene-based selection applies out of combat only
- README: top screenshot, clearer `/xlsettings` / `/xlplugins` install steps, and a note that scene features are experimental

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

[Unreleased]: https://github.com/exatrines/Oracle/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/exatrines/Oracle/compare/v1.0.6...v1.1.0
[1.0.6]: https://github.com/exatrines/Oracle/releases/tag/v1.0.6
[1.0.5]: https://github.com/exatrines/Oracle/releases/tag/v1.0.5
[1.0.4]: https://github.com/exatrines/Oracle/releases/tag/v1.0.4
[1.0.3]: https://github.com/exatrines/Oracle/releases/tag/v1.0.3
[1.0.2]: https://github.com/exatrines/Oracle/releases/tag/v1.0.2
[1.0.1]: https://github.com/exatrines/Oracle/releases/tag/v1.0.1
[1.0.0]: https://github.com/exatrines/Oracle/releases/tag/v1.0.0
