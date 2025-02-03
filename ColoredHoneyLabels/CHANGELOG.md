# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2025-02-02
#### Compatible with Stardew Valley v1.6.15 and SMAPI v4.1.10.

### Added
- 4 new honey sprite options now built in, using the default honey sprite. In addition to the existing "Full Label" default option, there is now:
	- **Mini Label** Just the center 4 pixels of the label are colored to replace the default yellow and orange pixels.
	- **Full Label + Lid** The fully-colored label (same as the default option), plus the lid is colored, too!
	- **Mini Label + Lid** Same as the 'Mini Label' option, plus the lid is colored, too!
	- **Lid Only** The label stays completely standard, but the lid is colored.
- Users can now choose the honey sprite to use from a new 'Honey Sprite' GMCM config option list.
- New 'Intermediate' example mod to demonstrate the flexibility of the new integration method.

### Changed
- Rewrote honey sprite integration/compatibility.
	- Integrations by other mods are much more flexible now, including being able to add multiple options.
- Rewrote 'Simple' example mod to match new integration method.
- Code cleanup and reorganization.

### Fixed
- Refresh the honey object data properly after running the 'undo honey colors' console command and exiting the current save.

## [1.0.0] - 2025-01-18
#### Compatible with Stardew Valley v1.6.15 and SMAPI v4.1.10.

### Added
- Initial release.

[1.0.0]: https://github.com/voltaek/StardewMods/releases/tag/CHL-v1.0.0
[2.0.0]: https://github.com/voltaek/StardewMods/releases/tag/CHL-v2.0.0
