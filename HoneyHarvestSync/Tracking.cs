using Netcode;
using Microsoft.Xna.Framework;
using StardewValley.TerrainFeatures;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewValley.Extensions;

namespace HoneyHarvestSync
{
	internal static class Tracking
	{
		/// <summary>
		/// Holds actions to clean up event listeners on tracked objects, organized by the a GUID assigned (in modData) to the tracked object and its location.
		/// </summary>
		private static readonly Dictionary<string, Dictionary<Guid, Action>> trackingListenersCleanupActions = new();

		/// <summary>Shorthand for the main logger instance.</summary>
		private static IMonitor Logger
		{
			get { return ModEntry.Logger; }
		}

		/// <summary>
		/// Shorthand method for creating a standard log entry, depending on debug build or not.
		/// </summary>
		/// <param name="message">The message to log.</param>
		private static void Log(string message) => Utilities.Log(message);

		private static string VerboseStart
		{
			get { return Utilities.VerboseStart; }
		}

		// Either get the existing GUID off an object or assign it one to then return
		private static Guid GetTrackingGuid(IHaveModData tracked)
		{
			if (!tracked.modData.Keys.Contains(Constants.ModDataKey_TrackingGuid))
			{
				tracked.modData.Add(Constants.ModDataKey_TrackingGuid, Guid.NewGuid().ToString());
			}

			return Guid.Parse(tracked.modData[Constants.ModDataKey_TrackingGuid]);
		}

		/// <summary>
		/// Adds a listener cleanup action to our by-location collection of them.
		/// If one exists for the given GUID, that action is ran before being replaced with the given one in the collection.
		/// </summary>
		/// <param name="locationName">The name of the location to store the action under.</param>
		/// <param name="index">The Guid of the object this cleanup action is for.</param>
		/// <param name="action">The action to do the cleanup AKA listener unsubscribing.</param>
		private static void AddListenerCleanup(string locationName, Guid index, Action action)
		{
			if (!trackingListenersCleanupActions.ContainsKey(locationName))
			{
				trackingListenersCleanupActions.Add(locationName, new());
			}
			else if (trackingListenersCleanupActions[locationName].ContainsKey(index))
			{
				// Run the removal before we replace it
				CleanupAllListeners(locationName, index);
			}

			trackingListenersCleanupActions[locationName].Add(index, action);
		}

		/// <summary>
		/// Removes the listener cleanup action from our tracking collection without running it.
		/// Only run this if we've already unsubscribed the associated listener.
		/// </summary>
		/// <param name="locationName">The location name the Guid is under.</param>
		/// <param name="index">The Guid of entry to remove; is the Guid of the object the cleanup action is for.</param>
		/// <returns></returns>
		private static bool RemoveListenerCleanup(string locationName, Guid index)
		{
			if (!trackingListenersCleanupActions.ContainsKey(locationName))
			{
				return false;
			}

			return trackingListenersCleanupActions[locationName].Remove(index);
		}


		/// <summary>
		/// Helper to allow us to provide any object and have its tracking-removing anon function ran and then removed from our collection of them
		/// </summary>
		/// <param name="tracked">The tracked object of which to run its listener unsubscribing action.</param>
		/// <param name="locationName">(Optional) The location name the object is indexed under, if known.</param>
		internal static void CleanupListener(IHaveModData tracked, string locationName = null)
		{
			if (!tracked.modData.Keys.Contains(Constants.ModDataKey_TrackingGuid))
			{
				Logger.VerboseLog($"{nameof(CleanupListener)} - Attempted to run tracking removal when no modData entry for tracking GUID exists on given object");

				return;
			}

			Guid trackingGuid = Guid.Parse(tracked.modData[Constants.ModDataKey_TrackingGuid]);

			CleanupAllListeners(locationName, trackingGuid);
		}

