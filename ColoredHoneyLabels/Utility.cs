using Microsoft.Xna.Framework;
using StardewValley.Internal;
using StardewValley.Menus;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	internal static class Utility
	{
		/// <summary>Shorthand for the main logger instance.</summary>
		private static IMonitor Logger => ModEntry.Logger;

		/// <summary>
		/// Shorthand property for creating a verbose log entry header.
		/// We want to use the verbose log method directly for best performance, both when actually using verbose and not.
		/// </summary>
		internal static string VerboseStart
		{
			// Show microsecond, so we can tell if something is slow.
			get { return ModEntry.Logger.IsVerbose ? DateTime.Now.ToString("ffffff") : String.Empty; }
		}

		// These are only used internally while refreshing colors
		private static bool IsRefreshing { get; set; } = false;
		private static Dictionary<string, Color> PreservedItemIDColors = new();

		/// <summary>
		/// Shift a given color to a close, but distinct color. The new color should ideally be not too close to the next closest primary color.
		/// </summary>
		/// <param name="color">The color to shift</param>
		/// <returns>The shifted color.</returns>
		public static Color ShiftColor(Color color)
		{
			StardewValley.Utility.RGBtoHSL(color.R, color.G, color.B, out double colorHue, out double colorSat, out double colorLum);

			// Darken bright colors
			if (colorLum > 0.75)
			{
				colorLum -= 0.15;
			}
			// Hue-shift warm colors a little since their primary colors are close in hue number
			else if (colorHue < 90)
			{
				colorHue += 15;
			}
			// Hue-shift other colors more
			else
			{
				colorHue += 30;
			}

			StardewValley.Utility.HSLtoRGB(colorHue, colorSat, colorLum, out int colorRed, out int colorGreen, out int colorBlue);

			return new Color(colorRed, colorGreen, colorBlue);
		}

		/// <summary>
		/// Convert a colored object back into a non-colored one while maintaining all non-color-related data.
		/// </summary>
		/// <param name="coloredObject">The colored object to convert.</param>
		/// <returns>A standard, non-colored object.</returns>
		public static SObject GetObjectFromColoredObject(ColoredObject coloredObject)
		{
			// Instantiate a new `Object`
			SObject standardObject = new(coloredObject.ItemId, coloredObject.Stack);

			// Restore all field values from the original object
			standardObject.CopyFieldsFrom(coloredObject);

			// The `CopyFieldsFrom()` method uses `GetOneCopyFrom()` internally, so need to reset the stack size
			// from the hardcoded '1' in there to the actual value.
			standardObject.Stack = coloredObject.Stack;

			return standardObject;
		}

		
		internal static void RefreshAllHoneyColors()
		{
			IsRefreshing = true;
			PreservedItemIDColors.Clear();

			int refreshedLabelColorCount = 0;

			try
			{
				// Go through all items in the game world to refresh the colors of colored honey objects
				StardewValley.Utility.ForEachItemContext(delegate (in ForEachItemContext context)
				{
					if (context.Item.QualifiedItemId != Constants.HoneyObjectQualifiedIndentifier || context.Item is not ColoredObject coloredItem)
					{
						return true;
					}

					Color freshColor = GetLabelColorFromHoneyObject(coloredItem);
					coloredItem.color.Value = freshColor;

					// Whenever we calc a color we should notate the outcome so we can avoid recalcs later.
					coloredItem.StoreLabelColor(freshColor);

					refreshedLabelColorCount += 1;

					return true;
				});

				Logger.Log($"Refreshed {refreshedLabelColorCount} colored Honey items.", LogLevel.Debug);
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in {nameof(RefreshAllHoneyColors)}.\nException Details:\n{ex}", LogLevel.Info);
			}

			PreservedItemIDColors.Clear();
			IsRefreshing = false;
		}


		internal static Color GetLabelColorFromHoneyIngredient(SObject? honeyIngredient)
		{
			Color labelColor = Constants.WildHoneyLabelColor;
			Color? dyeColor = TailoringMenu.GetDyeColor(honeyIngredient);

			if (dyeColor.HasValue)
			{
				labelColor = dyeColor.Value;
			}

			if (!ModEntry.Config.MoreLabelColorVariety || honeyIngredient == null || String.IsNullOrWhiteSpace(honeyIngredient.BaseName))
			{
				return labelColor;
			}

			// Vary color based on the ingredient/honey flavor source's name. Sum the name's char bytes and vary based on odd or even.
			bool shouldVary = honeyIngredient!.BaseName.ToCharArray().Select(Convert.ToInt32).Sum() % 2 == 0;

			if (shouldVary)
			{
				labelColor = Utility.ShiftColor(labelColor);
			}

			return labelColor;
		}


		internal static Color GetLabelColorFromHoneyObject(SObject honeyObject)
		{
			// If we have a stored label color, just pull that unless we're purposefully refreshing colors
			if (!IsRefreshing && honeyObject.modData.ContainsKey(Constants.ModDataKey_LabelColorPackedValue)
				&& UInt32.TryParse(honeyObject.modData[Constants.ModDataKey_LabelColorPackedValue], out UInt32 packedValue))
			{
				return new(packedValue);
			}

			// If we don't have a stored color, we'll need to get the honey flavor ingredient to determine the color
			string? preservedItemID = honeyObject.GetPreservedItemId();

			if (String.IsNullOrEmpty(preservedItemID))
			{
				return Constants.WildHoneyLabelColor;
			}

			// If we already calc'd the color for this ID during the refresh, pull it from the temp cache of them
			if (IsRefreshing && PreservedItemIDColors.ContainsKey(preservedItemID))
			{
				return PreservedItemIDColors[preservedItemID];
			}

			if (ItemRegistry.Create(preservedItemID, allowNull: true) is not SObject honeyIngredient)
			{
				return Constants.WildHoneyLabelColor;
			}

			Color? ingredientColor = GetLabelColorFromHoneyIngredient(honeyIngredient);

			if (!ingredientColor.HasValue)
			{
				return Constants.WildHoneyLabelColor;
			}

			// Temp cache the color while refreshing to speed things up
			if (IsRefreshing && !PreservedItemIDColors.ContainsKey(preservedItemID))
			{
				PreservedItemIDColors.Add(preservedItemID, ingredientColor.Value);
			}

			return ingredientColor.Value;
		}


		internal static ColoredObject? GetColoredHoneyObjectFromHoneyObject(SObject honeyObject)
		{
			Color labelColor = GetLabelColorFromHoneyObject(honeyObject);

			if (!ColoredObject.TrySetColor(honeyObject, labelColor, out ColoredObject coloredHoney))
			{
				ModEntry.Logger.LogOnce($"Failed to color the honey object in {nameof(GetColoredHoneyObjectFromHoneyObject)}", Constants.BuildLogLevel);

				return null;
			}

			// Whenever we calc a color we should notate the outcome so we can avoid recalcs later.
			coloredHoney.StoreLabelColor(labelColor);

			return coloredHoney;
		}

		internal static void StoreLabelColor(this SObject obj, Color labelColor)
		{
			if (!obj.modData.TryAdd(Constants.ModDataKey_LabelColorPackedValue, labelColor.PackedValue.ToString()))
			{
				obj.modData[Constants.ModDataKey_LabelColorPackedValue] = labelColor.PackedValue.ToString();
			}
		}
	}
}
