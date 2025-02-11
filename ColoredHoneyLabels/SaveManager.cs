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
			ConvertAllHoneyToColored();
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaveLoaded)} - Ended");
		}

		private static void OnSaving(object? sender, SavingEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaving)} - Started");
			ConvertAllHoneyToStandard();
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaving)} - Ended");
		}

		private static void OnSaved(object? sender, SavedEventArgs e)
		{
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaved)} - Started");
			ConvertAllHoneyToColored();
			Logger.VerboseLog($"{Utility.VerboseStart} {nameof(OnSaved)} - Ended");
		}

		private static void ConvertAllHoneyToColored()
		{
			int convertedToColoredCount = 0;

			try
			{
				// Go through all items in the game world to update the standard honey objects to colored ones
				StardewValley.Utility.ForEachItemContext(delegate (in ForEachItemContext context)
				{
					if (context.Item.QualifiedItemId != Constants.HoneyObjectQualifiedIndentifier || context.Item is ColoredObject
						|| !context.Item.HasTypeObject() || context.Item is not SObject honeyObject)
					{
						return true;
					}

					ColoredObject? coloredHoneyObject = Utility.GetColoredHoneyObjectFromHoneyObject(honeyObject);

					if (coloredHoneyObject == null)
					{
						return true;
					}

					// Set the object's sprite index manually to avoid us having to invalidate the cached object data definition.
					coloredHoneyObject.ParentSheetIndex = AssetManager.SelectedSpriteData.SpriteIndex;

					// Replace the `Object` with our `ColoredObject` recreated from it
					context.ReplaceItemWith(coloredHoneyObject);
					convertedToColoredCount += 1;

					return true;
				});

				Logger.VerboseLog($"Replaced {convertedToColoredCount} standard Honey items with colored ones.");
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in {nameof(ConvertAllHoneyToStandard)}.\nException Details:\n{ex}", LogLevel.Info);
			}
		}

		private static void ConvertAllHoneyToStandard()
		{
			int convertedToStandardCount = 0;

			try
			{
				// Go through all items in the game world to update the colored honey objects back to standard ones
				StardewValley.Utility.ForEachItemContext(delegate (in ForEachItemContext context)
				{
					if (context.Item.QualifiedItemId != Constants.HoneyObjectQualifiedIndentifier || context.Item is not ColoredObject coloredItem)
					{
						return true;
					}

					// Change our `ColoredObject` back to an `Object`, as it is in vanilla.
					SObject honeyObject = Utility.GetObjectFromColoredObject(coloredItem);

					// Set the object's sprite index manually to avoid us having to invalidate the cached object data definition.
					if (AssetManager.DefaultHoneySpriteIndex.HasValue)
					{
						honeyObject.ParentSheetIndex = AssetManager.DefaultHoneySpriteIndex.Value;
					}
					else
					{
						Logger.LogOnce($"Unable to restore default Honey sprite index in {nameof(ConvertAllHoneyToStandard)}", LogLevel.Info);
					}

					// Replace the `ColoredObject` with our `Object` recreated from it
					context.ReplaceItemWith(honeyObject);
					convertedToStandardCount += 1;

					return true;
				});

				Logger.VerboseLog($"Replaced {convertedToStandardCount} colored Honey items with standard ones.");
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in {nameof(ConvertAllHoneyToStandard)}.\nException Details:\n{ex}", LogLevel.Info);
			}
		}
	}
}
