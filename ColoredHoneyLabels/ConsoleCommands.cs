using StardewValley.Extensions;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	internal class ConsoleCommands
	{
		/// <summary>Shorthand for the main logger instance.</summary>
		private static IMonitor Logger => ModEntry.Logger;

		public const string UndoHoneyColors = "chl_undo_honey_colors";
		public const string GiveTestHoney = "chl_give_test_honey";
		public const string GiveTestHoneyList = "chl_give_test_honey_list";
		public const string ClearInventoryHoney = "chl_clear_inventory_honey";

		public static bool HasRunUndoHoneyColorsCommand { get; private set; } = false;

		public static readonly List<string> TestHoneyIngredientIdentifiers = new() {
			// Spring
			"591", // Tulip (red)
			"597", // Blue Jazz (blue)

			// Summer
			"376", // Poppy (orange)
			"593", // Summer Spangle (yellow)

			// Summer and Fall
			"421", // Sunflower (yellow)

			// Fall
			"595", // Fairy Rose (pink)

			// Forage
			"18", // Daffodil (yellow)
			"22", // Dandelion (yellow)
			"402", // Sweet Pea (purple)
			"418", // Crocus (purple)

			// Modded (enable "Enable Extended Flowers Pack" in Cornucopia's config)
			"Cornucopia_Iris", // (dark purple)
			"Cornucopia_Orchid", // (dark pink)
			"Cornucopia_Hydrangea", // (light cyan)
			"Cornucopia_Larkspur", // (cyan)
			"Cornucopia_Lupine", // (pale violet red)
			"Cornucopia_RosePrismatic", // (prismatic)

			// Modded Forage
			"Cornucopia_PitcherPlant", // (lime)

			// Modded from Cornucopia's "Rose Color Explosion" (enable in its config)
			"Cornucopia_RoseSpring3", // (green)
			"Cornucopia_RoseFall3", // (sand)
			"Cornucopia_RoseWinter3", // (black)
		};

		private static int TestHoneyIngredientListLastOutputIndex = -1;

		/// <summary>
		/// Reset tracked values that were changed when the user ran the console command to undo all colored honey objects.
		/// </summary>
		internal static void ResetUndoHoneyColors()
		{
			HasRunUndoHoneyColorsCommand = false;
		}

		/// <summary>Initialize our custom console commands.</summary>
		public static void AddCommands()
		{
			ModEntry.Context.Helper.ConsoleCommands.Add(
				ConsoleCommands.UndoHoneyColors,
				$"Restores all honey items in the entire save (all player inventories, chests, machines, etc.) which were modified by this mod "
					+ $"({ModEntry.Context.ModManifest.Name}) to be normal, non-colored honey items.\n\n"
					+ $"Also, until the current save or the game itself is closed, only default honey objects will be created, and all honey items will have the default honey icon/texture.\n\n"
					+ $"If you save your game after this has run, then once the game is closed you can safely uninstall the mod and have normal honey items the next time you open your save game.\n\n"
					+ $"Usage: {ConsoleCommands.UndoHoneyColors} [retry]\n"
					+ $"- retry: (bool) [optional, default: 0] Attempt to restore all honey items created by the mod, even if they were already restored to being a non-colored item.",
				UndoHoneyColorsCommand);

			ModEntry.Context.Helper.ConsoleCommands.Add(
				ConsoleCommands.GiveTestHoney,
				$"Puts one test honey object - flavored by the item with the given identifier, if any - into the farmer's inventory.\n\n"
					+ $"Usage: {ConsoleCommands.GiveTestHoney} [identifier]\n"
					+ $"- identifier: (string) [optional] An unqualified object identifier to use as the honey ingredient/flavor. For example, '591' is Tulip's identifier. If no identifier is specified a Wild Honey object will be given.",
				GiveTestHoneyCommand);

			ModEntry.Context.Helper.ConsoleCommands.Add(
				ConsoleCommands.GiveTestHoneyList,
				$"Puts one or more test honey objects into the farmer's inventory. Each honey will be flavored by a different flower on the testing identifiers list.\n\n"
					+ $"Usage: {ConsoleCommands.GiveTestHoneyList} [quantity] [continue]\n"
					+ $"- quantity: (integer|'all') [optional, default: 1] The quantity of test honey objects to provide from the test identifier list, or 'all' to get the whole list.\n"
					+ $"- continue: (bool) [optional, default: 0] Continue outputting from the testing identifiers list from the last entry output. Has no effect when quantity of 'all' is used.",
				GiveTestHoneyListCommand);

			ModEntry.Context.Helper.ConsoleCommands.Add(
				ConsoleCommands.ClearInventoryHoney,
				$"Clears all honey objects from the farmer's inventory. The command has no parameters.",
				ClearInventoryHoneyCommand);
		}

		/// <summary>Run our custom console command to attempt to restore all Honey items this mod has created back to vanilla, non-colored honey objects.</summary>
		/// <param name="command">The name of the command invoked.</param>
		/// <param name="args">The arguments received by the command. Each word after the command name is a separate argument.</param>
		private static void UndoHoneyColorsCommand(string command, string[] args)
		{
			Logger.Log($"Console command '{command}' invoked with {(args == null || args.Length == 0 ? "no " : "")}params"
				+ (args?.Length > 0 ? ": " + String.Join(", ", args.Select(x => $"'{x}'")) : ""), LogLevel.Trace);

			bool shouldRetryAll = false;

			if (args != null && args.Length > 0)
			{
				shouldRetryAll = ParseBoolParam(args[0]);
			}

			int restoredAsObjects = 0;

			try
			{
				// Mark that this command has been run so we can start the process of reverting our data and object edits.
				HasRunUndoHoneyColorsCommand = true;

				// By resetting the cache, our asset requested event handler can restore the default honey object data values.
				// Then when creating a new object below, it will be created with the proper default values.
				ModEntry.Context.Helper.GameContent.InvalidateCache(Constants.HoneyObjectParentAssetName);

				// Go through all items in the game world to update the relevant honey objects
				StardewValley.Utility.ForEachItemContext(delegate (in ForEachItemContext context)
				{
					if (context.Item.QualifiedItemId != Constants.HoneyObjectQualifiedIndentifier
						|| !context.Item.modData.Keys.Contains(Constants.ModDataKey_HasColoredLabel))
					{
						return true;
					}

					// If we're not retrying all objects and we've previously "restored" this object, then skip it.
					if (!shouldRetryAll && context.Item.modData[Constants.ModDataKey_HasColoredLabel] == "0")
					{
						return true;
					}

					// Update the mod data entry we added to mark this mod as having modified it
					context.Item.modData[Constants.ModDataKey_HasColoredLabel] = "0";

					// Change our `ColoredObject` back to an `Object`, as it is in vanilla.
					SObject honeyObject = new(context.Item.ItemId, context.Item.Stack);
					
					// Restore all field values from the original object
					honeyObject.CopyFieldsFrom(context.Item);

					// The `CopyFieldsFrom()` method uses `GetOneCopyFrom()` internally, so need to reset the stack size
					// from the hardcoded '1' in there to the actual value.
					honeyObject.Stack = context.Item.Stack;

					// Replace the `ColoredObject` with our `Object` recreated from it
					context.ReplaceItemWith(honeyObject);
					restoredAsObjects += 1;

					return true;
				});
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while updating Honey objects in the {UndoHoneyColors} console command.\nException Details:\n{ex}", LogLevel.Warn);
			}

			Logger.Log($"Removed color and restored default values to {restoredAsObjects} honey objects created by {ModEntry.Context.ModManifest.Name}.", LogLevel.Info);
		}

		/// <summary>Run our custom console command to create a Honey item flavored with an optionally specified ingredient.</summary>
		/// <param name="command">The name of the command invoked.</param>
		/// <param name="args">The arguments received by the command. Each word after the command name is a separate argument.</param>
		private static void GiveTestHoneyCommand(string command, string[] args)
		{
			Logger.Log($"Console command '{command}' invoked with {(args == null || args.Length == 0 ? "no " : "")}params"
				+ (args?.Length > 0 ? ": " + String.Join(", ", args.Select(x => $"'{x}'")) : ""), LogLevel.Trace);

			SObject? ingredient = null;

			// If they pass no param then create one honey object with no ingredient and put it in the farmer's inventory.
			if (args == null || args.Length == 0)
			{
				// do nothing
			}
			// If they specify the identifier param then attempt to create a honey object with that ingredient and put it in the farmer's inventory.
			else if (!String.IsNullOrWhiteSpace(args[0]))
			{
				ingredient = ItemRegistry.Create(args[0].Trim(), allowNull: true) as SObject;

				if (ingredient == null)
				{
					Logger.Log($"Failed to create an Item with identifier '{args[0].Trim()}' to use as the honey ingredient.", LogLevel.Info);

					return;
				}
			}
			else
			{
				Logger.Log($"Unknown parameter option for console command '{command}'", LogLevel.Warn);

				return;
			}

			SObject testHoney = ItemRegistry.GetObjectTypeDefinition().CreateFlavoredHoney(ingredient);
			testHoney.Price = 0;

			Game1.player.addItemToInventory(testHoney);
		}

		/// <summary>Run our custom console command to create Honey items with ingredients from a test list.</summary>
		/// <param name="command">The name of the command invoked.</param>
		/// <param name="args">The arguments received by the command. Each word after the command name is a separate argument.</param>
		private static void GiveTestHoneyListCommand(string command, string[] args)
		{
			Logger.Log($"Console command '{command}' invoked with {(args == null || args.Length == 0 ? "no " : "")}params"
				+ (args?.Length > 0 ? ": " + String.Join(", ", args.Select(x => $"'{x}'")) : ""), LogLevel.Trace);

			int quantity = 1;
			int startIndex = 0;

			// If they pass no param then create one honey object from an ingredient on the test list and put it in the farmer's inventory.
			if (args == null || args.Length == 0)
			{
				// do nothing
			}
			// If they pass the quantity param as 'all', then create honey objects from the entire test ingredient list and put them in the farmer's inventory.
			else if (args[0].Trim().ToLower() == "all")
			{
				quantity = TestHoneyIngredientIdentifiers.Count;
			}
			// If they pass the quantity param as an integer, then create that many honey objects from the test ingredient list and put them in the farmer's inventory.
			// If they also pass the continue param as true, then start outputting from the test ingredient list entry after the last one that was output.
			else if (Int32.TryParse(args[0], out quantity))
			{
				if (quantity > TestHoneyIngredientIdentifiers.Count)
				{
					quantity = TestHoneyIngredientIdentifiers.Count;
				}

				if (args.Length > 1)
				{
					bool shouldContinue = ParseBoolParam(args[1]);

					if (shouldContinue)
					{
						startIndex = TestHoneyIngredientListLastOutputIndex + 1;
					}
				}
			}
			else
			{
				Logger.Log($"Unknown parameter option for console command '{command}'", LogLevel.Warn);

				return;
			}

			ObjectDataDefinition objectDataDefinition = ItemRegistry.GetObjectTypeDefinition();
			HashSet<SObject> honeys = new();
			int ingredientIndex = startIndex;

			for (int i = 0; i < quantity; i++)
			{
				if (ingredientIndex > TestHoneyIngredientIdentifiers.Count - 1)
				{
					ingredientIndex = 0;
				}

				SObject? ingredient = ItemRegistry.Create(TestHoneyIngredientIdentifiers[ingredientIndex], allowNull: true) as SObject;
				TestHoneyIngredientListLastOutputIndex = ingredientIndex;

				SObject testListHoney = objectDataDefinition.CreateFlavoredHoney(ingredient);
				testListHoney.Price = 0;

				honeys.Add(testListHoney);

				ingredientIndex += 1;
			}

			foreach (SObject honey in honeys)
			{
				Game1.player.addItemToInventory(honey);
			}
		}

		/// <summary>Run our custom console command to clear the farmer's inventory of all Honey items.</summary>
		/// <param name="command">The name of the command invoked.</param>
		/// <param name="args">The arguments received by the command. Each word after the command name is a separate argument.</param>
		private static void ClearInventoryHoneyCommand(string command, string[] args)
		{
			Logger.Log($"Console command '{command}' invoked with {(args == null || args.Length == 0 ? "no " : "")}params"
				+ (args?.Length > 0 ? ": " + String.Join(", ", args.Select(x => $"'{x}'")) : ""), LogLevel.Trace);

			// Simply clear all honey items from the farmer's inventory
			Game1.player.Items.RemoveWhere(x => x?.QualifiedItemId == Constants.HoneyObjectQualifiedIndentifier);
		}

		/// <summary>
		/// Parse a given parameter string into a boolean value. Accepts multiple common boolean-meaning strings.
		/// </summary>
		/// <param name="param">The parameter string to parse.</param>
		/// <returns>`True` if a true-indicating value is found, and `False` if not.</returns>
		private static bool ParseBoolParam(string param)
		{
			if (String.IsNullOrWhiteSpace(param))
			{
				return false;
			}

			string trimmedParam = param.Trim();

			if (Boolean.TryParse(trimmedParam, out bool boolParsed))
			{
				return boolParsed;
			}
			
			switch (trimmedParam.ToLower())
			{
				case "1":
				case "true":
				case "on":
				case "yes":
					return true;
			}

			return false;
		}
	}
}