		/// <summary>
		/// Runs listener cleanup actions and then removes them from the cleanup action collection.
		/// This can be run for all locations, just one location, just one object's GUID, or just one object's GUID at a particular location.
		/// </summary>
		/// <param name="locationName">(Optional) The location to limit the cleanup to.</param>
		/// <param name="trackingGuid">(Optional) The object GUID to limit the cleanup to.</param>
		internal static void CleanupAllListeners(string locationName = null, Guid trackingGuid = default)
		{
			// Wrapper to catch any exceptions from the unsubscribing and log them
			Action<Action> runActionTryCatch = (Action act) => {
				try
				{
					act();
				}
				catch (Exception ex)
				{
					StringBuilder sb = new();
					while (ex != null)
					{
						sb.Append($"{ex.GetType().Name} - {ex.Message}\n\nStack Trace: {ex.StackTrace}\n\n");
						ex = ex.InnerException;
					}
					Log($"{nameof(CleanupAllListeners)} - Exception while cleaning up:\n{sb.ToString()}");
				}
			};

			// If we have both params, then we don't need to search by looping
			if (locationName != null && trackingGuid != Guid.Empty)
			{
				if (!trackingListenersCleanupActions.ContainsKey(locationName) || !trackingListenersCleanupActions[locationName].ContainsKey(trackingGuid))
				{
					Log($"{nameof(CleanupAllListeners)} - Tried to cleanup non-existant listener at {locationName} with GUID {trackingGuid}");

					return;
				}

				runActionTryCatch(trackingListenersCleanupActions[locationName][trackingGuid]);

				trackingListenersCleanupActions[locationName].Remove(trackingGuid);

				return;
			}

			foreach (KeyValuePair<string, Dictionary<Guid, Action>> locationEntry in trackingListenersCleanupActions)
			{
				if (locationName != null && locationEntry.Key != locationName)
				{
					continue;
				}

				foreach (KeyValuePair<Guid, Action> actionEntry in locationEntry.Value)
				{
					if (trackingGuid != Guid.Empty && actionEntry.Key != trackingGuid)
					{
						continue;
					}

					runActionTryCatch(actionEntry.Value);

					// If we are removing by GUID, do that removal in-loop
					if (trackingGuid != Guid.Empty)
					{
						trackingListenersCleanupActions[locationEntry.Key].Remove(actionEntry.Key);

						break;
					}
				}
			}

			if (trackingGuid == Guid.Empty)
			{
				if (locationName != null)
				{
					trackingListenersCleanupActions.Remove(locationName);
				}
				else
				{
					trackingListenersCleanupActions.Clear();
				}
			}
		}

