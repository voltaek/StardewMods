using ColoredHoneyLabels.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SpriteDataAsset = System.Collections.Generic.Dictionary<string, ColoredHoneyLabels.Models.SpriteData>;

namespace ColoredHoneyLabels
{
	internal static class AssetManager
	{
		/// <summary>Shorthand for the main logger instance.</summary>
		private static IMonitor Logger => ModEntry.Logger;

		private static bool HasCollectedDefaultHoneyObjectDataValues = false;
		internal static bool DefaultHoneyColorOverlayFromNextIndex;
		internal static string? DefaultHoneyTexture;
		internal static int DefaultHoneySpriteIndex;

		internal static bool HasRestoredDefaultHoneyData = false;

		internal static string SpriteDataAssetName => $"Mods/{ModEntry.ModID}/{nameof(SpriteData)}";

		public const string DefaultSpritesImagePath = "assets/default-sprites.png";
		public static string DefaultSpritesTextureName => $"Mods/{ModEntry.ModID}/DefaultSprites";

		internal static string DefaultSpriteDataKey => $"{ModEntry.ModID}_default_with_colored_label";
		private static readonly SpriteData DefaultSpriteData = new() {
			DisplayName = "Default with Colored Label",
			TextureName = DefaultSpritesTextureName,
		};

		public const string DebugSpritesImagePath = "assets/debug-sprites.png";
		public static string DebugSpritesTextureName => $"Mods/{ModEntry.ModID}/DebugSprites";

		internal static string DebugSpriteDataKey => $"{ModEntry.ModID}_debug";
		private static readonly SpriteData DebugSpriteData = new()
		{
			DisplayName = "DEBUG",
			TextureName = DebugSpritesTextureName,
		};

		#if DEBUG
			private const bool AddDebugSpritesToAsset = true;
		#else
			private const bool AddDebugSpritesToAsset = false;
		#endif

		/// <summary>
		/// Our custom data structure with our default entry (or entries when in debug) already added.
		/// </summary>
		private static SpriteDataAsset DefaultSpriteDataAsset
		{
			get
			{
				SpriteDataAsset asset = new() {
					{ DefaultSpriteDataKey, DefaultSpriteData },
				};

				if (AddDebugSpritesToAsset)
				{
					// Only add the debug option in debug builds
					asset.Add(DebugSpriteDataKey, DebugSpriteData);
				}

				return asset;
			}
		}

		private static SpriteDataAsset? spriteData = null;

		/// <summary>
		/// The data asset of all sprite data options.
		/// </summary>
		public static SpriteDataAsset AllSpriteData
		{
			get
			{
				spriteData ??= Game1.content.Load<SpriteDataAsset>(SpriteDataAssetName);

				return spriteData;
			}
		}

