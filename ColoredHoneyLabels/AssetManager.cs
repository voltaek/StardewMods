using ColoredHoneyLabels.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;
using System;
using System.Collections.Generic;
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
		private static IRawTextureData? HoneyTexturePiecesRaw = null;

		public const string HoneyTextureBaseImagePath = "assets/honey-texture-base.png";
		public const string HoneyTexturePiecesImagePath = "assets/honey-texture-pieces.png";
		public const string DebugHoneyTextureImagePath = "assets/debug-honey-texture.png";

		private static string SpriteDataAssetName => $"Mods/{ModEntry.ModID}/{nameof(SpriteData)}";

		internal static string DefaultSpriteDataKey => $"{ModEntry.ModID}_default_honey_full_label";
		private static readonly SpriteData DefaultSpriteDataObject = new() {
			DisplayName = "CHL - Full Label (default)",
			TextureName = $"Mods/{ModEntry.ModID}/DefaultHoneyFullLabel",
		};

		private static string DebugTextureName => $"Mods/{ModEntry.ModID}/DebugHoney";

		#if DEBUG
			private const bool AddDebugSpritesToAsset = true;
		#else
			private const bool AddDebugSpritesToAsset = false;
		#endif

		private static SpriteDataAsset? builtIns = null;

		/// <summary>All of our built-in sprite data in a SpriteDataAsset AKA Dictionary.</summary>
		private static SpriteDataAsset BuiltInSpriteDataAsset
		{
			get
			{
				if (builtIns != null)
				{
					return builtIns;
				}

				SpriteDataAsset asset = new()
				{
					{
						DefaultSpriteDataKey,
						DefaultSpriteDataObject
					},
					{
						$"{ModEntry.ModID}_default_honey_mini_label",
						new() {
							DisplayName = "CHL - Mini Label",
							TextureName = $"Mods/{ModEntry.ModID}/DefaultHoneyMiniLabel",
						}
					},
					{
						$"{ModEntry.ModID}_default_honey_full_label_with_lid",
						new() {
							DisplayName = "CHL - Full Label + Lid",
							TextureName = $"Mods/{ModEntry.ModID}/DefaultHoneyFullLabelWithLid",
						}
					},
					{
						$"{ModEntry.ModID}_default_honey_mini_label_with_lid",
						new() {
							DisplayName = "CHL - Mini Label + Lid",
							TextureName = $"Mods/{ModEntry.ModID}/DefaultHoneyMiniLabelWithLid",
						}
					},
					{
						$"{ModEntry.ModID}_default_honey_lid_only",
						new() {
							DisplayName = "CHL - Lid Only",
							TextureName = $"Mods/{ModEntry.ModID}/DefaultHoneyLidOnly",
						}
					},
				};

				if (AddDebugSpritesToAsset)
				{
					// Only add the debug option in debug builds
					asset.Add($"{ModEntry.ModID}_debug", new()
					{
						DisplayName = "Debug",
						TextureName = DebugTextureName,
					});
				}

				builtIns = asset;

				return builtIns;
			}
		}

		private static SpriteDataAsset? allSpriteData = null;

		/// <summary>
		/// The data asset of all sprite data options.
		/// </summary>
		public static SpriteDataAsset AllSpriteData
		{
			get
			{
				allSpriteData ??= Game1.content.Load<SpriteDataAsset>(SpriteDataAssetName);

				return allSpriteData;
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

				string spriteDataKey = ModEntry.Config.SpriteDataKey;

				if (AllSpriteData.TryGetValue(spriteDataKey, out SpriteData? selectedSpriteData))
				{
					if (selectedSpriteData == null || String.IsNullOrWhiteSpace(selectedSpriteData.TextureName) || selectedSpriteData.SpriteIndex < 0)
					{
						Logger.Log($"Selected 'Honey Sprite' data for config option key '{spriteDataKey}' is invalid. Using default honey sprite.", LogLevel.Info);
						Logger.Log($"Selected Sprite Data: {selectedSpriteData}", Constants.BuildLogLevel);

						ModEntry.Config.SpriteDataKey = DefaultSpriteDataKey;

						return DefaultSpriteDataObject;
					}

					return selectedSpriteData;
				}

				// Check if all entries from integration mods have been added before changing their config option.
				if (ModEntry.Config.AreContentPatcherEditsReady)
				{
					Logger.Log($"Selected 'Honey Sprite' data entry not found for config option key '{spriteDataKey}'. Using default honey sprite.", Constants.BuildLogLevel);

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
				allSpriteData = null;
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetReady"/>
		internal static void OnAssetReady(object? sender, AssetReadyEventArgs e)
		{
			// Reload our data asset after any edits were made to it by other mods
			if (e.NameWithoutLocale.IsEquivalentTo(SpriteDataAssetName))
			{
				allSpriteData = Game1.content.Load<SpriteDataAsset>(SpriteDataAssetName);
			}
		}

		/// <inheritdoc cref="IContentEvents.AssetRequested"/>
		internal static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			// Load our custom asset to hold sprite data, with default data in it already
			if (e.NameWithoutLocale.IsEquivalentTo(SpriteDataAssetName))
			{
				// Don't pass our actual dictionary, otherwise integration entries will get mixed in with our built-in ones.
				e.LoadFrom(() => new SpriteDataAsset(BuiltInSpriteDataAsset), AssetLoadPriority.Exclusive);

				return;
			}

			// Load our debug sprites for testing
			if (e.NameWithoutLocale.IsEquivalentTo(DebugTextureName))
			{
				e.LoadFromModFile<Texture2D>(DebugHoneyTextureImagePath, AssetLoadPriority.Exclusive);

				// If we don't return early then we'll try to also load the Debug texture from the list of built-ins.
				return;
			}

			// Check if the asset is one of our built-in honey textures
			List<SpriteData> matchingBuiltInDataAssets = BuiltInSpriteDataAsset
				.Where(x => e.NameWithoutLocale.IsEquivalentTo(x.Value.TextureName))
				.Select(y => y.Value)
				.ToList();

			foreach (SpriteData spriteData in matchingBuiltInDataAssets)
			{
				// If it is, load the base of the texture from our PNG
				e.LoadFromModFile<Texture2D>(HoneyTextureBaseImagePath, AssetLoadPriority.Exclusive);

				HoneyTexturePiecesRaw ??= ModEntry.Context.Helper.ModContent.Load<IRawTextureData>(HoneyTexturePiecesImagePath);

				// Then edit the proper sprite pieces into the texture from our pieces PNG
				e.Edit(asset => {
					IAssetDataForImage editor = asset.AsImage();

					string textureName = spriteData.TextureName ?? String.Empty;
					bool hasFullLabel = textureName.Contains("FullLabel");
					bool hasMiniLabel = textureName.Contains("MiniLabel");
					bool hasLid = textureName.Contains("Lid");

					if (!(hasFullLabel || hasMiniLabel || hasLid))
					{
						Logger.Log($"Built-in sprite data texture name {(spriteData.TextureName == null ? "`null`" : $"'{spriteData.TextureName}'")} "
							+ $"had no label indicators in it. Defaulting to full label.", LogLevel.Info);

						hasFullLabel = true;
					}

					int spriteSize = 16;
					Rectangle labelSlot = new() { X = 16, Y = 0, Width = spriteSize, Height = spriteSize };

					if (hasFullLabel || hasMiniLabel)
					{
						// Patch the bottle overlay for under the mini label onto our honey bottle sprite
						editor.PatchImage(HoneyTexturePiecesRaw, new() { X = 0, Y = 0, Width = spriteSize, Height = spriteSize }, null, PatchMode.Overlay);

						// Patch the inner/mini label into the mask slot
						editor.PatchImage(HoneyTexturePiecesRaw, new() { X = 16, Y = 0, Width = spriteSize, Height = spriteSize }, labelSlot, PatchMode.Overlay);
					}

					if (hasFullLabel)
					{
						// Patch the outer label into the mask slot
						editor.PatchImage(HoneyTexturePiecesRaw, new() { X = 32, Y = 0, Width = spriteSize, Height = spriteSize }, labelSlot, PatchMode.Overlay);
					}

					if (hasLid)
					{
						// Patch the lid into the mask slot
						editor.PatchImage(HoneyTexturePiecesRaw, new() { X = 48, Y = 0, Width = spriteSize, Height = spriteSize }, labelSlot, PatchMode.Overlay);
					}
				});
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
