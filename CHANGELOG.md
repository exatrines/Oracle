# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

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

[Unreleased]: https://github.com/exatrines/Oracle/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/exatrines/Oracle/releases/tag/v1.0.1
[1.0.0]: https://github.com/exatrines/Oracle/releases/tag/v1.0.0
