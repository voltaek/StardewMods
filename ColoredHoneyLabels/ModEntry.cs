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
		public static readonly string HoneyObjectQualifiedIndentifier = $"(O){HoneyObjectUnqualifiedIndentifier}";
		public const string ModAssetPath_HoneyAndLabelMaskTexture = "assets/honey-and-label-mask.png";

		public static string ModAssetName_HoneyAndLabelMaskTexture
		{
			get { return $"Mods/{Context.ModManifest.UniqueID}/HoneyAndLabelMask"; }
		}

		public static string ModDataKey_HasColoredLabel
		{
			get { return $"{Context.ModManifest.UniqueID}_has_colored_label"; }
		}

		// NOTE - These are all reset in our `OnReturnedToTitle` handler
		internal static bool DefaultHoneyColorOverlayFromNextIndex;
		internal static string? DefaultHoneyTexture;
		internal static int DefaultHoneySpriteIndex;
		private static bool HasCollectedDefaultHoneyObjectDataValues = false;
		internal static bool HasRunUndoHoneyColorsCommand = false;
		internal static bool HasRestoredDefaultHoneyData = false;

		/// <inheritdoc cref="IMod.Entry"/>
		public override void Entry(IModHelper helper)
		{
			Context = this;
			Logger = Monitor;

			// Read user's config
			Config = Helper.ReadConfig<ModConfig>();

			// Set up Generic Mod Config Menu integration
			Helper.Events.GameLoop.GameLaunched += OnGameLaunched;

			// Reset things between save games
			Helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

			// Add our custom asset and modify the honey object's definition
			Helper.Events.Content.AssetRequested += OnAssetRequested;

			// Apply Harmony patches so that honey items are created as the `ColoredObject` type and get their color assigned to them.
			Patches.ApplyPatches();

			// Register our custom console commands
			ConsoleCommands.AddCommands();
		}

		/// <inheritdoc cref="IGameLoopEvents.GameLaunched"/>
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

		/// <inheritdoc cref="IGameLoopEvents.ReturnedToTitle"/>
		private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
		{
			DefaultHoneyColorOverlayFromNextIndex = default;
			DefaultHoneyTexture = default;
			DefaultHoneySpriteIndex = default;
			HasCollectedDefaultHoneyObjectDataValues = false;
			HasRunUndoHoneyColorsCommand = false;
			HasRestoredDefaultHoneyData = false;
		}

		/// <inheritdoc cref="IContentEvents.AssetRequested"/>
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
						// If we can't edit the honey object data, then when our Harmony patch makes honey items be a `ColoredObject` type,
						// it should just "tint" the entire default honey sprite itself due to `ColorOverlayFromNextIndex` being `false` by default.
						Logger.LogOnce($"{nameof(OnAssetRequested)} - Failed to find Honey object's data definition. "
							+ $"Will be unable to customize honey item labels, but honey items may still get some color applied to them.", LogLevel.Warn);

						return;
					}

					// Hold onto these in case we need to restore them, such as if the Undo Honey Colors command is run.
					if (!HasCollectedDefaultHoneyObjectDataValues)
					{
						DefaultHoneyColorOverlayFromNextIndex = honeyDefinition.ColorOverlayFromNextIndex;
						DefaultHoneyTexture = honeyDefinition.Texture;
						DefaultHoneySpriteIndex = honeyDefinition.SpriteIndex;

						HasCollectedDefaultHoneyObjectDataValues = true;
					}

					// If the user has run the Undo Honey Colors command, then restore the honey object data default values.
					if (HasRunUndoHoneyColorsCommand)
					{
						// Only restore it once.
						if (!HasRestoredDefaultHoneyData)
						{
							honeyDefinition.ColorOverlayFromNextIndex = DefaultHoneyColorOverlayFromNextIndex;
							honeyDefinition.Texture = DefaultHoneyTexture;
							honeyDefinition.SpriteIndex = DefaultHoneySpriteIndex;

							HasRestoredDefaultHoneyData = true;

							Logger.Log($"{nameof(OnAssetRequested)} - Restored default Honey object data definition values", LogLevel.Info);
						}
					}
					else
					{
						// The normal case: Use our texture image from our custom asset which has a color overlay mask next to the honey sprite.
						honeyDefinition.ColorOverlayFromNextIndex = true;
						honeyDefinition.Texture = ModAssetName_HoneyAndLabelMaskTexture;
						honeyDefinition.SpriteIndex = 0;
					}

				}, AssetEditPriority.Late);
			}
		}
	}
}
