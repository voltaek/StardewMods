using BetterBeehouses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestSync
{
	/// <summary>Contains logic and values to increase compatibility with other mods.</summary>
	internal class ModCompat
	{
		private const bool canVanillaBeeHousesProduceIndoors = false;
		/// <summary>Whether we should check indoors for bee houses.</summary>
		public bool SyncIndoorBeeHouses
		{
			get { return BetterBeehousesModInfo != null ? canBetterBeehousesAllowIndoorBeehouses : canVanillaBeeHousesProduceIndoors; }
		}

		private const int vanillaFlowerRange = 5;
		/// <summary>The max tile range that flowers interact with bee houses at.</summary>
		public int FlowerRange
		{
			get { return BetterBeehousesAPI != null ? BetterBeehousesAPI.GetSearchRadius() : vanillaFlowerRange; }
		}

		// Note - Since Better Beehouses patches the base game's `Utility.findCloseFlower()` method directly we shouldn't need to do anything different
		// to determine the crop (if any) affecting a bee house. We'll just need to keep in mind that we might not get back a *flower* crop necessarily.
		// Ref: https://github.com/tlitookilakin/BetterBeehouses
		// Ref: https://www.nexusmods.com/stardewvalley/mods/10996
		private const string betterBeehousesUniqueID = "tlitookilakin.BetterBeehouses";
		private const string minimumBetterBeehousesVersion = "2.0.0";
		private const bool canBetterBeehousesAllowIndoorBeehouses = true;

		private IModInfo BetterBeehousesModInfo { get; set; } = null;
		private IBetterBeehousesAPI BetterBeehousesAPI { get; set; } = null;

		/// <summary>
		/// Call this to set up compatibility values and APIs. We have to wait until after `Entry()` before attempting to access their APIs, though,
		/// so all mods will be loaded; usually the `GameLaunched` event is a good time.
		/// </summary>
		public void Init()
		{
			// See if the Better Beehouses mod is even installed/loaded
			BetterBeehousesModInfo = ModEntry.Context.Helper.ModRegistry.Get(betterBeehousesUniqueID);

			if (BetterBeehousesModInfo == null)
			{
				ModEntry.Logger.Log($"{nameof(ModCompat)}.{nameof(Init)} - Mod '{betterBeehousesUniqueID}' not found; Not attempting to get {nameof(IBetterBeehousesAPI)}");

				return;
			}

			// The API they had changed for SDV v1.6 / BB v2.0.0, so make sure its a current version.
			if (BetterBeehousesModInfo.Manifest.Version.IsOlderThan(minimumBetterBeehousesVersion))
			{
				ModEntry.Logger.Log($"{nameof(ModCompat)}.{nameof(Init)} - Mod '{betterBeehousesUniqueID}' was found, "
					+ $"but is older than our required minimum version of {minimumBetterBeehousesVersion} to interact with its API");

				return;
			}

			// Try to get Better Beehouses's API
			BetterBeehousesAPI = ModEntry.Context.Helper.ModRegistry.GetApi<IBetterBeehousesAPI>(betterBeehousesUniqueID);

			if (BetterBeehousesAPI == null)
			{
				ModEntry.Logger.Log($"{nameof(ModCompat)}.{nameof(Init)} - Failed to get {nameof(IBetterBeehousesAPI)} even though found mod '{betterBeehousesUniqueID}'", LogLevel.Info);

				return;
			}
			
			ModEntry.Logger.Log($"{nameof(ModCompat)}.{nameof(Init)} - Got {nameof(IBetterBeehousesAPI)}");
		}
	}
}
