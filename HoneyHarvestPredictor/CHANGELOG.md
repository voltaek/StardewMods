# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.2] - 2025-02-23
#### Compatible with Stardew Valley v1.6.15 and SMAPI v4.1.10.

### Fixed
- Fixed another possible SMAPI error message from garden pot removal.


## [4.0.1] - 2025-02-23
#### Compatible with Stardew Valley v1.6.15 and SMAPI v4.1.10.

### Fixed
- Fix possible SMAPI error message from garden pot removal.

### Changed
- Minor code improvements.


## [4.0.0] - 2025-01-01
#### Compatible with Stardew Valley v1.6.15 and SMAPI v4.1.10.

**When upgrading, be sure to delete any previous 'Honey Harvest Sync' mod folder and files.**

### Changed
- Changed name of mod from 'Honey Harvest Sync' to 'Honey Harvest Predictor'.
	- API was also renamed, hence the full version bump to v4.0.
	- Console command `hhs_refresh` renamed to `hhp_refresh`.
- Compatibility with Better Beehouses (an optional dependency) now requires at least version v2.1.3.
- Full rewrite of honey-flavor-source tracking to use event listeners instead of polling.
- Replace custom bee house range check with faster built-in function.

### Fixed
- Refresh bee houses near all parts of a harvested giant crop, not just its top left tile.
- Track bushes' harvested state rather than bloom state.
- Clean up tracking when returning to the title screen.

### Added
- Handle when tracked things are destroyed by bombs, especially garden pots.
- Only check bee houses becoming ready throughout the day if they're not configured refresh daily (daily is the default).


## [3.0.0] - 2024-06-14

### Added
- Custom console command `hhs_refresh` for manually refresh known bee houses or everything.
	- Use `help hhs_refresh` in the SMAPI console for information on how to use.

### Changed
- See `v3.0.0-beta.1` release notes below for full list of changes in this release.


## [3.0.0-beta.1] - 2024-06-11
#### Compatible with Stardew Valley v1.6.8 and SMAPI v4.0.8.

### Added
- Compatibility with the Better Beehouses mod (min v2.1.1)
	- Support all of its various honey flavor sources (crops, forage, bushes, fruit trees, giant crops)
	- Support crops, forage, and bushes in garden pots, too.
	- Support its various configuration options, such as flower range, indoor and/or winter honey production, etc.
- An API for other mods to access some info and functionality of this mod.
- Added entry to `modData` of our custom held item in bee houses to mark it as coming from this mod.

### Changed
- Simplified checks for removed bee houses.
- Better handling of a honey source having a `null` `indexOfHarvest.Value` property.
- Change from using `GameLocation` objects as keys of the tracking `Dictionary`s to using the location's `NameOrUniqueName` string value.
- Codebase reorganization.
- Changed to `MPL-2.0` license and include in release ZIP files.


## [2.0.1] - 2024-04-01
#### Compatible with Stardew Valley v1.6.3 and SMAPI v4.0.4.

### Fixed
- Error when a tracked flower crop's dirt becomes invalid.

### Changed
- Reduced log messages output to the console for non-issues.


## [2.0.0] - 2024-03-19
#### Compatible with Stardew Valley v1.6 and SMAPI v4.0.

### Added
- 'BeeHouseReadyIcon' config option and new 'Flower' icon feature as its default.

### Changed
- Compatibility with all of Stardew Valley's internal changes for v1.6.


## [1.1.1] - 2024-03-17
#### Compatible with Stardew Valley v1.5.6 and SMAPI v3.18.x.

### Fixed
- Temporarily revert new icon option that doesn't work in Stardew Valley pre-v1.6.
- Minor bug with updating bee houses that are finishing during the current day.


## [1.1.0] - 2024-03-17
#### Compatible with Stardew Valley v1.5.6 and SMAPI v3.18.6.

### Added
- Flower icon option. Set icon option to 'Flower' as default.
- GMCM integration.


## [1.0.0] - 2024-03-10
#### Compatible with Stardew Valley v1.5.6 and SMAPI v3.18.6.

### Added
- Initial release.

[4.0.2]: https://github.com/voltaek/StardewMods/releases/tag/HHP-v4.0.2
[4.0.1]: https://github.com/voltaek/StardewMods/releases/tag/HHP-v4.0.1
[4.0.0]: https://github.com/voltaek/StardewMods/releases/tag/HHP-v4.0.0
[3.0.0]: https://github.com/voltaek/StardewMods/releases/tag/v3.0.0
[3.0.0-beta.1]: https://github.com/voltaek/StardewMods/releases/tag/v3.0.0-beta.1
[2.0.1]: https://github.com/voltaek/StardewMods/releases/tag/v2.0.1
[2.0.0]: https://github.com/voltaek/StardewMods/releases/tag/v2.0.0
[1.1.1]: https://github.com/voltaek/StardewMods/releases/tag/v1.1.1
[1.1.0]: https://github.com/voltaek/StardewMods/releases/tag/v1.1.0
[1.0.0]: https://github.com/voltaek/StardewMods/releases/tag/v1.0.0
