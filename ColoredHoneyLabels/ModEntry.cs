using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
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

		#nullable enable

		#if DEBUG
			// For debug builds, show log messages as DEBUG so they show in the SMAPI console.
			public const LogLevel BuildLogLevel = LogLevel.Debug;
		#else
			public const LogLevel BuildLogLevel = LogLevel.Trace;
		#endif

		public const string honeyObjectUnqualifiedIndentifier = "340";


		// TODO - Make this a GMCM option (so also add GMCM integration) and default to OFF

		public const bool shouldVaryColors = true;


		public static string ModAssetName_HoneyAndLabelMaskTexture
		{
			get { return $"Mods/{Context.ModManifest.UniqueID}/HoneyAndLabelMask"; }
		}

		public static string ModAssetPath_HoneyAndLabelMaskTexture
		{
			get { return "assets/honey-and-label-mask.png"; }
		}

		/// <summary>The mod entry point, called after the mod is first loaded.</summary>
		/// <param name="helper">Provides simplified APIs for writing mods.</param>
		public override void Entry(IModHelper helper)
		{
			Context = this;
			Logger = Monitor;


			// TODO - Replace placeholder Nexus update key in project file


			helper.Events.Content.AssetRequested += OnAssetRequested;

			Harmony harmony = new(helper.ModContent.ModID);

			try
			{
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
		}

		private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			if (e.NameWithoutLocale.IsEquivalentTo(ModAssetName_HoneyAndLabelMaskTexture))
			{

				// TODO - Test loading this with a higher priority from inside another mod to see if it get overridden with the asset from that mod properly


				e.LoadFromModFile<Texture2D>(ModAssetPath_HoneyAndLabelMaskTexture, AssetLoadPriority.Low);
			}

			if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
			{
				e.Edit(asset => {
					IDictionary<string, ObjectData> objects = asset.AsDictionary<string, ObjectData>().Data;
					
					if (!objects.TryGetValue(honeyObjectUnqualifiedIndentifier, out ObjectData? honeyDefinition) || honeyDefinition is null)
					{
						Logger.LogOnce($"{nameof(OnAssetRequested)} - Failed to find Honey object's data definition", LogLevel.Warn);

						return;
					}

					honeyDefinition.ColorOverlayFromNextIndex = true;
					honeyDefinition.Texture = ModAssetName_HoneyAndLabelMaskTexture;
					honeyDefinition.SpriteIndex = 0;

				}, AssetEditPriority.Late);
			}
		}

		private static void CreateFlavoredHoney_ObjectDataDefinition_Postfix(ref SObject __result, SObject? ingredient)
		{
			try
			{
				Color wildHoneyLabelColor = Color.White;
				Color labelColor = TailoringMenu.GetDyeColor(ingredient) ?? wildHoneyLabelColor;

				if (!shouldVaryColors)
				{
					Logger.VerboseLog($"Label color: {labelColor.R} {labelColor.G} {labelColor.B} (RGB)");
				}
				else
				{
					if (!String.IsNullOrWhiteSpace(ingredient?.BaseName))
					{
						// Vary color based on the ingredient/honey flavor source.
						int variant = ingredient.BaseName.ToCharArray().Select(Convert.ToInt32).Sum() % 2;
						Logger.VerboseLog($"Ingredient '{ingredient.BaseName}' with {ingredient.GetContextTags().FirstOrDefault(x => x.StartsWith("color_"))}");

						if (variant == 0)
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
	}
}