		/// <summary>
		/// Adds the given crop (typically a flower) to our tracking so we can keep bee houses showing their current honey-flavor source (when ready).
		/// </summary>
		/// <param name="crop">The crop to track. Not necessarily a flower when other mods are involved.</param>
		/// <param name="location">The map that the crop is on.</param>
		/// <param name="beeHouse">The bee house the crop is affecting the honey of. Currently only used for logging purposes.</param>
		/// <param name="honeyFlavorSourceHarvestName">The name of the harvest item of the crop, if it was able to be determined. Should be an empty string if not.</param>
		/// <returns>True if we added the crop to tracking. False if the crop was A) already being tracked or B) we couldn't add it to be tracked.</returns>
		internal static bool TrackHoneyFlavorSource(Crop crop, GameLocation location, SObject beeHouse, string honeyFlavorSourceHarvestName)
		{
			// Make sure the `Dirt` property is set. Mods that support more than just crops as honey "flavor" sources can return `Crop` instances with minimal properties set on them.
			// Mods like "Better Beehouses" create `Crop` objects for all the non-`Crop` things that they let bee houses use for flavoring honey,
			// and for that mod, they don't set the `Dirt` property (since many of them don't/can't have an associated one).
			// A crop with dirt associated to it can be either in the ground or in an "Garden Pot" AKA `IndoorPot`, now that vanilla SD v1.6.6+ supports flowers in pots flavoring honey, too.
			if (crop.Dirt != null)
			{
				// Track the tile location of the `HoeDirt` that holds the flower's `Crop` object so we can watch for it being harvested later.
				if (TrackFlowerDirt(location.NameOrUniqueName, crop.Dirt))
				{
					Logger.VerboseLog($"{VerboseStart} Now tracking nearby grown flower '{honeyFlavorSourceHarvestName}' "
						+ $"via its Dirt with tile {crop.Dirt?.Tile.ToString() ?? "[Dirt has `null` Tile]"}. (Bee House Tile {beeHouse.TileLocation} and {location.NameOrUniqueName} location)");

					return true;
				}

				return false;
			}

			// Check that we should attempt to track modded honey flavor sources, and ensure we have a location to check at.
			if (!ModEntry.Compat.ShouldTrackNonDirtCrops || crop.tilePosition.Equals(default) || crop.tilePosition.Equals(Vector2.Zero))
			{
				Logger.Log($"`Crop` object '{honeyFlavorSourceHarvestName}' is missing required data. "
					+ $"Will be unable to track if it gets harvested. (Bee House Tile {beeHouse.TileLocation} and {location.Name} location)", LogLevel.Debug);

				return false;
			}

			// If we can't track the dirt for when its crop is harvested, we'll have to try to determine what to even track by what's at this "crop" (which isn't a normal crop) location.
			Vector2 searchPosition = crop.tilePosition;

			// Better Beehouses labels the source type so we know what any of its minimally filled-in `Crop` instances represent.
			// BB source types: Crop, Forage, FruitTree, Bush, and GiantCrop
			bool hasSourceType_BB = crop.modData.TryGetValue(ModCompat.betterBeehousesModDataSourceTypeKey, out string sourceType_BB);

			// This key is only set when the thing is in a pot, so we can assume if the key exists that its value is the equivalent of `true` (they set it to "T").
			bool isInPot_BB = crop.modData.ContainsKey(ModCompat.betterBeehousesModDataFromPotKey);

			bool wasFound = false;
			bool wasAdded = false;

			// First we'll track for normal dirt with a crop at the location, in case the `Dirt` property just wasn't set on our copy for whatever reason.
			if (location.terrainFeatures.TryGetValue(searchPosition, out TerrainFeature terrainFeature))
			{
				// If we can get dirt with a crop in it, we can track this like we do with normal crop flowers above.
				if (terrainFeature is HoeDirt tfDirt && tfDirt.crop != null)
				{
					wasFound = true;
					wasAdded = TrackFlowerDirt(location.NameOrUniqueName, tfDirt);
				}
				// Note that BB will provide either fruit trees with flowers as the "fruit" on them
				// or *any* fruit tree if its 'UseAnyFruitTrees' setting is enabled.
				else if (terrainFeature is FruitTree tfFruitTree)
				{
					wasFound = true;
					wasAdded = TrackFruitTree(location.NameOrUniqueName, tfFruitTree);
				}
				else if (terrainFeature is Bush tfBush)
				{
					wasFound = true;
					wasAdded = TrackBush(location.NameOrUniqueName, tfBush);
				}
			}

			// Only check the objects list if BB marked the item as in a pot or noted its "type".
			// Note that in the future if we need to support other mods, we could either remove the BB-specific checks in this `if` or add to them.
			// In the meantime we'll prefer to not search the (potentially large) objects list if possible.
			if (!wasFound && (isInPot_BB || hasSourceType_BB) && location.Objects.TryGetValue(searchPosition, out SObject locationObject))
			{
				if (locationObject is IndoorPot objPot)
				{
					// For Better Beehouses, we should get a crop with its dirt associated with it back even for crops in pots,
					// which we would have been handled above already, so this shouldn't be necessary.
					// But it's best to cover all bases, especially if we add support/compat for other mods in the future, so we'll double check here.
					if (objPot.hoeDirt?.Value?.crop != null)
					{
						wasFound = true;
						wasAdded = TrackFlowerDirt(location.NameOrUniqueName, objPot.hoeDirt.Value);
					}
					// Check if the pot has a qualifying item in it. Non-crop items can be grabbed, whereas crops stay crops until harvested.
					// For Better Beehouses, this would likely be a forage item, but could be anything with its 'AnythingHoney' config enabled.
					else if (objPot.heldObject.Value?.CanBeGrabbed ?? false)
					{
						wasFound = true;
						wasAdded = TrackForagePot(location.NameOrUniqueName, objPot);
					}
					else if (objPot.bush?.Value != null)
					{
						wasFound = true;
						wasAdded = TrackBushPot(location.NameOrUniqueName, objPot);
					}
				}
				// Check if it's an item that's just on the ground/floor, i.e. not in a pot.
				// For Better Beehouses, this would likely be a forage item, but could be anything with its 'AnythingHoney' config enabled.
				else if (locationObject.CanBeGrabbed)
				{
					wasFound = true;

					if (!HoneyUpdater.nearbyForageObjects.ContainsKey(location.NameOrUniqueName))
					{
						HoneyUpdater.nearbyForageObjects.Add(location.NameOrUniqueName, new());
					}

					// This will be tracked by `OnObjectListChanged` since we just have to watch for it being picked up.
					wasAdded = HoneyUpdater.nearbyForageObjects[location.NameOrUniqueName].Add(locationObject);
				}
			}

			// Only do this check if BB marked it as a giant crop.
			if (!wasFound && sourceType_BB == "GiantCrop")
			{
				GiantCrop giantCrop = location.resourceClumps.FirstOrDefault(x => x is GiantCrop && x.Tile == searchPosition) as GiantCrop;

				if (giantCrop != null)
				{
					wasFound = true;
					wasAdded = TrackGiantCrop(location.NameOrUniqueName, giantCrop);
				}
			}

			if (wasAdded)
			{
				Logger.VerboseLog($"{VerboseStart} Now tracking nearby honey-flavor source '{honeyFlavorSourceHarvestName}' "
					+ $"{(hasSourceType_BB ? $"(BB | source type: {sourceType_BB} | harvest ID: {crop.indexOfHarvest.Value}) " : String.Empty)}"
					+ $"{(isInPot_BB ? $"(BB in-pot item) " : String.Empty)}at {searchPosition} tile position. "
					+ $"(Bee House Tile {beeHouse.TileLocation} and {location.Name} location)");
			}
			else if (!wasFound)
			{
				Logger.Log($"`Crop` object '{honeyFlavorSourceHarvestName}' "
					+ $"{(hasSourceType_BB ? $"(BB | source type: {sourceType_BB} | harvest ID: {crop.indexOfHarvest.Value}) " : String.Empty)}"
					+ $"{(isInPot_BB ? $"(BB in-pot item) " : String.Empty)}at {searchPosition} tile position didn't match any known trackable honey-flavoring source. "
					+ $"Will be unable to track if it gets harvested. (Bee House Tile {beeHouse.TileLocation} and {location.Name} location)", LogLevel.Debug);
			}

			return wasAdded;
		}

