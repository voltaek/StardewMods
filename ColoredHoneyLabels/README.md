# Colored Honey Labels

## How to load your own honey and label sprites into this mod's Honey Sprite config option list

If you'd like to add your own honey with colored label sprite, this is the documentation for you.

At a high level, you just need to edit an entry into this mod's custom data asset dictionary to tell it about the texture that has your honey sprite loaded into it.
The entry will also include some information for the mod to work with the texture and some for telling users about your sprites.
If your honey isn't already loaded into a texture, you'll need to load it into one, first, before editing in your entry.

It will probably makes the following documentation easier to follow if you reference the example mods I've created to demonstrate both simple and intermediate
integrations. They're located in the [ExampleIntegrationMods](ExampleIntegrationMods) folder. I'll mostly be describing the 'Simple' variant in this documentation.

The texture you provide in the data entry must be a minimum of 32px x 16px (unless you're messing with `ColorOverlayFromNextIndex` \[see below\],
in which case 16px x 16px) so that your honey sprite and label sprite can both fit in it. The simplest way to get your sprites into a texture
is to load your own PNG image into a custom texture. New textures are created the same size as the PNG you put into them.
You can do this with Content Patcher by writing a couple JSON files (as I've done for the example mods) or with SMAPI itself by writing
a C# mod (definitely overkill for this). See the [Load Action] Content Patcher documention for more information on loading images into texture assets.

The image you load should be a transparent PNG with a 16px x 16px honey sprite on the left half, and a tint mask for the label on the right.
The tint mask will be colored and applied overtop of the honey sprite when drawn in-game. For more examples of tint masks the game uses, see
the other artisan object base sprites and tint mask sprites along the bottom of the `TileSheets/Objects_2.png` file after [unpacking the game's content files].
There you can see how the wine, juice, pickles, and jelly sprites are built-up by the game.

For further reading on loading and editing assets, or just using Content Patcher in general, see the [Content Patcher Author Guide].

### Data Asset Target

`Mods/voltaek.ColoredHoneyLabels/SpriteData`

This is a dictionary data asset with string keys and custom objects (defined in 'Entry Data' below) as values.

### Entry Key

The unique key you use for your data entry into the dictionary will be stored in the user's `config.json` as the value behind the 'Honey Sprite' option.
Your key must be unique (easily accomplished by including `{{ModId}}` in it, which automatically adds your mod's unique ID) and should ideally include
similar descriptors to your display name. Something like `{{ModId}}_honey_with_striped_label` would work. If you don't use `{{ModId}}` for some reason,
then you should include something in it to identify it as coming from your mod.

### Entry Data

#### Required Fields

* `DisplayName` - A short description of your honey sprite. If your sprite is to add compatibility with another mod, you should ideally include
that mod's name in this. This text must fit in the 'Honey Sprite' config option list, so keep it as short as possible.

* `TextureName` - The name of the texture your sprite or sprites are loaded into. This must have your honey sprite in it and (unless you're
setting `ColorOverlayFromNextIndex` \[see below\] to `false`), then immediately to the right of your honey sprite (as in, in the next 16px x 16px sprite slot)
must be your label tint mask sprite. See [the default sprites PNG](assets/default-sprites.png) for an example of this layout, or the Simple example mod's PNG.

#### Optional Fields

* `SpriteIndex` \[default: `0`\] - If your sprites are not the first ones in your texture, then you must provide the sprite index of the honey sprite.

* `ColorOverlayFromNextIndex` \[default: `true`\] - When the game colors a `ColoredObject`, it does it one of two ways. The first way is to just "tint"
the object's sprite directly. This is what will happen if you set this to `false`; the game will just tint your honey sprite. No label sprite of any kind
is required for this option. The second way is how this mod works by default, where the sprite immediately to the right of the object is "tinted"
and then overlaid on top of the object sprite. In this case, we tint the label sprite and overlay it on top of the honey sprite.

### Example Data Edit (excerpt from the [ExampleIntegrationMods](ExampleIntegrationMods) directory's Simple example mod's `content.json` file)
```json
{
	"Action": "EditData",
	"Target": "Mods/voltaek.ColoredHoneyLabels/SpriteData",
	"LogName": "Edit entry into CHL sprite data dictionary",
	"Entries": {
		// The dictionary key you put here will end up stored into user's CHL config files, so please make it both readable and unique.
		// The ideal way to make it unique is to include your mod's unique ID in it, which has the benefit of identifying its origins.
		"{{ModId}}_simple_example_sprites": {
			
			// A short, descriptive name; should probably include your mod's name in it.
			// This text must fit in the 'Honey Sprite' config option list, so keep it as short as possible.
			"DisplayName": "Simple Example Sprites",
			
			// The texture target you loaded your texture into earlier (see Simple example mod's full `config.json`).
			"TextureName": "Mods/{{ModId}}/HoneyAndLabelTexture",
		}
	},
}
```

See the [EditData] documentation in the [Content Patcher Author Guide] for more information on how to construct this JSON.

[Load Action]: https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-load.md
[EditData]: https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-editdata.md#usage
[Content Patcher Author Guide]: https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-load.md
[unpacking the game's content files]: https://stardewvalleywiki.com/Modding:Editing_XNB_files#Unpack_game_files
