using ColoredHoneyLabels.Integrations;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;
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

		/// <summary>The mod entry point, called after the mod is first loaded.</summary>
		/// <param name="helper">Provides simplified APIs for writing mods.</param>
		public override void Entry(IModHelper helper)
		{
			Context = this;
			Logger = Monitor;

			// Read user's config
			Config = Helper.ReadConfig<ModConfig>();



			// TODO - Test honey from before mod install and after mod removal



			// Rig up event handler to set up Generic Mod Config Menu integration
			Helper.Events.GameLoop.GameLaunched += OnGameLaunched;

			// Add our custom asset and modify the honey object's definition
			Helper.Events.Content.AssetRequested += OnAssetRequested;

			Patches.ApplyPatches();
			ConsoleCommands.AddCommands();
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
	}
}
