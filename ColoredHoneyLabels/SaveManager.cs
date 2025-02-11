using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley.Extensions;
using StardewValley.Internal;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	internal static class SaveManager
	{
		/// <summary>Shorthand for the main logger instance.</summary>
		private static IMonitor Logger => ModEntry.Logger;

		internal static void RegisterEvents()
		{
			ModEntry.Context.Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
			ModEntry.Context.Helper.Events.GameLoop.Saving += OnSaving;
			ModEntry.Context.Helper.Events.GameLoop.Saved += OnSaved;
		}

		private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaveLoaded)} - Started");

			PrepareHoneyForUse();

			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaveLoaded)} - Ended");
		}

		private static void OnSaving(object? sender, SavingEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaving)} - Started");

			PrepareHoneyForStorage();

			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaving)} - Ended");
		}

		private static void OnSaved(object? sender, SavedEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaved)} - Started");

			PrepareHoneyForUse();

			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaved)} - Ended");
		}

		private static void PrepareHoneyForUse()
		{
			try
			{
				// Go through all items in the game world to update both standard and colored honey objects to ready them for use in the world
				StardewValley.Utility.ForEachItemContext(delegate (in ForEachItemContext context)
				{
					if (context.Item.QualifiedItemId != Constants.HoneyObjectQualifiedIndentifier || context.Item is not SObject honeyObject)
					{
						return true;
					}

					// Change all existing honey objects to use the current sprite index
					honeyObject.ParentSheetIndex = AssetManager.SelectedSpriteData.SpriteIndex;

					if (honeyObject is ColoredObject coloredItem)
					{
						// Change all existing colored honey objects to use the current overlay value
						coloredItem.ColorSameIndexAsParentSheetIndex = !AssetManager.SelectedSpriteData.ColorOverlayFromNextIndex;

						Color? storedColor = coloredItem.TryGetStoredLabelColor();

						if (storedColor.HasValue)
						{
							// If the existing colored honey object has its label color stored on it, then restore its color to that
							coloredItem.color.Value = storedColor.Value;
						}
					}
					else
					{
						// Attempt to convert any non-colored honey to be colored
						ColoredObject? coloredHoneyObject = Utility.GetColoredHoneyObjectFromHoneyObject(honeyObject);

						if (coloredHoneyObject != null)
						{
							// Replace the `Object` with our `ColoredObject` recreated from it
							context.ReplaceItemWith(coloredHoneyObject);
						}
					}

					return true;
				});
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in {nameof(PrepareHoneyForUse)}.\nException Details:\n{ex}", LogLevel.Info);
			}
		}

		private static void PrepareHoneyForStorage()
		{
			try
			{
				// Go through all items in the game world to update both standard and colored honey objects to ready them for storage/saving
				StardewValley.Utility.ForEachItemContext(delegate (in ForEachItemContext context)
				{
					if (context.Item.QualifiedItemId != Constants.HoneyObjectQualifiedIndentifier || context.Item is not SObject honeyObject)
					{
						return true;
					}

					if (AssetManager.DefaultHoneySpriteIndex.HasValue)
					{
						// Set all honey objects to have the default sprite index so the save file will have it set to that.
						// This allows seamless uninstalls since honey objects won't have incorrect sprite indexes "cached" on them.
						honeyObject.ParentSheetIndex = AssetManager.DefaultHoneySpriteIndex.Value;
					}
					else
					{
						Logger.LogOnce($"Unable to restore default Honey sprite index in {nameof(PrepareHoneyForStorage)}", LogLevel.Info);
					}

					if (honeyObject is ColoredObject coloredItem)
					{
						if (!coloredItem.HasStoredLabelColor())
						{
							// Store the honey's current color if we haven't already
							coloredItem.StoreLabelColor(coloredItem.color.Value);
						}

						// Set the honey's color to white and set the color/tint to overlay on the honey sprite itself, rather than the next index's sprite.
						// Doing this means even though this honey is a `ColoredObject`, the tint will have essentially no affect.
						// This will allow all honey objects - "colored" or not - to appear the same as each other if this mod gets uninstalled.
						coloredItem.color.Value = Color.White;
						coloredItem.ColorSameIndexAsParentSheetIndex = true;
					}

					return true;
				});
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in {nameof(PrepareHoneyForStorage)}.\nException Details:\n{ex}", LogLevel.Info);
			}
		}
	}
}
