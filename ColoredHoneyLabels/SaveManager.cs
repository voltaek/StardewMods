using ColoredHoneyLabels.Extensions;
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

		/// <summary>Registers the event handlers we need for this manager to operate.</summary>
		internal static void RegisterEvents()
		{
			ModEntry.Context.Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
			ModEntry.Context.Helper.Events.GameLoop.Saving += OnSaving;
			ModEntry.Context.Helper.Events.GameLoop.Saved += OnSaved;
		}

		/// <inheritdoc cref="IGameLoopEvents.SaveLoaded"/>
		private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaveLoaded)} - Started");

			PrepareHoneyForDisplay();

			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaveLoaded)} - Ended");
		}

		/// <inheritdoc cref="IGameLoopEvents.Saving"/>
		private static void OnSaving(object? sender, SavingEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaving)} - Started");

			PrepareHoneyForStorage();

			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaving)} - Ended");
		}

		/// <inheritdoc cref="IGameLoopEvents.Saved"/>
		private static void OnSaved(object? sender, SavedEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaved)} - Started");

			PrepareHoneyForDisplay();

			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaved)} - Ended");
		}

		/// <summary>Update all honey objects in the save for display by our mod.</summary>
		private static void PrepareHoneyForDisplay()
		{
			int existingColoredCount = 0;
			int newlyColoredCount = 0;
			int existingNonColoredCount = 0;

			try
			{
				// Go through all items in the game world to update both standard and colored honey objects to ready them for display in the world
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

						existingColoredCount += 1;
					}
					else
					{
						// Attempt to convert any non-colored honey to be colored.
						// This includes honey items that existed in the save before this mod was installed,
						// but also generated items such as those held by Bee Houses.
						ColoredObject? coloredHoneyObject = ColorManager.GetColoredHoneyObjectFromHoneyObject(honeyObject);

						if (coloredHoneyObject != null)
						{
							// Replace the `Object` with our `ColoredObject` recreated from it
							context.ReplaceItemWith(coloredHoneyObject);

							newlyColoredCount += 1;
						}
						else
						{
							existingNonColoredCount += 1;
						}
					}

					return true;
				});

				Logger.Log($"Honey objects prepared for use: [{existingColoredCount} Existing Colored] "
					+ $"[{newlyColoredCount} Newly Colored] [{existingNonColoredCount} Non-Colored]", Constants.BuildLogLevel);
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in {nameof(PrepareHoneyForDisplay)}.\nException Details:\n{ex}", LogLevel.Info);
			}
		}

		/// <summary>
		/// Update all honey objects in the save to be stored in the save file.
		/// We update them so that if this mod is uninstalled the honey objects will look vanilla; no broken sprites, weird color overlays, etc.
		/// While doing that, we also set up the honey objects to be able to be performantly converted back to fully-colored honey objects by our mod.
		/// </summary>
		private static void PrepareHoneyForStorage()
		{
			int coloredCount = 0;
			int nonColoredCount = 0;

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

						// Change the honey object's color to white and configure it so its own sprite gets "tinted" rather than the next
						// index's sprite - which would then get overlaid onto the honey sprite.
						// Note that setting the color to transparent doesn't work; the whole honey sprite becomes transparent/invisible.
						// Doing this means even though this honey is a `ColoredObject`, the tint will have essentially no affect.
						// This will allow all honey objects - "colored" or not - to appear the same as each other if this mod gets uninstalled.
						coloredItem.color.Value = Constants.SaveCompatibilityLabelColor;
						coloredItem.ColorSameIndexAsParentSheetIndex = true;

						coloredCount += 1;
					}
					else
					{
						nonColoredCount += 1;
					}

					return true;
				});

				Logger.Log($"Honey objects prepared for saving: [{coloredCount} Colored] [{nonColoredCount} Non-Colored]", Constants.BuildLogLevel);
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in {nameof(PrepareHoneyForStorage)}.\nException Details:\n{ex}", LogLevel.Info);
			}
		}
	}
}
