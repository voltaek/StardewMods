# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - TBD
#### Compatible with Stardew Valley v1.6.15 and SMAPI v4.1.10.

### Changed
- Rewrote honey sprite integration/compatibility.
	- Users can now choose the honey sprite to use from a new GMCM config option list.
	- Integrations are much more flexible now, including being able to add multiple options.
- Rewrote 'simple' example mod to match new integration method.
- Misc cleanup and reorganization.

### Added
- New 'intermediate' example mod to demonstrate the flexibility of the new integration method.

### Fixed
- Refresh the honey object data after running the undo honey colors console command and exiting the current save.

## [1.0.0] - 2025-01-18
#### Compatible with Stardew Valley v1.6.15 and SMAPI v4.1.10.

### Added
- Initial release.

[1.0.0]: https://github.com/voltaek/StardewMods/releases/tag/CHL-v1.0.0
[2.0.0]: https://github.com/voltaek/StardewMods/releases/tag/CHL-v2.0.0