		/// <summary>Add a dirt object with a crop associated to it to be tracked for future changes.</summary>
		/// <param name="locationName">The name of the GameLocation the dirt is located in.</param>
		/// <param name="dirt">The dirt to track.</param>
		/// <returns>True is fully added to tracking, False if already being tracked or if failed to add.</returns>
		private static bool TrackFlowerDirt(string locationName, HoeDirt dirt)
		{
			if (String.IsNullOrWhiteSpace(locationName) || dirt == null)
			{
				return false;
			}

			if (!HoneyUpdater.nearbyFlowerDirt.ContainsKey(locationName))
			{
				HoneyUpdater.nearbyFlowerDirt.Add(locationName, new());
			}

			// Track the tile location of the `HoeDirt` that holds the flower's `Crop` object so we can watch for it being harvested later.
			// If we are already tracking it, exit early.
			if (!HoneyUpdater.nearbyFlowerDirt[locationName].Add(dirt))
			{
				return false;
			}

			// `HoeDirt.netCrop` is private, so we have to get it indirectly.
			NetRef<Crop> dirtNetCrop = dirt.NetFields.GetFields().FirstOrDefault(x => x.Name == Constants.HoeDirtNetCropNetFieldName) as NetRef<Crop>;

			if (dirtNetCrop?.Value == null)
			{
				// If the way that the game builds 'Name' values for `NetFields` entries changes, warn the user that things aren't working right.
				Logger.LogOnce($"{nameof(TrackFlowerDirt)} - Failed to get {nameof(NetRef<Crop>)} netfield for dirt. "
					+ $"Will be unable to track harvesting from it - and likely other crops in dirt - though this message will only show this one time.", LogLevel.Warn);

				string nameValues = String.Join(", ", dirt.NetFields.GetFields().Select(y => y.Name));
				Logger.LogOnce($"{nameof(TrackFlowerDirt)} - All `{nameof(HoeDirt)}.{nameof(HoeDirt.NetFields)}` 'Name' values:\n\t{nameValues}", LogLevel.Trace);

				return false;
			}

			// Wire up an event listener for if the dirt's crop property changes so we can stop tracking it if it's harvested/gone.
			// We define the listener after init so we that we can unsubscribe from listening within the listener body.
			FieldChange<NetRef<Crop>, Crop> dirtNetCropFieldChange = null;
			Guid trackingGuid = GetTrackingGuid(dirt);
			string day = Utilities.UniqueDay;

			dirtNetCropFieldChange = delegate (NetRef<Crop> field, Crop oldValue, Crop newValue)
			{
				Logger.VerboseLog($"{nameof(TrackFlowerDirt)} delegate fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");

				// If we failed to clean up this delegate end-of-day, then just pitch it.
				if (Utilities.UniqueDay != day)
				{
					Logger.VerboseLog($"Fired {nameof(TrackFlowerDirt)} delegate from another day; Skipping and removing ({trackingGuid} from {day})");

					field.fieldChangeVisibleEvent -= dirtNetCropFieldChange;

					return;
				}

				if (newValue == null)
				{
					// Unsubscribe so we don't attempt to remove tracking again
					field.fieldChangeVisibleEvent -= dirtNetCropFieldChange;

					// Remove from EOD cleanup tasks
					RemoveListenerCleanup(locationName, trackingGuid);

					// No dirt or it's not currently tracked, bail early
					if (dirt == null || !HoneyUpdater.nearbyFlowerDirt.ContainsKey(locationName) || !HoneyUpdater.nearbyFlowerDirt[locationName].Contains(dirt))
					{
						Log($"{nameof(TrackFlowerDirt)} listener - Field change event fired in {locationName} location for `null` or untracked dirt @ {dirt?.Tile}");

						return;
					}

					Log($"{nameof(TrackFlowerDirt)} listener - Found harvested tracked flower dirt in {locationName} location @ {dirt.Tile}");

					// Add the tile to our collection to be updated shortly (outside of this delegate)
					HoneyUpdater.ScheduleToUpdateBeeHousesNearLocationTile(locationName, dirt.Tile);

					// Remove it from being tracked
					HoneyUpdater.nearbyFlowerDirt[locationName].Remove(dirt);
				}
			};

			// Subscribe our listener
			dirtNetCrop.fieldChangeVisibleEvent += dirtNetCropFieldChange;

			// Be sure to clean this listener up at the end of the day
			AddListenerCleanup(locationName, trackingGuid, () => {
				dirtNetCrop.fieldChangeVisibleEvent -= dirtNetCropFieldChange;
				Logger.VerboseLog($"{nameof(TrackFlowerDirt)} cleanup delegate action fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");
			});

			return true;
		}

