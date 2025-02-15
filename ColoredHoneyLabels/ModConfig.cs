using ColoredHoneyLabels.Integrations;
using ColoredHoneyLabels.Models;
using StardewModdingAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	public sealed class ModConfig
	{
		public string? SpriteDataKey { get; set; } = AssetManager.DefaultSpriteDataKey;

		public bool MoreLabelColorVariety { get; set; } = false;

		private const int TicksPastGameLaunchedContentPatcherEditsReady = 4;
		private bool HasGameLaunched = false;
		private int TickContentPatcherEditsReady = Int32.MaxValue;

		/// <summary>Content Patcher isn't done applying edits until X ticks past GameLaunched.</summary>
		internal bool AreContentPatcherEditsReady => HasGameLaunched && Game1.ticks >= TickContentPatcherEditsReady;

		/// <summary>Subscribe to event handlers that will register our mod and its config options with GMCM later.</summary>
		internal void ScheduleEventRegistration()
		{
			// Set up Generic Mod Config Menu integration
			ModEntry.Context.Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			ModEntry.Context.Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
		}

		/// <inheritdoc cref="IGameLoopEvents.GameLaunched"/>
		private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
		{
			HasGameLaunched = true;
			TickContentPatcherEditsReady = Game1.ticks + TicksPastGameLaunchedContentPatcherEditsReady;
		}

		/// <inheritdoc cref="IGameLoopEvents.UpdateTicked"/>
		private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
		{
			if (AreContentPatcherEditsReady)
			{
				// Remove this handler now that we've waited long enough.
				ModEntry.Context.Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

				// Register our mod and its config options with GMCM.
				// We have to wait until GameLaunched + X ticks so that other mod's edits to our data asset will have already taken place.
				// Otherwise our `allowedValues` for the 'Honey Sprite' option will only include our initially loaded defaults.
				Register();
			}
		}

		/// <summary>Register our mod and its config options with GMCM.</summary>
		internal void Register()
		{
			// Get Generic Mod Config Menu's API (if it's installed)
			IGenericModConfigMenuApi? configMenu = ModEntry.Context.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

			if (configMenu is null)
			{
				return;
			}

			// Register mod
			configMenu.Register(
				mod: ModEntry.Context.ModManifest,
				reset: () => ModEntry.Config = new(),
				save: () => ModEntry.Context.Helper.WriteConfig(this)
			);

			// Add each config value

			configMenu.AddTextOption(
				mod: ModEntry.Context.ModManifest,
				name: () => "Honey Sprite",
				tooltip: () => "Select the honey sprite to use from this or other mods' compatible options.",
				getValue: () => String.IsNullOrWhiteSpace(SpriteDataKey) ? AssetManager.DefaultSpriteDataKey : SpriteDataKey,
				setValue: value => {
					string? oldValue = SpriteDataKey;
					SpriteDataKey = value;

					if (oldValue != value)
					{
						ModEntry.Logger.Log($"Updated {nameof(SpriteDataKey)} config value via GMCM from {(oldValue == null ? "`null`" : $"'{oldValue}'")} to '{value}'", LogLevel.Debug);

						AssetManager.RefreshHoneyData();
					}
				},
				// NOTE - Since these values are only assigned once, our list of CP-loaded-and-edited sprite data needs to already have any edits from other mods in it.
				// If we register this immediately at GameLaunched then Content Patcher will only have done the initial load of our data asset,
				// but not yet applied edits from other mods to it, yet, so we have to wait a minimum number of ticks past GameLaunched to register this option
				// so that it gets the loaded data plus edited-in data assigned to it.
				allowedValues: AssetManager.LoadedSpriteData.Keys.ToArray(),
				formatAllowedValue: (value) => {
					if (AssetManager.LoadedSpriteData.TryGetValue(value, out SpriteData? data))
					{
						return data?.DisplayName ?? value;
					}

					return value;
				}
			);

			configMenu.AddBoolOption(
				mod: ModEntry.Context.ModManifest,
				name: () => "More Label Color Variety",
				tooltip: () => "Enable this to slightly shift the label color of some honey types, resulting in a larger variety of label colors.",
				getValue: () => MoreLabelColorVariety,
				setValue: value => {
					bool oldValue = MoreLabelColorVariety;
					MoreLabelColorVariety = value;

					if (oldValue != value)
					{
						ModEntry.Logger.Log($"Updated {nameof(MoreLabelColorVariety)} config value via GMCM from '{oldValue}' to '{value}'", LogLevel.Debug);

						// If they changed the value, recalc all colors so they match the currently selected option value
						ColorManager.RefreshAllHoneyColors();
					}
				}
			);
		}
	}
}