		/// <summary>
		/// Returns the currently-selected (or default if none are) sprite data.
		/// </summary>
		private static SpriteData SelectedSpriteData
		{
			get
			{
				if (String.IsNullOrWhiteSpace(ModEntry.Config.SpriteDataKey))
				{
					Logger.Log($"Selected Honey Sprite data key is missing. Resetting selected Honey Sprite to the default.", LogLevel.Info);

					ModEntry.Config.SpriteDataKey = DefaultSpriteDataKey;

					RefreshHoneyData();
				}

				string spriteDataKey = ModEntry.Config.SpriteDataKey;

				if (AllSpriteData.TryGetValue(spriteDataKey, out SpriteData? selectedSpriteData))
				{
					if (selectedSpriteData == null || String.IsNullOrWhiteSpace(selectedSpriteData.TextureName) || selectedSpriteData.SpriteIndex < 0)
					{
						Logger.Log($"Selected Honey Sprite data (entry key '{spriteDataKey}') is invalid. "
							+ $"Resetting selected Honey Sprite to the default.", LogLevel.Info);
						Logger.Log($"Selected Sprite Data: {selectedSpriteData}", Constants.BuildLogLevel);

						ModEntry.Config.SpriteDataKey = DefaultSpriteDataKey;

						RefreshHoneyData();

						return DefaultSpriteData;
					}

					return selectedSpriteData;
				}

				Logger.Log($"{nameof(SelectedSpriteData)} - Honey Sprite data entry (key '{spriteDataKey}') not found. "
					+ $"Returning default Honey Sprite data.", Constants.BuildLogLevel);

				return DefaultSpriteData;
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetsInvalidated"/>
		internal static void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
		{
			if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(SpriteDataAssetName)))
			{
				spriteData = null;
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetReady"/>
		internal static void OnAssetReady(object? sender, AssetReadyEventArgs e)
		{
			// Reload our data asset after any edits were made to it by other mods
			if (e.NameWithoutLocale.IsEquivalentTo(SpriteDataAssetName))
			{
				spriteData = Game1.content.Load<SpriteDataAsset>(SpriteDataAssetName);
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetRequested"/>
		internal static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			// Load our custom asset to hold sprite data, with default data in it already
			if (e.NameWithoutLocale.IsEquivalentTo(SpriteDataAssetName))
			{
				e.LoadFrom(() => DefaultSpriteDataAsset, AssetLoadPriority.Exclusive);
			}

			// Load our default sprites into a texture for later
			if (e.NameWithoutLocale.IsEquivalentTo(DefaultSpritesTextureName))
			{
				e.LoadFromModFile<Texture2D>(DefaultSpritesImagePath, AssetLoadPriority.Exclusive);
			}

			// Load our debug sprites for testing
			if (e.NameWithoutLocale.IsEquivalentTo(DebugSpritesTextureName))
			{
				e.LoadFromModFile<Texture2D>(DebugSpritesImagePath, AssetLoadPriority.Exclusive);
			}

			// When Objects data is loaded, make our edits to the Honey object definition.
			if (e.NameWithoutLocale.IsEquivalentTo(Constants.HoneyObjectParentAssetName))
			{
				e.Edit(asset => {
					IDictionary<string, ObjectData> objects = asset.AsDictionary<string, ObjectData>().Data;

					if (!objects.TryGetValue(Constants.HoneyObjectUnqualifiedIndentifier, out ObjectData? honeyDefinition) || honeyDefinition is null)
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
					if (ConsoleCommands.HasRunUndoHoneyColorsCommand)
					{
						// Only restore it once.
						if (!HasRestoredDefaultHoneyData)
						{
							honeyDefinition.ColorOverlayFromNextIndex = DefaultHoneyColorOverlayFromNextIndex;
							honeyDefinition.Texture = DefaultHoneyTexture;
							honeyDefinition.SpriteIndex = DefaultHoneySpriteIndex;

							HasRestoredDefaultHoneyData = true;

							Logger.Log($"Restored default Honey object data definition values", LogLevel.Info);
						}
					}
					else
					{
						// The normal case: Use the currently-selected option in our custom data asset to get the config values
						// for rendering the honey object's sprite when it's a `ColoredObject`.
						SpriteData selected = SelectedSpriteData;
						honeyDefinition.ColorOverlayFromNextIndex = selected.ColorOverlayFromNextIndex;
						honeyDefinition.Texture = selected.TextureName;
						honeyDefinition.SpriteIndex = selected.SpriteIndex;
					}

				}, AssetEditPriority.Late);
			}
		}

		/// <inheritdoc cref="IGameLoopEvents.ReturnedToTitle"/>
		internal static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
		{
			ResetUndoHoneyColors();
		}

		/// <summary>
		/// Refresh the honey object data definition so our current configuration of it applies.
		/// </summary>
		internal static void RefreshHoneyData()
		{
			ModEntry.Context.Helper.GameContent.InvalidateCache(Constants.HoneyObjectParentAssetName);
		}

		/// <summary>
		/// Reset tracked values that were changed when the user ran the console command to undo all colored honey objects.
		/// </summary>
		internal static void ResetUndoHoneyColors()
		{
			ConsoleCommands.ResetUndoHoneyColors();

			if (HasRestoredDefaultHoneyData)
			{
				HasRestoredDefaultHoneyData = false;

				RefreshHoneyData();
			}
		}
	}
}