		/// <summary>Add a fruit tree to be tracked for future changes.</summary>
		/// <param name="locationName">The name of the GameLocation the tree is located in.</param>
		/// <param name="fruitTree">The fruit tree to track.</param>
		/// <returns>True is fully added to tracking, False if already being tracked or if failed to add.</returns>
		private static bool TrackFruitTree(string locationName, FruitTree fruitTree)
		{
			if (String.IsNullOrWhiteSpace(locationName) || fruitTree == null)
			{
				return false;
			}

			if (!HoneyUpdater.nearbyFruitTrees.ContainsKey(locationName))
			{
				HoneyUpdater.nearbyFruitTrees.Add(locationName, new());
			}

			// If we are already tracking it, exit early.
			if (!HoneyUpdater.nearbyFruitTrees[locationName].Add(fruitTree))
			{
				return false;
			}

			// Wire up an event listener for when the tree's fruit list is emptied so we can stop tracking it.
			// We define the listener after init so we that we can unsubscribe from listening within the listener body.
			// NOTE - When the tree is shaken (which removes all fruit), `Clear()` is called on the `NetList`, which replaces it with an empty list,
			// which means we can just observe this event and not have to observe `OnElementChanged`, too.
			NetList<Item, NetRef<Item>>.ArrayReplacedEvent fruitArrayReplacedEvent = null;
			Guid trackingGuid = GetTrackingGuid(fruitTree);
			string day = Utilities.UniqueDay;

			fruitArrayReplacedEvent = delegate (NetList<Item, NetRef<Item>> list, IList<Item> before, IList<Item> after)
			{
				Logger.VerboseLog($"{nameof(TrackFruitTree)} delegate fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");

				// If we failed to clean up this delegate end-of-day, then just pitch it.
				if (Utilities.UniqueDay != day)
				{
					Logger.VerboseLog($"Fired {nameof(TrackFruitTree)} delegate from another day; Skipping and removing ({trackingGuid} from {day})");

					// Unsubscribe so we don't attempt again
					list.OnArrayReplaced -= fruitArrayReplacedEvent;

					return;
				}

				if ((list?.Count ?? 0) == 0)
				{
					// Unsubscribe so we don't attempt to remove tracking again
					list.OnArrayReplaced -= fruitArrayReplacedEvent;

					// Remove from EOD cleanup tasks
					RemoveListenerCleanup(locationName, trackingGuid);

					// No tree or it's not currently tracked, bail early
					if (fruitTree == null || !HoneyUpdater.nearbyFruitTrees.ContainsKey(locationName) || !HoneyUpdater.nearbyFruitTrees[locationName].Contains(fruitTree))
					{
						Log($"{nameof(TrackFruitTree)} listener - Fruit array change event fired in {locationName} location for `null` or untracked fruit tree @ {fruitTree?.Tile}");

						return;
					}

					Log($"{nameof(TrackFruitTree)} listener - Found harvested fruit tree in {locationName} location @ {fruitTree.Tile}");

					// Add the tile to our collection to be updated shortly (outside of this delegate)
					HoneyUpdater.ScheduleToUpdateBeeHousesNearLocationTile(locationName, fruitTree.Tile);

					// Remove it from being tracked
					HoneyUpdater.nearbyFruitTrees[locationName].Remove(fruitTree);
				}
			};

			// Subscribe our listener
			fruitTree.fruit.OnArrayReplaced += fruitArrayReplacedEvent;

			// Be sure to clean this listener up at the end of the day
			AddListenerCleanup(locationName, trackingGuid, () => {
				fruitTree.fruit.OnArrayReplaced -= fruitArrayReplacedEvent;
				Logger.VerboseLog($"{nameof(TrackFruitTree)} cleanup delegate action fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");
			});

			return true;
		}

