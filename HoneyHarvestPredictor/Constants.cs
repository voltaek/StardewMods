using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestPredictor
{
	public class Constants
	{
		/// <summary>Minutes total from when the farmer/player wakes up (6am) until the latest they can be awake (2am).</summary>
		public const int maxMinutesAwake = 1200;

		/// <summary>
		/// Time the farmer wakes up, but in the 24 hour integer form used in the properties of `TimeChangedEventArgs`.
		/// Examples: 600 is 6am and 1300 is 1pm.
		/// </summary>
		public const int startOfDayTime = 600;

		/// <summary>The globally unique identifier for Bee House machines.</summary>
		public const string beeHouseQualifiedItemID = "(BC)10";

		/// <summary>The globally unique identifier for Garden Pot AKA `IndoorPot`.</summary>
		public const string gardenPotQualifiedItemID = "(BC)62";

		/// <summary>The HoeDirt class's private netfield that backs its 'crop' property.</summary>
		public const string HoeDirtNetCropPropertyName = "netCrop";

		/// <summary>
		/// This is the 'Name' property that the HoeDirt class's 'netCrop' property gets registered in a HoeDirt instances's 'NetFields' collection property.
		/// Ref: Netcode/NetFields.cs -> GetNameForInstance() and AddField()
		/// </summary>
		public const string HoeDirtNetCropNetFieldName = $"{nameof(TerrainFeature)} ({nameof(HoeDirt)}): {HoeDirtNetCropPropertyName}";

		/// <summary>The console command for refreshing things.</summary>
		public const string consoleCommandRefresh = "hhp_refresh";

		// For debug builds, show log messages as DEBUG so they show in the SMAPI console.
		#if DEBUG
		public const LogLevel buildLogLevel = LogLevel.Debug;
		#else
		public const LogLevel buildLogLevel = LogLevel.Trace;
		#endif

		public static string ModDataKey_BeeHouseReadyTempDisplayObject
		{
			get { return $"{ModEntry.Context.ModManifest.UniqueID}_BeeHouseReadyTempDisplayObject"; }
		}

		public static string ModDataKey_TrackingGuid
		{
			get { return $"{ModEntry.Context.ModManifest.UniqueID}_TrackingGuid"; }
		}
	}
}
