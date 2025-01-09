using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley.GameData.Machines;
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

		public const string honeyObjectIndentifier = "340";

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
				Logger.Log($"An error occurred while registering a harmony patch for {nameof(ObjectDataDefinition)}.{nameof(ObjectDataDefinition.CreateFlavoredHoney)}. "
					+ $"Exception Details:\n{ex}", LogLevel.Warn);
			}
		}

		private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			if (e.NameWithoutLocale.IsEquivalentTo(ModAssetName_HoneyAndLabelMaskTexture))
			{

				// TODO - Test loading this with a higher priority in another mod to see if it get overridden properly


				e.LoadFromModFile<Texture2D>(ModAssetPath_HoneyAndLabelMaskTexture, AssetLoadPriority.Low);
			}

			if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
			{
				e.Edit(asset => {
					IDictionary<string, ObjectData> objects = asset.AsDictionary<string, ObjectData>().Data;
					
					if (!objects.TryGetValue(honeyObjectIndentifier, out ObjectData? honeyDefinition) || honeyDefinition is null)
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
			Color wildHoneyLabelColor = Color.White;
			Color labelColor = TailoringMenu.GetDyeColor(ingredient) ?? wildHoneyLabelColor;


			// TODO - wrap entirely of this method's contents in a try/catch and log the exception as pointing to this mod

			// LEFT OFF HERE


			// TODO MAYBE - Vary brightness of label color depending on flower name to add more color variations.
			// Probably a lighter variant and a darker variant of each color would be more than plenty, something like ~25% lighter or darker.
			// Would need to test many variants to make sure they all work alright, since some colors might not work well
			// with the current label mask when the base color varies too much from what it's been dialed-in to handle.
			// Base game has these that might be helpful in addition to the XNA functions:
			// Utility.RGBtoHSL()
			// Utility.HSLtoRGB()


			if (!ColoredObject.TrySetColor(__result, labelColor, out ColoredObject coloredHoney))
			{
				Logger.Log($"Failed to color the flavored honey object in {nameof(CreateFlavoredHoney_ObjectDataDefinition_Postfix)}", BuildLogLevel);

				return;
			}

			__result = coloredHoney;
		}
	}
}