		/// <summary>Add a bush to be tracked for future changes. Supports bushes located in garden pots.</summary>
		/// <param name="locationName">The name of the GameLocation the bush is located in.</param>
		/// <param name="bush">The bush to track.</param>
		/// <returns>True is fully added to tracking, False if already being tracked or if failed to add.</returns>
		private static bool TrackBush(string locationName, Bush bush)
		{
			if (String.IsNullOrWhiteSpace(locationName) || bush == null)
			{
				return false;
			}

			if (!HoneyUpdater.nearbyBushes.ContainsKey(locationName))
			{
				HoneyUpdater.nearbyBushes.Add(locationName, new());
			}

			// If we are already tracking it, exit early.
			if (!HoneyUpdater.nearbyBushes[locationName].Add(bush))
			{
				return false;
			}

			// Wire up an event listener for if the bush changes to not harvestable so we can stop tracking it.
			// The `Bush.readyForHarvest()` function internally looks at its `tileSheetOffset` field to check if it's 1, so we can listener for the field changing.
			// Note that `Bush.readyForHarvest()` is new as of Stardew Valley v1.6.9+, and Better Beehouses will switch to referencing it to calc bushes being eligible or not.
			// Note that if you try to chop down a bush that is in the ground, the first hit will just harvest it, anyways.
			// We define the listener after init so we that we can unsubscribe from listening within the listener body.
			FieldChange<NetInt, int> bushTileSheetOffsetFieldChange = null;
			Guid trackingGuid = GetTrackingGuid(bush);
			string day = Utilities.UniqueDay;

			bushTileSheetOffsetFieldChange = delegate (NetInt field, int oldValue, int newValue)
			{
				Logger.VerboseLog($"{nameof(TrackBush)}{(bush.inPot.Value ? " (in a pot)" : "")} delegate fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");

				// If we failed to clean up this delegate end-of-day, then just pitch it.
				if (Utilities.UniqueDay != day)
				{
					Logger.VerboseLog($"Fired {nameof(TrackBush)}{(bush.inPot.Value ? " (in a pot)" : "")} delegate from another day; Skipping and removing ({trackingGuid} from {day})");

					bush.tileSheetOffset.fieldChangeVisibleEvent -= bushTileSheetOffsetFieldChange;

					return;
				}

				// When not ready for harvest, remove tracking (`Bush.readyForHarvest()` is `false` when `Bush.tileSheetOffset` is `0`)
				if (newValue == 0)
				{
					// Unsubscribe so we don't attempt to remove tracking again
					bush.tileSheetOffset.fieldChangeVisibleEvent -= bushTileSheetOffsetFieldChange;

					// Remove from EOD cleanup tasks
					RemoveListenerCleanup(locationName, trackingGuid);

					// No bush or it's not currently tracked, bail early
					if (bush == null || !HoneyUpdater.nearbyBushes.ContainsKey(locationName) || !HoneyUpdater.nearbyBushes[locationName].Contains(bush))
					{
						Log($"{nameof(TrackBush)} listener - Bush tileSheetOffset field change event fired in {locationName} location for `null` or untracked bush @ {bush?.Tile}");

						return;
					}

					Log($"{nameof(TrackBush)} listener - Found harvested bush {(bush.inPot.Value ? "(was in a pot) " : "")}in {locationName} location @ {bush.Tile}");

					// Add the tile to our collection to be updated shortly (outside of this delegate)
					HoneyUpdater.ScheduleToUpdateBeeHousesNearLocationTile(locationName, bush.Tile);

					// Remove it from being tracked
					HoneyUpdater.nearbyBushes[locationName].Remove(bush);
				}
			};

			// Subscribe our listener
			bush.tileSheetOffset.fieldChangeVisibleEvent += bushTileSheetOffsetFieldChange;

			// Be sure to clean this listener up at the end of the day
			AddListenerCleanup(locationName, trackingGuid, () => {
				bush.tileSheetOffset.fieldChangeVisibleEvent -= bushTileSheetOffsetFieldChange;
				Logger.VerboseLog($"{nameof(TrackBush)}{(bush.inPot.Value ? " (in a pot)" : "")} cleanup delegate action fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");
			});

			return true;
		}

