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

			// Read user's config and schedule to register our mod and its config options
			Config = Helper.ReadConfig<ModConfig>();
			Config.ScheduleRegistration();

			// Manage our custom asset and modifications to the honey object's definition
			Helper.Events.Content.AssetRequested += AssetManager.OnAssetRequested;
			Helper.Events.Content.AssetsInvalidated += AssetManager.OnAssetsInvalidated;
			Helper.Events.Content.AssetReady += AssetManager.OnAssetReady;

			// Reset some things between save games
			Helper.Events.GameLoop.ReturnedToTitle += AssetManager.OnReturnedToTitle;

			// Apply Harmony patches so that honey items are created as the `ColoredObject` type and get their color assigned to them.
			Patches.ApplyPatches();

			// Register our custom console commands
			ConsoleCommands.AddCommands();
		}
	}
}
