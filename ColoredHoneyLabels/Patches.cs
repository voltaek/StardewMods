using ColoredHoneyLabels.Extensions;
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
		private static IMonitor Logger => ModEntry.Logger;

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
				Color labelColor = ColorManager.GetLabelColorFromHoneyIngredient(ingredient);

				if (!ColoredObject.TrySetColor(__result, labelColor, out ColoredObject coloredHoney))
				{
					Logger.Log($"Failed to color the flavored honey object in {nameof(CreateFlavoredHoney_ObjectDataDefinition_Postfix)}", Constants.BuildLogLevel);

					return;
				}

				// Whenever we calc a color we should store the outcome so we can avoid recalcs later.
				coloredHoney.StoreLabelColor(labelColor);

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
