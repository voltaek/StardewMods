using ColoredHoneyLabels.Extensions;
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
	internal static class ColorManager
	{
		/// <summary>Shorthand for the main logger instance.</summary>
		private static IMonitor Logger => ModEntry.Logger;

		/// <summary>Whether we're currently refreshing colors by recalculating them or not.</summary>
		private static bool IsRefreshing { get; set; } = false;

		/// <summary>Cached color calculation results indexed by the ingredient's Item ID.</summary>
		private static Dictionary<string, Color> PreservedItemIDColorsCache = new();

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

		/// <summary>Recalculate the label color of all honey objects.</summary>
		internal static void RefreshAllHoneyColors()
		{
			IsRefreshing = true;
			PreservedItemIDColorsCache.Clear();

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

					// Whenever we calc a color we should store the outcome so we can avoid recalcs later.
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

			IsRefreshing = false;
		}

		/// <summary>Determine the label color from a Honey object.</summary>
		/// <param name="honeyObject">The honey object.</param>
		/// <returns>The honey label color.</returns>
		internal static Color GetLabelColorFromHoneyObject(SObject honeyObject)
		{
			// If we have a stored label color on the object, just pull that unless we're purposefully refreshing colors
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

			// Pull the calculated color for this ID from the cache when possible
			if (PreservedItemIDColorsCache.ContainsKey(preservedItemID))
			{
				return PreservedItemIDColorsCache[preservedItemID];
			}

			if (ItemRegistry.Create(preservedItemID, allowNull: true) is not SObject honeyIngredient)
			{
				// Cache the ID for the failed object creation so we don't keep trying to do it
				PreservedItemIDColorsCache.Add(preservedItemID, Constants.WildHoneyLabelColor);

				return Constants.WildHoneyLabelColor;
			}

			// NOTE - This internally caches the calculated color, so we don't need to do so ourselves
			Color ingredientColor = GetLabelColorFromHoneyIngredient(honeyIngredient);

			return ingredientColor;
		}

		/// <summary>Use a honey ingredient (such as a flower) to calculate the label color for a honey sprite.</summary>
		/// <param name="honeyIngredient">The ingredient to calculate with.</param>
		/// <returns>The honey label color.</returns>
		internal static Color GetLabelColorFromHoneyIngredient(SObject? honeyIngredient)
		{
			// Pull the calculated color for this ID from the cache when possible
			if (honeyIngredient != null && PreservedItemIDColorsCache.ContainsKey(honeyIngredient.QualifiedItemId))
			{
				return PreservedItemIDColorsCache[honeyIngredient.QualifiedItemId];
			}

			Color labelColor = Constants.WildHoneyLabelColor;
			Color? dyeColor = TailoringMenu.GetDyeColor(honeyIngredient);

			if (dyeColor.HasValue)
			{
				labelColor = dyeColor.Value;
			}

			if (!ModEntry.Config.MoreLabelColorVariety || honeyIngredient == null || String.IsNullOrWhiteSpace(honeyIngredient.BaseName))
			{
				if (honeyIngredient != null)
				{
					// Cache the calculated color when possible
					PreservedItemIDColorsCache.Add(honeyIngredient.QualifiedItemId, labelColor);
				}

				return labelColor;
			}

			// Vary color based on the ingredient/honey flavor source's name. Sum the name's char bytes and vary based on odd or even.
			bool shouldVary = honeyIngredient.BaseName.ToCharArray().Select(Convert.ToInt32).Sum() % 2 == 0;

			if (shouldVary)
			{
				labelColor = ColorManager.ShiftColor(labelColor);
			}

			// Cache the calculated color
			PreservedItemIDColorsCache.Add(honeyIngredient.QualifiedItemId, labelColor);

			return labelColor;
		}

		/// <summary>Duplicates a non-colored Honey object, calculates the color it should be, and returns a colored Honey object.</summary>
		/// <param name="honeyObject">The honey object to duplicate and color.</param>
		/// <returns>A colored object or `null` on failure.</returns>
		internal static ColoredObject? GetColoredHoneyObjectFromHoneyObject(SObject honeyObject)
		{
			Color labelColor = GetLabelColorFromHoneyObject(honeyObject);

			if (!ColoredObject.TrySetColor(honeyObject, labelColor, out ColoredObject coloredHoney))
			{
				ModEntry.Logger.LogOnce($"Failed to color the honey object in {nameof(GetColoredHoneyObjectFromHoneyObject)}", Constants.BuildLogLevel);

				return null;
			}

			// Whenever we calc a color we should store the outcome so we can avoid recalcs later.
			coloredHoney.StoreLabelColor(labelColor);

			return coloredHoney;
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
	}
}
