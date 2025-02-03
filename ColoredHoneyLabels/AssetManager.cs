using ColoredHoneyLabels.Models;
using Force.DeepCloner;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
		private static bool DefaultHoneyColorOverlayFromNextIndex;
		private static string? DefaultHoneyTexture;
		private static int DefaultHoneySpriteIndex;

		private static bool HasRestoredDefaultHoneyData = false;

		private static string SpriteDataAssetName => $"Mods/{ModEntry.ModID}/SpriteData";
		
		#if DEBUG
			private const bool UseDebugSprites = true;
		#else
			private const bool UseDebugSprites = false;
		#endif

		private static readonly ImmutableList<InternalSpriteData> AllInternalSpriteData;

		static AssetManager()
		{
			List<InternalSpriteData> internalData = new()
			{
				new(
					textureName: $"Mods/{ModEntry.ModID}/DefaultHoneyFullLabel",
					displayName: "CHL - Full Label (default)",
					assetDictionaryKey: $"{ModEntry.ModID}_default_honey_full_label",
					imagePath: "assets/honey-full-label.png"
				) {
					// Should only ever be one of these
					IsDefault = true
				},
				new(
					textureName: $"Mods/{ModEntry.ModID}/DefaultHoneyMiniLabel",
					displayName: "CHL - Mini Label",
					assetDictionaryKey: $"{ModEntry.ModID}_default_honey_mini_label",
					imagePath: "assets/honey-mini-label.png"
				),
				new(
					textureName: $"Mods/{ModEntry.ModID}/DefaultHoneyFullLabelWithLid",
					displayName: "CHL - Full Label + Lid",
					assetDictionaryKey: $"{ModEntry.ModID}_default_honey_full_label_with_lid",
					imagePath: "assets/honey-full-label-with-lid.png"
				),
				new(
					textureName: $"Mods/{ModEntry.ModID}/DefaultHoneyMiniLabelWithLid",
					displayName: "CHL - Mini Label + Lid",
					assetDictionaryKey: $"{ModEntry.ModID}_default_honey_mini_label_with_lid",
					imagePath: "assets/honey-mini-label-with-lid.png"
				),
				new(
					textureName: $"Mods/{ModEntry.ModID}/DefaultHoneyLidOnly",
					displayName: "CHL - Lid Only",
					assetDictionaryKey: $"{ModEntry.ModID}_default_honey_lid_only",
					imagePath: "assets/honey-lid-only.png"
				)
			};

			if (UseDebugSprites)
			{
				internalData.Add(new(
					textureName: $"Mods/{ModEntry.ModID}/DebugHoney",
					displayName: "Debug",
					assetDictionaryKey: $"{ModEntry.ModID}_debug",
					imagePath: "assets/honey-debug.png"
				) {
					IsDebug = true
				});
			}

			AllInternalSpriteData = internalData.ToImmutableList();
		}

		internal static string DefaultSpriteDataKey => AllInternalSpriteData.First(x => x.IsDefault).AssetDictionaryKey;
		private static SpriteData DefaultSpriteDataObject => AllInternalSpriteData.First(x => x.IsDefault);

		/// <summary>All of our internal/built-in sprite data in a SpriteDataAsset AKA Dictionary.</summary>
		private static SpriteDataAsset GetInternalSpriteDataAsset
		{
			get
			{
				SpriteDataAsset asset = new();

				foreach (InternalSpriteData internalData in AllInternalSpriteData)
				{
					asset.Add(internalData.AssetDictionaryKey, internalData.ShallowClone<SpriteData>());
				}

				return asset;
			}
		}

		private static SpriteDataAsset? loadedSpriteData = null;

		/// <summary>
		/// The data asset of all sprite data options.
		/// </summary>
		public static SpriteDataAsset LoadedSpriteData
		{
			get
			{
				loadedSpriteData ??= Game1.content.Load<SpriteDataAsset>(SpriteDataAssetName);

				return loadedSpriteData;
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
					Logger.Log($"Selected 'Honey Sprite' config option is blank or missing. Using default honey sprite.", LogLevel.Info);

					ModEntry.Config.SpriteDataKey = DefaultSpriteDataKey;
				}

				if (LoadedSpriteData.TryGetValue(ModEntry.Config.SpriteDataKey, out SpriteData? selectedSpriteData))
				{
					if (selectedSpriteData == null || String.IsNullOrWhiteSpace(selectedSpriteData.TextureName) || selectedSpriteData.SpriteIndex < 0)
					{
						Logger.Log($"Selected 'Honey Sprite' data for config option key '{ModEntry.Config.SpriteDataKey}' is invalid. "
							+ $"Using default honey sprite.", LogLevel.Info);
						Logger.Log($"Selected Sprite Data: {selectedSpriteData}", Constants.BuildLogLevel);
						
						ModEntry.Config.SpriteDataKey = DefaultSpriteDataKey;

						return DefaultSpriteDataObject;
					}

					return selectedSpriteData;
				}

				// Check if all entries from integration mods have been added (AKA their edits have run) before changing the user's config option.
				if (ModEntry.Config.AreContentPatcherEditsReady)
				{
					Logger.Log($"Selected 'Honey Sprite' data entry not found for config option key '{ModEntry.Config.SpriteDataKey}'. "
						+ $"Using default honey sprite.", Constants.BuildLogLevel);

					ModEntry.Config.SpriteDataKey = DefaultSpriteDataKey;
				}

				return DefaultSpriteDataObject;
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetsInvalidated"/>
		internal static void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
		{
			if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(SpriteDataAssetName)))
			{
				loadedSpriteData = null;
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetReady"/>
		internal static void OnAssetReady(object? sender, AssetReadyEventArgs e)
		{
			// Reload our data asset after any edits were made to it by other mods
			if (e.NameWithoutLocale.IsEquivalentTo(SpriteDataAssetName))
			{
				loadedSpriteData = Game1.content.Load<SpriteDataAsset>(SpriteDataAssetName);
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetRequested"/>
		internal static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			// Load our custom asset to hold sprite data, with all of our internal/default data in it already.
			// Other mods can then edit entries into this data to add their own honey sprite options.
			if (e.NameWithoutLocale.IsEquivalentTo(SpriteDataAssetName))
			{
				// Don't pass our actual dictionary, otherwise integration entries will get mixed in with our built-in ones.
				e.LoadFrom(() => GetInternalSpriteDataAsset, AssetLoadPriority.Exclusive);
			}

			// Load any of our internal honey textures when requested
			InternalSpriteData? requestedSpriteData = AllInternalSpriteData.FirstOrDefault(x => e.NameWithoutLocale.IsEquivalentTo(x.TextureName));

			if (requestedSpriteData != null)
			{
				e.LoadFromModFile<Texture2D>(requestedSpriteData.ImagePath, AssetLoadPriority.Exclusive);
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
