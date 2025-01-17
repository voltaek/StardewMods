using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	internal class Patches
	{
		/// <summary>Shorthand for the main logger instance.</summary>
		private static IMonitor Logger
		{
			get { return ModEntry.Logger; }
		}

		public static void ApplyPatches()
		{
			try
			{
				Harmony harmony = new(ModEntry.Context.Helper.ModContent.ModID);

				// Patch flavored honey creation method to return a colored object
				harmony.Patch(
					original: AccessTools.Method(typeof(ObjectDataDefinition), nameof(ObjectDataDefinition.CreateFlavoredHoney)),
					postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.CreateFlavoredHoney_ObjectDataDefinition_Postfix))
				);
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while registering a Harmony patch for {nameof(ObjectDataDefinition)}.{nameof(ObjectDataDefinition.CreateFlavoredHoney)}. "
					+ $"\nException Details:\n{ex}", LogLevel.Warn);
			}
		}

		/// <summary>Harmony postfix for `ObjectDataDefinition.CreateFlavoredHoney()`.</summary>
		/// <param name="__result">The returned value from calling the original method.</param>
		/// <param name="ingredient">The ingredient param that was given to the original method.</param>
		public static void CreateFlavoredHoney_ObjectDataDefinition_Postfix(ref SObject __result, SObject? ingredient)
		{
			try
			{
				Color wildHoneyLabelColor = Color.White;
				Color labelColor = TailoringMenu.GetDyeColor(ingredient) ?? wildHoneyLabelColor;

				if (!ModEntry.Config.MoreLabelColorVariety)
				{
					Logger.VerboseLog($"Label color: {labelColor.R} {labelColor.G} {labelColor.B} (RGB)");
				}
				else
				{
					if (!String.IsNullOrWhiteSpace(ingredient?.BaseName))
					{
						// Vary color based on the ingredient/honey flavor source's name. Sum the name's char bytes and vary based on odd or even.
						bool shouldVary = ingredient.BaseName.ToCharArray().Select(Convert.ToInt32).Sum() % 2 == 0;
						Logger.VerboseLog($"Ingredient '{ingredient.BaseName}' with {ingredient.GetContextTags().FirstOrDefault(x => x.StartsWith("color_"))}");

						if (shouldVary)
						{
							Logger.VerboseLog($"Original color: {labelColor.R} {labelColor.G} {labelColor.B} (RGB)");
							labelColor = Utility.ShiftColor(labelColor);
							Logger.VerboseLog($"Shifted color using for label: {labelColor.R} {labelColor.G} {labelColor.B} (RGB)");
						}
						else
						{
							Logger.VerboseLog($"Color for label: {labelColor.R} {labelColor.G} {labelColor.B} (RGB)");
						}
					}
					else
					{
						Logger.VerboseLog("No honey ingredient (such as for Wild Honey) or no ingredient BaseName value to potentially vary label color with");
					}
				}

				if (!ColoredObject.TrySetColor(__result, labelColor, out ColoredObject coloredHoney))
				{
					Logger.Log($"Failed to color the flavored honey object in {nameof(CreateFlavoredHoney_ObjectDataDefinition_Postfix)}", ModEntry.BuildLogLevel);

					return;
				}

				__result = coloredHoney;
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred in the {nameof(CreateFlavoredHoney_ObjectDataDefinition_Postfix)} Harmony patch "
					+ $"for {nameof(ObjectDataDefinition)}.{nameof(ObjectDataDefinition.CreateFlavoredHoney)}.\nException Details:\n{ex}", LogLevel.Warn);
			}
		}
	}
}
