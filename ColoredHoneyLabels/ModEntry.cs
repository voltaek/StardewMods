using ColoredHoneyLabels.Integrations;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley.Extensions;
using StardewValley.GameData.Objects;
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
	internal sealed class ModEntry : Mod
	{
		#nullable disable
		
		/// <summary>A reference to our mod's instantiation to use everywhere.</summary>
		internal static ModEntry Context { get; private set; }

		/// <summary>A reference to the Mod's logger so we can use it everywhere.</summary>
		internal static IMonitor Logger { get; private set; }

		/// <summary>The mod configuration from the player.</summary>
		internal static ModConfig Config { get; private set; }

		#nullable enable

		#if DEBUG
			// For debug builds, show log messages as DEBUG so they show in the SMAPI console.
			public const LogLevel BuildLogLevel = LogLevel.Debug;
		#else
			public const LogLevel BuildLogLevel = LogLevel.Trace;
		#endif

		public const string HoneyObjectUnqualifiedIndentifier = "340";
		public const string ModAssetPath_HoneyAndLabelMaskTexture = "assets/honey-and-label-mask.png";

		public static string ModAssetName_HoneyAndLabelMaskTexture
		{
			get { return $"Mods/{Context.ModManifest.UniqueID}/HoneyAndLabelMask"; }
		}

		public const string ConsoleCommand_GiveTestHoney = "chl_give_test_honey";
		public const string ConsoleCommand_GiveTestHoneyList = "chl_give_test_honey_list";
		public const string ConsoleCommand_ClearInventoryHoney = "chl_clear_inventory_honey";

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

		/// <summary>The mod entry point, called after the mod is first loaded.</summary>
		/// <param name="helper">Provides simplified APIs for writing mods.</param>
		public override void Entry(IModHelper helper)
		{
			Context = this;
			Logger = Monitor;

			// Read user's config
			Config = Helper.ReadConfig<ModConfig>();



			// TODO - Replace placeholder Nexus update key in project file



			// Rig up event handler to set up Generic Mod Config Menu integration
			Helper.Events.GameLoop.GameLaunched += OnGameLaunched;

			// Add our asset and modify the honey object's definition
			Helper.Events.Content.AssetRequested += OnAssetRequested;

			Harmony harmony = new(Helper.ModContent.ModID);

			try
			{
				// Patch flavored honey to return a colored object
				harmony.Patch(
					original: AccessTools.Method(typeof(ObjectDataDefinition), nameof(ObjectDataDefinition.CreateFlavoredHoney)),
					postfix: new HarmonyMethod(typeof(ModEntry), nameof(CreateFlavoredHoney_ObjectDataDefinition_Postfix))
				);
			}
			catch (Exception ex)
			{
				Logger.Log($"An error occurred while registering a Harmony patch for {nameof(ObjectDataDefinition)}.{nameof(ObjectDataDefinition.CreateFlavoredHoney)}. "
					+ $"\nException Details:\n{ex}", LogLevel.Warn);
			}

			// Rig up console command handling

			Helper.ConsoleCommands.Add(
				ConsoleCommand_GiveTestHoney,
				$"Puts one test honey object - flavored by the item with the given identifier, if any - into the farmer's inventory.\n\n"
					+ $"Usage: {ConsoleCommand_GiveTestHoney} [identifier]\n"
					+ $"- identifier: (string) [optional] An unqualified object identifier to use as the honey ingredient/flavor. For example, '591' is Tulip's identifier. If no identifier is specified a Wild Honey object will be given.",
				DoConsoleCommand);

			Helper.ConsoleCommands.Add(
				ConsoleCommand_GiveTestHoneyList,
				$"Puts one or more test honey objects into the farmer's inventory. Each honey will be flavored by a different flower on the testing identifiers list.\n\n"
					+ $"Usage: {ConsoleCommand_GiveTestHoneyList} [quantity] [continue]\n"
					+ $"- quantity: (integer|'all') [optional, default: 1] The quantity of test honey objects to provide from the test identifier list, or 'all' to get the whole list.\n"
					+ $"- continue: (bool) [optional, default: 0] Continue outputting from the testing identifiers list from the last entry output. Has no effect when quantity of 'all' is used.",
				DoConsoleCommand);

			Helper.ConsoleCommands.Add(
				ConsoleCommand_ClearInventoryHoney,
				$"Clears all honey objects from the farmer's inventory. The command has no parameters.",
				DoConsoleCommand);
		}

		/// <inheritdoc cref="IGameLoopEvents.GameLaunched"/>
		/// <param name="sender">The event sender. This isn't applicable to SMAPI events, and is always null.</param>
		/// <param name="e">The event data.</param>
		private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
		{
			// Get Generic Mod Config Menu's API (if it's installed)
			IGenericModConfigMenuApi? configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

			if (configMenu is null)
			{
				return;
			}

			// Register mod
			configMenu.Register(
				mod: ModManifest,
				reset: () => Config = new ModConfig(),
				save: () => Helper.WriteConfig(Config)
			);

			// Add each config value
			configMenu.AddBoolOption(
				mod: ModManifest,
				name: () => "More Label Color Variety",
				tooltip: () => "Enable this to slightly shift the label color of some honey types, resulting in a larger variety of label colors.",
				getValue: () => Config.MoreLabelColorVariety,
				setValue: value => {
					bool oldValue = Config.MoreLabelColorVariety;
					Config.MoreLabelColorVariety = value;

					Monitor.Log($"Updated {nameof(Config.MoreLabelColorVariety)} config value via GMCM from '{oldValue}' to '{value}'", LogLevel.Debug);
				}
			);
		}

		/// <inheritdoc cref="IContentEvents.AssetRequested"/>
		/// <param name="sender">The event sender. This isn't applicable to SMAPI events, and is always null.</param>
		/// <param name="e">The event data.</param>
		private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			// When our custom asset is requested, load it from our image file
			if (e.NameWithoutLocale.IsEquivalentTo(ModAssetName_HoneyAndLabelMaskTexture))
			{
				// This asset can be easily overridden in something as simple as Content Patcher mod with a Load entry that loads this same asset,
				// just with a higher load priority set. This means any other mod that has a custom honey icon could create an image file with their
				// base honey icon sprite on the left and a cover overlay mask on the right, and if that mod loaded that image into this asset,
				// then this mod would use those sprites instead, allowing custom honey bottle sprites from the other mod to have automatically colored labels.
				e.LoadFromModFile<Texture2D>(ModAssetPath_HoneyAndLabelMaskTexture, AssetLoadPriority.Low);
			}

			// When Objects data is loaded, make our edits to the Honey object definition.
			if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
			{
				e.Edit(asset => {
					IDictionary<string, ObjectData> objects = asset.AsDictionary<string, ObjectData>().Data;
					
					if (!objects.TryGetValue(HoneyObjectUnqualifiedIndentifier, out ObjectData? honeyDefinition) || honeyDefinition is null)
					{
						Logger.LogOnce($"{nameof(OnAssetRequested)} - Failed to find Honey object's data definition", LogLevel.Warn);

						return;
					}

					// Use our texture image from our custom asset which has a color overlay mask next to the honey sprite.
					honeyDefinition.ColorOverlayFromNextIndex = true;
					honeyDefinition.Texture = ModAssetName_HoneyAndLabelMaskTexture;
					honeyDefinition.SpriteIndex = 0;

				}, AssetEditPriority.Late);
			}
		}

		/// <summary>Harmony postfix for `ObjectDataDefinition.CreateFlavoredHoney()`.</summary>
		/// <param name="__result">The returned value from calling the original method.</param>
		/// <param name="ingredient">The ingredient param that was given to the original method.</param>
		private static void CreateFlavoredHoney_ObjectDataDefinition_Postfix(ref SObject __result, SObject? ingredient)
		{
			try
			{
				Color wildHoneyLabelColor = Color.White;
				Color labelColor = TailoringMenu.GetDyeColor(ingredient) ?? wildHoneyLabelColor;

				if (!Config.MoreLabelColorVariety)
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
							labelColor = ShiftColor(labelColor);
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
					Logger.Log($"Failed to color the flavored honey object in {nameof(CreateFlavoredHoney_ObjectDataDefinition_Postfix)}", BuildLogLevel);

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

		/// <summary>
		/// Shift a given color to a close, but distinct color. The new color should ideally be not too close to the next closest primary color.
		/// </summary>
		/// <param name="color">The color to shift</param>
		/// <returns>The shifted color.</returns>
		private static Color ShiftColor(Color color)
		{
			Utility.RGBtoHSL(color.R, color.G, color.B, out double colorHue, out double colorSat, out double colorLum);

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

			Utility.HSLtoRGB(colorHue, colorSat, colorLum, out int colorRed, out int colorGreen, out int colorBlue);
			
			return new Color(colorRed, colorGreen, colorBlue);
		}

		/// <summary>Run one of our custom console commands.</summary>
		/// <param name="command">The name of the command invoked.</param>
		/// <param name="args">The arguments received by the command. Each word after the command name is a separate argument.</param>
		private void DoConsoleCommand(string command, string[] args)
		{
			Logger.Log($"Console command '{command}' invoked with {(args == null || args.Length == 0 ? "no " : "")}params"
				+ (args?.Length > 0 ? ": " + String.Join(", ", args.Select(x => $"'{x}'")) : ""), LogLevel.Trace);

			if (command.Trim().ToLower() == ConsoleCommand_GiveTestHoney)
			{
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
			else if (command.Trim().ToLower() == ConsoleCommand_GiveTestHoneyList)
			{
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
				// If they pass the quantity param as an integery, then create that many honey objects from the test ingredient list and put them in the farmer's inventory.
				// If they also pass the continue param as true, then start outputting from the test ingredient list entry after the last one that was output.
				else if (Int32.TryParse(args[0], out quantity))
				{
					if (quantity > TestHoneyIngredientIdentifiers.Count)
					{
						quantity = TestHoneyIngredientIdentifiers.Count;
					}

					if (args.Length > 1 && !String.IsNullOrWhiteSpace(args[1]))
					{
						string continueParam = args[1].Trim();
						bool shouldContinue = false;

						if (Boolean.TryParse(continueParam, out bool boolParsed))
						{
							shouldContinue = boolParsed;
						}
						else
						{
							switch (continueParam.ToLower())
							{
								case "1":
								case "true":
								case "on":
								case "yes":
									shouldContinue = true;
									break;
							}
						}

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
			else if (command.Trim().ToLower() == ConsoleCommand_ClearInventoryHoney)
			{
				// Simply clear all honey items from the farmer's inventory
				Game1.player.Items.RemoveWhere(x => x?.QualifiedItemId == $"(O){HoneyObjectUnqualifiedIndentifier}");
			}
			else
			{
				Logger.Log($"Unknown console command '{command}'", LogLevel.Warn);
			}
		}
	}
}