		/// <summary>Add a garden pot to have its bush tracked for future changes.</summary>
		/// <param name="locationName">The name of the GameLocation the pot is located in.</param>
		/// <param name="pot">The pot to track.</param>
		/// <returns>True is fully added to tracking, False if already being tracked or if failed to add.</returns>
		private static bool TrackBushPot(string locationName, IndoorPot pot)
		{
			if (String.IsNullOrWhiteSpace(locationName) || pot == null)
			{
				return false;
			}

			if (!HoneyUpdater.nearbyBushIndoorPots.ContainsKey(locationName))
			{
				HoneyUpdater.nearbyBushIndoorPots.Add(locationName, new());
			}

			// If we are already tracking it, exit early.
			if (!HoneyUpdater.nearbyBushIndoorPots[locationName].Add(pot))
			{
				return false;
			}

			// Wire up an event listener for if the pot's bush is removed so we can stop tracking it.
			// We define the listener after init so we that we can unsubscribe from listening within the listener body.
			FieldChange<NetRef<Bush>, Bush> potBushFieldChange = null;
			Guid trackingGuid = GetTrackingGuid(pot);
			string day = Utilities.UniqueDay;

			potBushFieldChange = delegate (NetRef<Bush> field, Bush oldValue, Bush newValue)
			{
				Logger.VerboseLog($"{nameof(TrackBushPot)} delegate fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");

				// If we failed to clean up this delegate end-of-day, then just pitch it.
				if (Utilities.UniqueDay != day)
				{
					Logger.VerboseLog($"Fired {nameof(TrackBushPot)} delegate from another day; Skipping and removing ({trackingGuid} from {day})");

					field.fieldChangeVisibleEvent -= potBushFieldChange;

					return;
				}

				// If the bush was removed from the pot, then stop tracking it
				if (newValue == null)
				{
					// Unsubscribe so we don't attempt to remove tracking again
					field.fieldChangeVisibleEvent -= potBushFieldChange;

					// Remove from EOD cleanup tasks
					RemoveListenerCleanup(locationName, trackingGuid);

					// No pot or it's not currently tracked, bail early
					if (pot == null || !HoneyUpdater.nearbyBushIndoorPots.ContainsKey(locationName) || !HoneyUpdater.nearbyBushIndoorPots[locationName].Contains(pot))
					{
						Log($"{nameof(TrackBushPot)} listener - IndoorPot's bush field change event fired in {locationName} location for `null` or untracked pot @ {pot?.TileLocation}");

						return;
					}

					Log($"{nameof(TrackBushPot)} listener - Found removed pot bush in {locationName} location @ {pot.TileLocation}");

					// Add the tile to our collection to be updated shortly (outside of this delegate)
					HoneyUpdater.ScheduleToUpdateBeeHousesNearLocationTile(locationName, pot.TileLocation);

					// Remove it from being tracked
					HoneyUpdater.nearbyBushIndoorPots[locationName].Remove(pot);
				}
			};

			// Subscribe our listener
			pot.bush.fieldChangeVisibleEvent += potBushFieldChange;

			// Be sure to clean this listener up at the end of the day
			AddListenerCleanup(locationName, trackingGuid, () => {
				pot.bush.fieldChangeVisibleEvent -= potBushFieldChange;
				Logger.VerboseLog($"{nameof(TrackBushPot)} cleanup delegate action fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");
			});

			// Since the bush can also be harvested, we need to also track the bush itself directly, so just do that like we track bushes that aren't in pots
			TrackBush(locationName, pot.bush.Value);

			return true;
		}

		/// <summary>Add a garden pot to have its held forage tracked for future changes.</summary>
		/// <param name="locationName">The name of the GameLocation the pot is located in.</param>
		/// <param name="pot">The pot to track.</param>
		/// <returns>True is fully added to tracking, False if already being tracked or if failed to add.</returns>
		private static bool TrackForagePot(string locationName, IndoorPot pot)
		{
			if (String.IsNullOrWhiteSpace(locationName) || pot == null)
			{
				return false;
			}

			if (!HoneyUpdater.nearbyForageIndoorPots.ContainsKey(locationName))
			{
				HoneyUpdater.nearbyForageIndoorPots.Add(locationName, new());
			}

			// If we are already tracking it, exit early.
			if (!HoneyUpdater.nearbyForageIndoorPots[locationName].Add(pot))
			{
				return false;
			}

			// Wire up an event listener for if the pot's forage is removed so we can stop tracking it.
			// We define the listener after init so we that we can unsubscribe from listening within the listener body.
			FieldChange<NetRef<SObject>, SObject> potHeldObjectFieldChange = null;
			Guid trackingGuid = GetTrackingGuid(pot);
			string day = Utilities.UniqueDay;

			potHeldObjectFieldChange = delegate (NetRef<SObject> field, SObject oldValue, SObject newValue)
			{
				Logger.VerboseLog($"{nameof(TrackForagePot)} delegate fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");

				// If we failed to clean up this delegate end-of-day, then just pitch it.
				if (Utilities.UniqueDay != day)
				{
					Logger.VerboseLog($"Fired {nameof(TrackForagePot)} delegate from another day; Skipping and removing ({trackingGuid} from {day})");

					field.fieldChangeVisibleEvent -= potHeldObjectFieldChange;

					return;
				}

				// When the forage item was been removed from the pot, stop tracking
				if (newValue == null)
				{
					// Unsubscribe so we don't attempt to remove tracking again
					field.fieldChangeVisibleEvent -= potHeldObjectFieldChange;

					// Remove from EOD cleanup tasks
					RemoveListenerCleanup(locationName, trackingGuid);

					// No pot or it's not currently tracked, bail early
					if (pot == null || !HoneyUpdater.nearbyForageIndoorPots.ContainsKey(locationName) || !HoneyUpdater.nearbyForageIndoorPots[locationName].Contains(pot))
					{
						Log($"{nameof(TrackForagePot)} listener - IndoorPot's heldObject field change event fired in {locationName} location for `null` or untracked pot @ {pot?.TileLocation}");

						return;
					}

					Log($"{nameof(TrackForagePot)} listener - Found removed pot forage in {locationName} location @ {pot.TileLocation}");

					// Add the tile to our collection to be updated shortly (outside of this delegate)
					HoneyUpdater.ScheduleToUpdateBeeHousesNearLocationTile(locationName, pot.TileLocation);

					// Remove it from being tracked
					HoneyUpdater.nearbyForageIndoorPots[locationName].Remove(pot);
				}
			};

			// Subscribe our listener
			pot.heldObject.fieldChangeVisibleEvent += potHeldObjectFieldChange;

			// Be sure to clean this listener up at the end of the day
			AddListenerCleanup(locationName, trackingGuid, () => {
				pot.heldObject.fieldChangeVisibleEvent -= potHeldObjectFieldChange;
				Logger.VerboseLog($"{nameof(TrackForagePot)} cleanup delegate action fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");
			});

			return true;
		}

