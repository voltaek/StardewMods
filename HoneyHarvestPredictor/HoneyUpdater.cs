using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley.Extensions;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HoneyHarvestPredictor
{
	public static class HoneyUpdater
	{
		// Tracking collections for bee houses and flowers (or flower equivalents) nearby them that we refresh each day.

		private static readonly Dictionary<string, HashSet<SObject>> beeHousesReady = new();
		private static readonly Dictionary<string, HashSet<SObject>> beeHousesReadyToday = new();

		internal static readonly Dictionary<string, HashSet<HoeDirt>> nearbyFlowerDirt = new();
		
		// For tracking modded honey-flavor sources
		internal static readonly Dictionary<string, HashSet<FruitTree>> nearbyFruitTrees = new();
		internal static readonly Dictionary<string, HashSet<Bush>> nearbyBushes = new();
		internal static readonly Dictionary<string, HashSet<IndoorPot>> nearbyBushIndoorPots = new();
		internal static readonly Dictionary<string, HashSet<IndoorPot>> nearbyForageIndoorPots = new();
		internal static readonly Dictionary<string, HashSet<SObject>> nearbyForageObjects = new();
		internal static readonly Dictionary<string, HashSet<GiantCrop>> nearbyGiantCrops = new();

		// For scheduling updates to bee houses near locations from within netfield delegates so we can handle them shortly after
		private static readonly Dictionary<string, HashSet<Vector2>> scheduledLocationTilesToUpdateAround = new();

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

		/// <summary>Event handler for after a new day starts.</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnDayStarted(object sender, DayStartedEventArgs e)
		{
			Logger.VerboseLog($"{VerboseStart} {nameof(OnDayStarted)} - Started");

			// Refresh everything - our tracked bee houses and our honey-flavor sources - for the new day
			RefreshAll();			

			Logger.VerboseLog($"{VerboseStart} {nameof(OnDayStarted)} - Ended");
		}

		/// <summary>
		/// Event handler raised before the game ends the current day.
		/// This happens before it starts setting up the next day and before saving.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnDayEnding(object sender, DayEndingEventArgs e)
		{
			Logger.VerboseLog($"{VerboseStart} {nameof(OnDayEnding)} - Started");

			// Clean up the listeners as soon as they're not required for the day.
			Tracking.CleanupAllListeners();

			Logger.VerboseLog($"{VerboseStart} {nameof(OnDayEnding)} - Ended");
		}

		/// <summary>
		/// Event handler raised after the game returns to the title screen.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			Logger.VerboseLog($"{VerboseStart} {nameof(OnReturnedToTitle)} - Started");

			ClearAll();

			Logger.VerboseLog($"{VerboseStart} {nameof(OnReturnedToTitle)} - Ended");
		}

		/// <summary>Event handler for when the in-game clock changes.</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnTimeChanged(object sender, TimeChangedEventArgs e)
		{
			// We don't need to day anything right when we wake up, since that's handled by `OnDayStarted`,
			// and we don't want to have any race conditions with it, either.
			if (e.NewTime == Constants.startOfDayTime)
			{
				return;
			}

			// If bee houses only refresh in the morning, then nothing to check.
			if (Utilities.DoBeeHousesOnlyRefreshDaily)
			{
				return;
			}

			foreach (KeyValuePair<string, HashSet<SObject>> entry in beeHousesReadyToday)
			{
				HashSet<SObject> newlyReadyBeeHouses = entry.Value.Where(x => x.readyForHarvest.Value).ToHashSet();

				if (newlyReadyBeeHouses.Count == 0)
				{
					continue;
				}

				Log($"{nameof(OnTimeChanged)} - Found {newlyReadyBeeHouses.Count} newly ready bee houses in {entry.Key} location");

				GameLocation location = Utilities.FetchLocationByName(entry.Key);

				if (location != null)
				{
					UpdateLocationBeeHouses(location, newlyReadyBeeHouses);

					if (!beeHousesReady.ContainsKey(entry.Key))
					{
						beeHousesReady.Add(entry.Key, new HashSet<SObject>());
					}

					beeHousesReady[entry.Key].AddRange(newlyReadyBeeHouses);
				}

				beeHousesReadyToday[entry.Key].RemoveWhere(newlyReadyBeeHouses.Contains);
			}
		}

		/// <summary>Event handler for after the game state is updated, once per second.</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
		{
			// Every X seconds (note: ≈60 ticks/second), refresh everything if we found that another mod has changed settings we care about.
			if (e.IsMultipleOf(5 * 60) && ModEntry.Compat.DidCompatModConfigChange())
			{
				Log($"{nameof(OnOneSecondUpdateTicked)} - Doing a full refresh because another mod has updated config values we care about.");

				RefreshAll();
			}
		}

		/// <summary>
		/// Raised after the game state is updated (≈60 times per second).
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		internal static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
		{
			// Every X ticks, check if we scheduled any tiles to update bee houses around
			if (e.IsMultipleOf(10))
			{
				if (scheduledLocationTilesToUpdateAround.Any())
				{
					UpdateBeeHousesNearLocationTiles(scheduledLocationTilesToUpdateAround);

					scheduledLocationTilesToUpdateAround.Clear();
				}
			}
		}

		/// <summary>
		/// Event handler for after objects are added/removed in any location (including machines, fences, etc).
		/// This doesn't apply for floating items (see `DebrisListChanged`) or furniture (see `FurnitureListChanged`).
		/// This event isn't raised for objects already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added → objects`.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
		{
			string locationName = e.Location.NameOrUniqueName;

			// Check the removed objects for bee houses
			if (e.Removed.Any(x => x.Value.QualifiedItemId == Constants.beeHouseQualifiedItemID))
			{
				// Find all removed bee houses so we can remove them from our tracking dictionaries
				IEnumerable<SObject> removedBeeHouses = e.Removed.Select(y => y.Value).Where(z => z.QualifiedItemId == Constants.beeHouseQualifiedItemID);

				Log($"{nameof(OnObjectListChanged)} - Found {removedBeeHouses.Count()} removed bee houses to attempt to remove from tracking in {locationName} location");

				if (beeHousesReady.ContainsKey(locationName) && beeHousesReady[locationName].Any(removedBeeHouses.Contains))
				{
					beeHousesReady[locationName].RemoveWhere(removedBeeHouses.Contains);
					Logger.VerboseLog($"{VerboseStart} {nameof(OnObjectListChanged)} - {e.Location} location has {beeHousesReady[locationName].Count} remaining tracked ready bee houses");
				}

				if (!Utilities.DoBeeHousesOnlyRefreshDaily
					&& beeHousesReadyToday.ContainsKey(locationName) && beeHousesReadyToday[locationName].Any(removedBeeHouses.Contains))
				{
					beeHousesReadyToday[locationName].RemoveWhere(removedBeeHouses.Contains);
					Logger.VerboseLog($"{VerboseStart} {nameof(OnObjectListChanged)} - {e.Location} location has {beeHousesReadyToday[locationName].Count} remaining tracked ready-today bee houses");
				}
			}

			// Collect all tiles to update around for this location
			HashSet<Vector2> updateNearTiles = new();

			IEnumerable<IndoorPot> removedIndoorPots = null;

			if (e.Removed.Any(x => x.Value.QualifiedItemId == Constants.gardenPotQualifiedItemID))
			{
				// If some objects have pot IDs, then extract them typed
				removedIndoorPots = e.Removed.Select(x => x.Value as IndoorPot).Where(x => x is not null && x.QualifiedItemId == Constants.gardenPotQualifiedItemID);
			}

			// If some pots were removed, check for vanilla situations we need to handle, such as a bomb destroying a pot and its dirt all at once, which skips the harvesting trigger.
			if (removedIndoorPots?.Any() ?? false && nearbyFlowerDirt.ContainsKey(locationName))
			{
				Log($"{nameof(OnObjectListChanged)} - Found {removedIndoorPots.Count()} removed garden pots to attempt to remove from tracking in {locationName} location (vanilla checks)");

				List<HoeDirt> removedLocationPotDirt = nearbyFlowerDirt[locationName].Where(dirt => removedIndoorPots.Contains(dirt.Pot)).ToList();

				if (removedLocationPotDirt.Any())
				{
					Log($"{nameof(OnObjectListChanged)} - Removed {removedLocationPotDirt.Count()} of the removed crop-dirt-holding indoor pots in {locationName} location"
						+ $" @ [{String.Join(", ", removedLocationPotDirt.Select(y => y.Pot?.TileLocation ?? y.Tile))}]");

					// Hold onto where in the GameLocation we need to update near
					updateNearTiles.AddRange(removedLocationPotDirt.Select(x => x.Pot?.TileLocation ?? x.Tile));

					// Remove the dirt in the indoor pot(s) from being tracked
					removedLocationPotDirt.ForEach(dirt => Tracking.CleanupListener(dirt, locationName));
					nearbyFlowerDirt[locationName].RemoveWhere(removedLocationPotDirt.Contains);
				}
			}

			// Exit early if we're only dealing with vanilla situations and process any updates we need to
			if (!ModEntry.Compat.ShouldTrackNonDirtCrops)
			{
				if (updateNearTiles.Any())
				{
					// Make a single pass through all the tiles we collected by collecting all the bee houses near all the tiles before processing the location.
					UpdateBeeHousesNearLocationTiles(new Dictionary<string, HashSet<Vector2>>() { { locationName, updateNearTiles } });
				}

				return;
			}

			// When BB installed - Have to check our list of forage-holding and bush-hosting pots to see if one of those was removed.
			// Even if the held object is ejected from the same hit that picks up the pot, the held object field of the pot isn't updated,
			// so it's required that we watch for them being removed and also that we not trust their held object property at that point.
			// If a pot is blown up with a bomb, it is removed immediately, so these checks will also handle that condition for forage and bush pots.
			if (removedIndoorPots?.Any() ?? false && (nearbyForageIndoorPots.ContainsKey(locationName) || nearbyBushIndoorPots.ContainsKey(locationName)))
			{
				Log($"{nameof(OnObjectListChanged)} - Found {removedIndoorPots.Count()} removed garden pots to attempt to remove from tracking in {locationName} location (modded checks)");

				List<IndoorPot> removedLocationForagePots = nearbyForageIndoorPots[locationName].Where(removedIndoorPots.Contains).ToList();
				List<IndoorPot> removedLocationBushPots = nearbyBushIndoorPots[locationName].Where(removedIndoorPots.Contains).ToList();

				if (removedLocationForagePots.Any())
				{
					Log($"{nameof(OnObjectListChanged)} - Removed {removedLocationForagePots.Count()} of the removed forage-holding indoor pots in {locationName} location"
						+ $" @ [{String.Join(", ", removedLocationForagePots.Select(y => y.TileLocation))}].");

					// Hold onto where in the GameLocation we need to update near
					updateNearTiles.AddRange(removedLocationForagePots.Select(x => x.TileLocation));

					// Remove the indoor pot(s) from being tracked
					removedLocationForagePots.ForEach(pot => Tracking.CleanupListener(pot, locationName));
					nearbyForageIndoorPots[locationName].RemoveWhere(removedLocationForagePots.Contains);
				}

				if (removedLocationBushPots.Any())
				{
					Log($"{nameof(OnObjectListChanged)} - Removed {removedLocationBushPots.Count()} of the removed bush-hosting indoor pots in {locationName} location"
						+ $" @ [{String.Join(", ", removedLocationBushPots.Select(y => y.TileLocation))}]");

					// Hold onto where in the GameLocation we need to update near
					updateNearTiles.AddRange(removedLocationBushPots.Select(x => x.TileLocation));

					// Remove the indoor pot(s) from being tracked
					removedLocationBushPots.ForEach(pot => Tracking.CleanupListener(pot, locationName));
					nearbyBushIndoorPots[locationName].RemoveWhere(removedLocationBushPots.Contains);

					// Also need to remove the bush(es) in the indoor pot(s) from being tracked, since we have to track them for being harvested
					removedLocationBushPots.ForEach(pot => Tracking.CleanupListener(pot.bush.Value, locationName));
					nearbyBushes[locationName].RemoveWhere(bush => removedLocationBushPots.Select(pot => pot.bush.Value).Contains(bush));
				}
			}

			// When BB installed - Check our list of bare forage to see if any were removed
			if (e.Removed.Any(x => x.Value.CanBeGrabbed && Utilities.IsHoneyFlavorSource(x.Value)) && nearbyForageObjects.ContainsKey(locationName))
			{
				IEnumerable<SObject> removedForageObjects = e.Removed.Select(x => x.Value).Where(obj => obj.CanBeGrabbed && Utilities.IsHoneyFlavorSource(obj));

				Log($"{nameof(OnObjectListChanged)} - Found {removedForageObjects.Count()} forage objects to attempt to remove from tracking in {locationName} location");

				if (removedForageObjects.Any())
				{
					IEnumerable<SObject> removedLocationForage = nearbyForageObjects[locationName].Where(removedForageObjects.Contains);

					Log($"{nameof(OnObjectListChanged)} - Removed {removedLocationForage.Count()} of the harvested bare forage in {locationName} location"
						+ $" @ [{String.Join(", ", removedLocationForage.Select(y => y.TileLocation))}]");

					// Hold onto where in the GameLocation we need to update near
					updateNearTiles.AddRange(removedLocationForage.Select(x => x.TileLocation));

					// Remove the forage(s) from being tracked
					nearbyForageObjects[locationName].RemoveWhere(removedLocationForage.Contains);
				}
			}

			if (updateNearTiles.Any())
			{
				// Now make a single pass through all the tiles we collected by collecting all the bee houses near all the tiles before processing the location.
				UpdateBeeHousesNearLocationTiles(new Dictionary<string, HashSet<Vector2>>() { { locationName, updateNearTiles } });
			}
		}

		/// <summary>Event handler for after a game location is added or removed (including building interiors).</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnLocationListChanged(object sender, LocationListChangedEventArgs e)
		{
			foreach (GameLocation addedLocation in e.Added.Where(Utilities.IsLocationWithBeeHouses))
			{
				// If we have the location tracked already, remove all existing tracking before we (re-)add the location
				RemoveLocationFromTracking(addedLocation);

				// Now add the location fresh to our tracking and run any updates we need to do
				AddLocation(addedLocation);
			}

			// Clear any data we are tracking about this location
			foreach (GameLocation removedLocation in e.Removed.Where(x => beeHousesReady.ContainsKey(x.NameOrUniqueName) || beeHousesReadyToday.ContainsKey(x.NameOrUniqueName)))
			{
				RemoveLocationFromTracking(removedLocation);
			}
		}

		/// <summary>
		/// Adds bee houses in the given location to our lists of bee houses.
		/// For "ready" bee houses, will also update the bee houses, which also adds flowers nearby to the bee houses to our tracked list.
		/// </summary>
		/// <param name="location">The location to add to tracking and immediately start tracking thing at.</param>
		private static void AddLocation(GameLocation location)
		{
			HashSet<SObject> ready = location.Objects.Values.Where(x => x.QualifiedItemId == Constants.beeHouseQualifiedItemID && x.readyForHarvest.Value).ToHashSet();

			if (ready.Count > 0)
			{
				Log($"{nameof(AddLocation)} - Found {ready.Count} ready bee houses in added {location.NameOrUniqueName} location");

				beeHousesReady.Add(location.NameOrUniqueName, ready);
				UpdateLocationBeeHouses(location, ready);
			}

			// No reason to check for bee houses becoming ready throughout the day if they only do so at the start of the day.
			// Ref: Use of `CalculateMinutesUntilMorning()` in `SObject.OutputMachine()` in game decompile.
			if (Utilities.DoBeeHousesOnlyRefreshDaily)
			{
				return;
			}

			HashSet<SObject> readyToday = location.Objects.Values.Where(x => x.QualifiedItemId == Constants.beeHouseQualifiedItemID
				&& !x.readyForHarvest.Value && x.MinutesUntilReady < Utilities.MinutesUntilEndOfDay).ToHashSet();

			if (readyToday.Count > 0)
			{
				Log($"{nameof(AddLocation)} - Found {readyToday.Count} bee houses that will be ready today in added {location.NameOrUniqueName} location");

				beeHousesReadyToday.Add(location.NameOrUniqueName, readyToday);
			}
		}

		/// <summary>Remove anything we're tracking at the given location.</summary>
		/// <param name="location">The location to no longer track anything at.</param>
		private static void RemoveLocationFromTracking(GameLocation location)
		{
			Tracking.CleanupAllListeners(location.NameOrUniqueName);

			beeHousesReady.Remove(location.NameOrUniqueName);
			beeHousesReadyToday.Remove(location.NameOrUniqueName);
			nearbyFlowerDirt.Remove(location.NameOrUniqueName);

			if (!ModEntry.Compat.ShouldTrackNonDirtCrops)
			{
				return;
			}

			nearbyFruitTrees.Remove(location.NameOrUniqueName);
			nearbyBushes.Remove(location.NameOrUniqueName);
			nearbyBushIndoorPots.Remove(location.NameOrUniqueName);
			nearbyForageIndoorPots.Remove(location.NameOrUniqueName);
			nearbyForageObjects.Remove(location.NameOrUniqueName);
			nearbyGiantCrops.Remove(location.NameOrUniqueName);
		}

		/// <summary>
		/// Refresh the "held object" in all tracked, ready-for-harvest bee houses.
		/// This will refresh the icon shown overtop those bee houses.
		/// This can be used in cases where the bee houses should now be showing a different icon above them
		/// due to another mod's config value being changed, which could/would affect the assigned/shown item.
		/// </summary>
		public static void RefreshBeeHouseHeldObjects()
		{
			Logger.VerboseLog($"{VerboseStart} {nameof(RefreshBeeHouseHeldObjects)} - Started");

			foreach (KeyValuePair<string, HashSet<SObject>> kvp in beeHousesReady)
			{
				GameLocation location = Utilities.FetchLocationByName(kvp.Key);

				if (location == null)
				{
					continue;
				}

				UpdateLocationBeeHouses(location, kvp.Value);
			}

			Logger.VerboseLog($"{VerboseStart} {nameof(RefreshBeeHouseHeldObjects)} - Ended");
		}

		/// <summary>
		/// This will refresh all tracking - bee houses being tracked as well as their honey flavor sources - across all locations.
		/// This is what runs at the start of each day and should ideally only be run then,
		/// but if everything should be thrown out and re-evaluated for some reason, this will do that.
		/// </summary>
		public static void RefreshAll()
		{
			ClearAll();

			// Get just locations we care about. Include indoor locations only when needed for mod compatability.
			Utility.ForEachLocation((GameLocation location) => {
				if (Utilities.IsLocationWithBeeHouses(location))
				{
					AddLocation(location);
				}

				return true;
			}, ModEntry.Compat.SyncIndoorBeeHouses);
		}

		/// <summary>
		/// Clean up all tracking, including listeners, bee houses, and honey flavor sources.
		/// </summary>
		internal static void ClearAll()
		{
			// Run and clear out any end of day listener cleanup tasks so we can re-track everything fresh
			Tracking.CleanupAllListeners();

			// Reset our tracked bee houses and vanilla honey-flavor sources
			beeHousesReady.Clear();
			beeHousesReadyToday.Clear();
			nearbyFlowerDirt.Clear();

			// Reset modded honey-flavor sources
			if (ModEntry.Compat.ShouldTrackNonDirtCrops)
			{
				nearbyFruitTrees.Clear();
				nearbyBushes.Clear();
				nearbyBushIndoorPots.Clear();
				nearbyForageIndoorPots.Clear();
				nearbyForageObjects.Clear();
				nearbyGiantCrops.Clear();
			}
		}

		/// <summary>
		/// If we don't want to do the work of updating potentially numerous bee houses immediately, we can schedule the location and tile to be updated shortly.
		/// This is typically so we don't try to do too much work inside the event listener attached to a net field.
		/// </summary>
		/// <param name="locationName">The GameLocation name.</param>
		/// <param name="locationTileToUpdateAround">The tile to update nearby bee houses of.</param>
		internal static void ScheduleToUpdateBeeHousesNearLocationTile(string locationName, Vector2 locationTileToUpdateAround)
		{
			if (!scheduledLocationTilesToUpdateAround.ContainsKey(locationName))
			{
				scheduledLocationTilesToUpdateAround.Add(locationName, new());
			}

			scheduledLocationTilesToUpdateAround[locationName].Add(locationTileToUpdateAround);
		}

		/// <summary>Updates any bee houses nearby each of the tiles in the given location tiles collections.</summary>
		/// <param name="locationTilesToUpdateAround">A collection of tiles that need nearby bee houses to be updated, grouped by their location.</param>
		private static void UpdateBeeHousesNearLocationTiles(Dictionary<string, HashSet<Vector2>> locationTilesToUpdateAround)
		{
			foreach (KeyValuePair<string, HashSet<Vector2>> locationWithTiles in locationTilesToUpdateAround)
			{
				string updateLocationName = locationWithTiles.Key;

				if (!beeHousesReady.ContainsKey(updateLocationName))
				{
					continue;
				}

				HashSet<SObject> beeHousesToUpdate = beeHousesReady[updateLocationName]
					.Where(beeHouse => locationWithTiles.Value.Any(updateAroundTile => Utilities.IsWithinFlowerRange(beeHouse.TileLocation, updateAroundTile)))
					.ToHashSet();

				if (beeHousesToUpdate.Count == 0)
				{
					continue;
				}

				Log($"{nameof(UpdateBeeHousesNearLocationTiles)} - Found {beeHousesToUpdate.Count} ready bee houses that need updating in {updateLocationName} location.");

				GameLocation updateLocation = Utilities.FetchLocationByName(updateLocationName);

				if (updateLocation == null)
				{
					Logger.LogOnce($"Unable to update the bee houses that need refreshed at this location.", LogLevel.Info);

					locationTilesToUpdateAround.Remove(updateLocationName);

					continue;
				}

				UpdateLocationBeeHouses(updateLocation, beeHousesToUpdate);
			}
		}

		/// <summary>
		/// Updates the honey held by the given ready-for-harvest bee houses, which are at the given location.
		/// This also adds any nearby flowers to our tracked list of them.
		/// </summary>
		/// <param name="location">The location of the ready bee houses.</param>
		/// <param name="readyBeeHouses">The bee houses which are ready to be harvested which we should update the honey of.</param>
		private static void UpdateLocationBeeHouses(GameLocation location, HashSet<SObject> readyBeeHouses)
		{
			Logger.VerboseLog($"{VerboseStart} {nameof(UpdateLocationBeeHouses)} - Started");

			ObjectDataDefinition objectData = ItemRegistry.GetObjectTypeDefinition();
			int flowerRange = ModEntry.Compat.FlowerRange;

			List<SObject> invalidBeeHouses = new();
			int newlyTrackedHoneyFlavorSourceCount = 0;

			foreach (SObject beeHouse in readyBeeHouses)
			{
				// If a bee house no longer qualifies in any way, we'll remove it after we go through the list we were given
				if (beeHouse == null || !beeHouse.readyForHarvest.Value || beeHouse.QualifiedItemId != Constants.beeHouseQualifiedItemID)
				{
					invalidBeeHouses.Add(beeHouse);

					// If the issue is just that the bee house was harvested, then no log entry should be made
					if (beeHouse?.readyForHarvest.Value == false)
					{
						continue;
					}

					Logger.Log($"Found an invalid bee house in {location} location; removing from tracking: "
						+ $"{(beeHouse == null ? "null" : $"Tile {beeHouse.TileLocation}; RFH {(beeHouse.readyForHarvest.Value ? "Yes" : "No")}; QID {beeHouse.QualifiedItemId}")}", LogLevel.Info);

					continue;
				}

				// Same flower check the game uses (see `MachineDataUtility.GetNearbyFlowerItemId()`) when collecting the honey out of the bee house.
				// Note that if another mod patches this method - such as 'Better Beehouses' - we'll still get a `Crop` back, but it might not be a flower,
				// and/or it might not be in a standard HoeDirt instance.
				Crop closeFlower = Utility.findCloseFlower(location, beeHouse.TileLocation, flowerRange, (Crop crop) => !crop.forageCrop.Value);
				SObject flowerIngredient = null;
				string flowerHarvestName = String.Empty;

				// If we found a qualifying flower crop with an assigned harvest item, then get its harvested object form.
				if (closeFlower?.indexOfHarvest?.Value != null)
				{
					string flowerIngredientID = ItemRegistry.QualifyItemId(closeFlower.indexOfHarvest.Value);

					if (flowerIngredientID == null)
					{
						Logger.Log($"Failed to get the qualified item ID of a nearby flower from the flower's `indexOfHarvest.Value` value of '{closeFlower.indexOfHarvest.Value}'.", LogLevel.Warn);
					}
					else
					{
						string itemCreationFailureMessage = $"Failed to create an `Item` (and then convert it to `Object`) via `ItemRegistry.Create` "
							+ $"using a nearby flower's qualified item ID of '{flowerIngredientID}'.";

						// `StardewValley.Internal.ItemQueryResolver.ItemQueryResolver.DefaultResolvers.FLAVORED_ITEM()` has this in a `try/catch`, so mimicking that here 
						try
						{
							// If this comes back as `null` or the conversion fails (resulting in `null`), that's fine since we'll just get "Wild Honey" back
							// when we attempt to create flavored honey below.
							flowerIngredient = ItemRegistry.Create(flowerIngredientID, allowNull: true) as SObject;

							if (flowerIngredient == null)
							{
								Logger.Log(itemCreationFailureMessage, LogLevel.Warn);
							}
							else
							{
								flowerHarvestName = flowerIngredient.Name;
							}
						}
						catch (Exception ex)
						{
							Logger.Log(itemCreationFailureMessage + $"\n\nException ({ex.GetType().Name}): {ex.Message}", LogLevel.Error);
						}
					}

					newlyTrackedHoneyFlavorSourceCount += Tracking.TrackHoneyFlavorSource(closeFlower, location, beeHouse, flowerHarvestName) ? 1 : 0;
				}
				else if (closeFlower != null && closeFlower.indexOfHarvest?.Value == null)
				{
					Logger.Log($"The nearby {(ModEntry.Compat.ShouldTrackNonDirtCrops ? "honey flavor source" : "flower")} "
						+ $"has no harvest item (`indexOfHarvest.Value`) value assigned, which is probably incorrect.", LogLevel.Info);
				}

				/*
				We set the held object to either the default honey item (such as when there are no nearby flowers or all nearby flowers were harvested),
				or to an object that will inform the player about what they'll receive at time of harvest, due to a qualifying full-grown flower being nearby enough.

				If the player's mod config specifies to show the nearby flower (the default), we use the flower ingredient object we created as the held object.
				Otherwise, if they changed their option to show artisan honey, we'll use the flower ingredient to create the flavored honey object.
				Note that the user will need to have another mod to provide custom icons for artisan honey items for this option to display anything besides the base honey item.

				The game will create its own honey object at harvest to return to the farmer, so whatever we have the bee house holding in the meantime won't affect gameplay in any way.
				Previous to SD v1.6, though, the game only updated some of the held object's properties rather than creating a new object,
				so we couldn't use the flower option (without additional programming), then.

				Note that the ingredient passed to `ObjectDataDefinition.CreateFlavoredHoney()` being `null` is fine for honey as it will return the base/default "Wild Honey" object.
				Ref: `Object.CheckForActionOnMachine()`
				*/
				beeHouse.heldObject.Value = flowerIngredient != null && ModEntry.Config.BeeHouseReadyIconEnum == ModConfig.ReadyIcon.Flower
					? flowerIngredient
					: objectData.CreateFlavoredHoney(flowerIngredient);

				// Add modData to this item to indicate that it's from this mod and it's just for display
				beeHouse.heldObject.Value.modData[Constants.ModDataKey_BeeHouseReadyTempDisplayObject] = "1";

				Logger.VerboseLog($"{VerboseStart} Assigned {beeHouse.heldObject.Value.Name} to bee house in {location.Name} location @ {beeHouse.TileLocation}");
			}

			// Remove any invalid bee houses from the given list
			readyBeeHouses.RemoveWhere(invalidBeeHouses.Contains);

			Log($"{nameof(UpdateLocationBeeHouses)} - Updated {readyBeeHouses.Count} ready bee houses in {location.Name} location"
				+ (newlyTrackedHoneyFlavorSourceCount > 0
					? $" and now tracking {newlyTrackedHoneyFlavorSourceCount} additional nearby {(ModEntry.Compat.ShouldTrackNonDirtCrops ? "honey flavor sources" : "flowers")}"
					: String.Empty));

			Logger.VerboseLog($"{VerboseStart} {nameof(UpdateLocationBeeHouses)} - Ended");
		}
	}
}
