﻿using HoneyHarvestSync.API;
using HoneyHarvestSync.Integrations;
using StardewModdingAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestSync
{
    internal sealed class ModEntry : Mod
	{
		/// <summary>A reference to our mod's instantiation to use everywhere.</summary>
		internal static ModEntry Context { get; private set; }

		/// <summary>A reference to the Mod's logger so we can use it everywhere.</summary>
		internal static IMonitor Logger { get; private set; }

		/// <summary>The mod configuration from the player.</summary>
		internal static ModConfig Config { get; private set; }

		/// <summary>Logic and values to increase compatibility with other mods.</summary>
		internal static ModCompat Compat { get; private set; }

		/// <summary>The mod entry point, called after the mod is first loaded.</summary>
		/// <param name="helper">Provides simplified APIs for writing mods.</param>
		public override void Entry(IModHelper helper)
		{
			Context = this;
			Logger = Monitor;

			// Read user's config
			Config = Helper.ReadConfig<ModConfig>();

			// Default compat values
			Compat = new();

			// Rig up event handler to set up Generic Mod Config Menu integration
			Helper.Events.GameLoop.GameLaunched += OnGameLaunched;

			// Rig up the event handlers we need to do proper tracking of bee houses and flowers
			Helper.Events.GameLoop.DayStarted += HoneyUpdater.OnDayStarted;
			Helper.Events.GameLoop.TimeChanged += HoneyUpdater.OnTimeChanged;
			Helper.Events.GameLoop.OneSecondUpdateTicked += HoneyUpdater.OnOneSecondUpdateTicked;
			Helper.Events.World.ObjectListChanged += HoneyUpdater.OnObjectListChanged;
			Helper.Events.World.LocationListChanged += HoneyUpdater.OnLocationListChanged;
		}

		/// <summary>Provide API access to other mods.</summary>
		/// <param name="mod">The mod accessing the provided API.</param>
		/// <returns>An instance of `HoneyHarvestSyncAPI`, which conforms to `IHoneyHarvestSyncAPI`.</returns>
		public override object GetApi(IModInfo mod)
		{
			return new HoneyHarvestSyncAPI(mod);
		}

		/// <summary>Event handler for when the game launches.</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
		{
			try
			{
				// Set up our mod compatibility stuff
				Compat.Init();
			}
			catch (Exception ex)
			{
				Monitor.Log($"{nameof(OnGameLaunched)} failed to initialize values and APIs to handle compatibility with other mods. Exception:\n{ex}", LogLevel.Warn);
			}

			// Get Generic Mod Config Menu's API (if it's installed)
			IGenericModConfigMenuApi configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

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
			configMenu.AddTextOption(
				mod: ModManifest,
				name: () => "Bee House Ready Icon",
				tooltip: () => "Controls icon type shown above bee houses with honey ready. 'Flower' (default) - flower that will flavor the honey. 'Honey' - artisan honey you'll get (artisan icons not included).",
				getValue: () => Config.BeeHouseReadyIcon,
				setValue: value => {
					string oldValue = Config.BeeHouseReadyIcon;
					Config.BeeHouseReadyIcon = value;

					Monitor.Log($"Updated {nameof(Config.BeeHouseReadyIcon)} config value via GMCM from '{oldValue}' to '{value}'", LogLevel.Debug);

					HoneyUpdater.RefreshBeeHouseHeldObjects();
				},
				allowedValues: Enum.GetNames<ModConfig.ReadyIcon>()
			);
		}
	}
}
