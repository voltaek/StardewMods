using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestPredictor.API
{
	public class HoneyHarvestPredictorAPI : IHoneyHarvestPredictorAPI
	{
		// The API-accessing mod's `Manifest`
		private IManifest ModManifest { get; set; }

		public HoneyHarvestPredictorAPI(IModInfo mod)
		{
			ModManifest = mod.Manifest;

			ModEntry.Logger.Log($"Mod {ModManifest.Name} ({ModManifest.UniqueID} {ModManifest.Version}) fetched API", Constants.buildLogLevel);
		}

		private void LogApiMethodRan(string methodName)
		{
			ModEntry.Logger.Log($"Mod {ModManifest.Name} is running {nameof(HoneyHarvestPredictorAPI)}.{methodName}", Constants.buildLogLevel);
		}

		// These have method documentation over in `IHoneyHarvestPredictorAPI`

		public string GetBeeHouseReadyIcon()
		{
			return ModEntry.Config.BeeHouseReadyIcon;
		}

		public int GetFlowerRange()
		{
			return ModEntry.Compat.FlowerRange;
		}

		public void RefreshTrackedReadyBeeHouses()
		{
			LogApiMethodRan(nameof(RefreshTrackedReadyBeeHouses));

			HoneyUpdater.RefreshBeeHouseHeldObjects();
		}

		public void RefreshAll()
		{
			LogApiMethodRan(nameof(RefreshAll));

			HoneyUpdater.RefreshAll();
		}
	}
}
