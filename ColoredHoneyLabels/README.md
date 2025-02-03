# Colored Honey Labels

## Integration Documentation

If you'd like to add your own honey with colored label/lid/etc. sprite into this mod's "Honey Sprite" config option list, this is the documentation for you.

At a high level, you just need to edit an entry into this mod's custom data asset dictionary to tell it about the texture that has your sprites loaded into it.
The entry will also include some information for describing your sprites to users and for the mod to work with the texture.
If your sprites aren't already loaded into a texture, you'll need to load them into one, first, before editing your entry into the dictionary.

It will probably make this documentation easier to follow if you reference the example mods I've created.
They demonstrate both simple and intermediate integrations, and are full of comments in the JSON describing what is going on and why.
They're located in the [ExampleIntegrationMods](ExampleIntegrationMods) folder. I'll mostly be describing the 'Simple' variant in this documentation.

The texture asset you use in the entry must be a minimum of 32px x 16px (unless you're messing with `ColorOverlayFromNextIndex` \[see below\],
in which case 16px x 16px) so that your honey sprite and label sprite can both fit in it, and should have a transparent background.
It contain a 16px x 16px honey sprite on the left half, and a 16px x 16px tint mask sprite for the label on the right.
The simplest way to get your sprites into a texture is to load your own PNG image into a custom texture asset.
New textures are created as the same size as the PNG you put into them.
You can do this with Content Patcher by writing a couple JSON files (as I've done for the example mods) or with SMAPI itself by writing
a C# mod (definitely overkill for this). See the [Load Action] Content Patcher documention for more information on loading images into texture assets.

The tint mask sprite will be colored and applied overtop of the honey sprite when drawn in-game.
You use this to apply color to any part of the honey sprite, such as the label, lid, and/or anything else.
For more examples of tint masks the game uses, see the other artisan object base sprites and tint mask sprites along the bottom of
the `TileSheets/Objects_2.png` file after [unpacking the game's content files].
There you can see how the wine, juice, pickles, and jelly sprites are built-up by the game.

For further reading on loading and editing assets, or just using Content Patcher in general, see the [Content Patcher Author Guide].

### Data Asset Target

`Mods/voltaek.ColoredHoneyLabels/SpriteData`

This is a dictionary data asset with string keys and custom objects (the contents of which are defined in 'Entry Data' below) as values.

### Entry Key

The unique key you use for your data entry into the dictionary will be stored in the user's `config.json` as the value behind the 'Honey Sprite' option.
Your key must be unique (easily accomplished by including `{{ModId}}` in it, which automatically adds your mod's unique ID) and should ideally also include
similar descriptors to your display name. Something like `{{ModId}}_honey_with_striped_label`, for example. If you don't use `{{ModId}}` for some reason,
then you should include something in it to identify it as coming from your mod.

### Entry Data

#### Required Fields

* `DisplayName` - A short description of your honey sprite. If your sprite is to add compatibility with another/your mod, you should ideally include
the mod's name or initials (to save space) in this. This text must fit in the 'Honey Sprite' config option list, so keep it as short as possible.

* `TextureName` - The name of the texture asset your sprite or sprites are loaded into.
Standard practice to ensure your texture name is unique is to start it with either `Mods/{{ModId}}/` or `{{ModId}}/`.
The texture must have your honey sprite in it (unless you're setting `ColorOverlayFromNextIndex` \[see below\] to `false`) and then immediately to the right
of your honey sprite (as in, in the next 16px x 16px sprite slot) must be your tint mask sprite. See the Simple example mod's PNG for reference.

#### Optional Fields

* `SpriteIndex` \[default: `0`\] - If your sprites are not the first ones in your texture, then you must provide the sprite index of the honey sprite.
This is only useful if you are referencing sprites in a spritesheet you're also using for other sprites, or if you're adding multiple entries and
are loading all of your sprites into one texture.

* `ColorOverlayFromNextIndex` \[default: `true`\] - When the game colors a `ColoredObject`, it does it one of two ways. The first way is to just "tint"
the object's sprite directly. This is what will happen if you set this to `false`; the game will just tint your honey sprite. No label sprite of any kind
is required for this option. The second way is how this mod works by default, where the sprite immediately to the right of the object is "tinted"
and then overlaid on top of the object sprite. In this case, we tint the label sprite and overlay it on top of the honey sprite.

### Example Data Edit

After you've loaded your texture, you can edit an entry into this mod's custom data asset dictionary.
```json
{
	"Action": "EditData",
	"Target": "Mods/voltaek.ColoredHoneyLabels/SpriteData",
	"LogName": "Edit entry into CHL sprite data dictionary",
	"Entries": {
		"{{ModId}}_simple_example_sprites": {
			"DisplayName": "Simple Example Sprites",
			"TextureName": "Mods/{{ModId}}/HoneyAndLabelTexture",
		}
	},
}
```
*This is an excerpt from the Simple example mod's `content.json` file in the [ExampleIntegrationMods](ExampleIntegrationMods) directory*

See the [EditData] documentation in the [Content Patcher Author Guide] for more information on how to construct this JSON.

[Load Action]: https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-load.md
[EditData]: https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-editdata.md#usage
[Content Patcher Author Guide]: https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-load.md
[unpacking the game's content files]: https://stardewvalleywiki.com/Modding:Editing_XNB_files#Unpack_game_files
