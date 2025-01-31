using StardewModdingAPI.Events;
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
		internal static ModConfig Config { get; set; }

		#nullable enable

		/// <summary>This mod's `UniqueID` from its mod manifest.</summary>
		internal static string ModID = null!;

		/// <inheritdoc cref="IMod.Entry"/>
		public override void Entry(IModHelper helper)
		{
			Context = this;
			Logger = Monitor;
			ModID = ModManifest.UniqueID;

			// Read user's config
			Config = Helper.ReadConfig<ModConfig>();

			// Set up Generic Mod Config Menu integration
			Helper.Events.GameLoop.GameLaunched += OnGameLaunched;

			// Reset some things between save games
			Helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

			// Add our custom asset and modify the honey object's definition
			Helper.Events.Content.AssetRequested += AssetManager.OnAssetRequested;
			Helper.Events.Content.AssetsInvalidated += AssetManager.OnAssetsInvalidated;

			// Apply Harmony patches so that honey items are created as the `ColoredObject` type and get their color assigned to them.
			Patches.ApplyPatches();

			// Register our custom console commands
			ConsoleCommands.AddCommands();
		}

		/// <inheritdoc cref="IGameLoopEvents.GameLaunched"/>
		private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
		{
			Config.Register(Helper, ModManifest);
		}

		/// <inheritdoc cref="IGameLoopEvents.ReturnedToTitle"/>
		private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
		{
			AssetManager.ResetUndoHoneyColors();
		}
	}
}