		/// <summary>Add a giant crop to be tracked for future changes.</summary>
		/// <param name="locationName">The name of the GameLocation the giant crop is located in.</param>
		/// <param name="giantCrop">The giant crop to track.</param>
		/// <returns>True is fully added to tracking, False if already being tracked or if failed to add.</returns>
		private static bool TrackGiantCrop(string locationName, GiantCrop giantCrop)
		{
			if (String.IsNullOrWhiteSpace(locationName) || giantCrop == null)
			{
				return false;
			}

			if (!HoneyUpdater.nearbyGiantCrops.ContainsKey(locationName))
			{
				HoneyUpdater.nearbyGiantCrops.Add(locationName, new());
			}

			// If we are already tracking it, exit early.
			if (!HoneyUpdater.nearbyGiantCrops[locationName].Add(giantCrop))
			{
				return false;
			}

			// Wire up an event listener for if the giant crop drops to zero or less health so we can stop tracking it.
			// This occurs before it's removed from its location's resource clumps list.
			// We define the listener after init so we that we can unsubscribe from listening within the listener body.
			FieldChange<NetFloat, float> giantCropHealthFieldChange = null;
			Guid trackingGuid = GetTrackingGuid(giantCrop);
			string day = Utilities.UniqueDay;

			giantCropHealthFieldChange = delegate (NetFloat field, float oldValue, float newValue)
			{
				Logger.VerboseLog($"{nameof(TrackGiantCrop)} delegate fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");

				// If we failed to clean up this delegate end-of-day, then just pitch it.
				if (Utilities.UniqueDay != day)
				{
					Logger.VerboseLog($"Fired {nameof(TrackGiantCrop)} delegate from another day; Skipping and removing ({trackingGuid} from {day})");

					giantCrop.health.fieldChangeVisibleEvent -= giantCropHealthFieldChange;

					return;
				}

				// When the giant crop's health is zero or less, stop tracking
				if (newValue <= 0)
				{
					// Unsubscribe so we don't attempt to remove tracking again
					giantCrop.health.fieldChangeVisibleEvent -= giantCropHealthFieldChange;

					// Remove from EOD cleanup tasks
					RemoveListenerCleanup(locationName, trackingGuid);

					// No giant crop or it's not currently tracked, bail early
					if (giantCrop == null || !HoneyUpdater.nearbyGiantCrops.ContainsKey(locationName) || !HoneyUpdater.nearbyGiantCrops[locationName].Contains(giantCrop))
					{
						Log($"{nameof(TrackGiantCrop)} listener - Giant crop health field change event fired in {locationName} location for `null` or untracked giant crop @ {giantCrop?.Tile}");

						return;
					}

					Log($"{nameof(TrackGiantCrop)} listener - Found harvested giant crop in {locationName} location @ {giantCrop.Tile}");

					// Just updating at the one "Tile" of the giant crop is not enough to update all bee houses within range.
					// Go through the tiles it covers and schedule updates around the entire border.
					for (int x = 0; x < giantCrop.width.Value; x++)
					{
						for (int y = 0; y < giantCrop.height.Value; y++)
						{
							if (x == 0 || y == 0 || x == (giantCrop.width.Value - 1) || y == (giantCrop.height.Value - 1))
							{
								Vector2 borderTile = new Vector2(giantCrop.Tile.X + x, giantCrop.Tile.Y + y);

								// Add the tile to our collection to be updated shortly (outside of this delegate)
								HoneyUpdater.ScheduleToUpdateBeeHousesNearLocationTile(locationName, borderTile);
							}
						}
					}

					// Remove it from being tracked
					HoneyUpdater.nearbyGiantCrops[locationName].Remove(giantCrop);
				}
			};

			// Subscribe our listener
			giantCrop.health.fieldChangeVisibleEvent += giantCropHealthFieldChange;

			// Be sure to clean this listener up at the end of the day
			AddListenerCleanup(locationName, trackingGuid, () => {
				giantCrop.health.fieldChangeVisibleEvent -= giantCropHealthFieldChange;
				Logger.VerboseLog($"{nameof(TrackGiantCrop)} cleanup delegate action fired on day {Game1.dayOfMonth}, registered day {day}, for {trackingGuid}");
			});

			return true;
		}
	}
}
