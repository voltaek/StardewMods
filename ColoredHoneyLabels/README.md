# Colored Honey Labels

## How to load your own honey bottle sprite into this mod's custom texture asset

For your mod to replace the texture with the honey bottle and label tint mask sprites - which is assigned by this mod to the honey object -
you just need to load your own PNG image into the custom texture asset at a higher priority than this mod does it at; so anything higher than "Low".

Essentially, you just need to load your PNG - with whatever filename you'd like (by convention should be located in your mod's "assets" directory) -
into the **"Mods/voltaek.ColoredHoneyLabels/HoneyAndLabelMask"** custom texture asset. You can do this with Content Patcher by writing a couple JSON files,
or with SMAPI itself by writing a C# mod.

Loading your PNG into the texture asset will cause SMAPI to load your image into the asset instead of this mod's image, and everything should work seemlessly.
Please see the [Example Asset Override Mod](ExampleAssetOverrideMod) folder which is a working example Content Patcher mod.

The image you load should be a 32px x 16px transparent PNG with a 16px x 16px honey bottle sprite on the left half, and a tint mask for the label on the right.
The tint mask will be colored and applied overtop of the honey bottle sprite when drawn in-game. For more examples of tint masks the game uses, see
the other artisan object base sprites and tint mask sprites along the bottom of the `TileSheets/Objects_2.png` file after [unpacking the game's content files].
There you can see how the wine, juice, pickles, and jelly sprites are built-up by the game.

For further reading on how asset loading priority works or just on asset loading in general, see the [Content Patcher Author Guide].

[unpacking the game's content files]: https://stardewvalleywiki.com/Modding:Editing_XNB_files#Unpack_game_files
[Content Patcher Author Guide]: https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-load.md
