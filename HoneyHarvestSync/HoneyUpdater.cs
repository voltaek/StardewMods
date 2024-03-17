using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using SObject = StardewValley.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StardewValley.TerrainFeatures;
using Microsoft.Xna.Framework;

namespace HoneyHarvestSync
{
	public static class HoneyUpdater
	{
		/// <summary>Should be set as a refence to the Mod's monitor before anything in here is called (or set as event handlers) so we can do logging.</summary>
		internal static IMonitor Monitor { get; set; }

		/// <summary>Minutes total from when the farmer/player wakes up (6am) until the latest they can be awake (2am).</summary>
		private const int maxMinutesAwake = 1200;

		/// <summary>The max default range a flower can affect a bee house from.</summary>
		private const int flowerRange = 5;

		/// <summary>
		/// Time the farmer wakes up, but in the 24 hour integer form used in the properties of `TimeChangedEventArgs`.
		/// Examples: 600 is 6am and 1300 is 1pm.
		/// </summary>
		private const int startOfDayTime = 600;

		/// <summary>The globally unique identifier for Bee House machines.</summary>
		private const string beeHouseQualifiedItemID = "(BC)10";

		/// <summary>Filter to test locations with to see if they can and do have bee houses in them.</summary>
		private static readonly Func<GameLocation, bool> isLocationWithBeeHouses = (location) => location.IsOutdoors && location.Objects.Values.Any(x => x.QualifiedItemId == beeHouseQualifiedItemID);

		/// <summary>Whether we should output development debug logging or not. It's pretty verbose, so should keep off in releases.</summary>
		#if DEBUG
		private const bool shouldOutputDebug = true;
		#else
		private const bool shouldOutputDebug = false;
		#endif

		// Tracking lists for bee houses and flowers nearby them that we refresh each day.
		private static readonly Dictionary<GameLocation, List<SObject>> beeHousesReady = new();
		private static readonly Dictionary<GameLocation, List<SObject>> beeHousesReadyToday = new();
		private static readonly Dictionary<GameLocation, List<Vector2>> closeFlowerTileLocations = new();

		// Shorthand for the debug logger. Also so we can easily disable outputting it.
		private static void DebugLog(string message)
		{


			// TODO - Should change this to VerboseLog for most calls to it, with probably only some select Debug (make mostly summary-type messages) and Info log entries being made


			if (shouldOutputDebug && Monitor != null)
			{
				// Show microsecond, too, so we can tell if something is causing performance issues
				Monitor.Log($"{DateTime.Now:ffffff} {nameof(HoneyUpdater)} {message}", LogLevel.Debug);
			}
		}

		/// <summary>Event handler for after a new day starts.</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnDayStarted(object sender, DayStartedEventArgs e)
		{
			DebugLog($"{nameof(OnDayStarted)} - Started");

			// Reset our tracked bee houses and flowers for the new day
			beeHousesReady.Clear();
			beeHousesReadyToday.Clear();
			closeFlowerTileLocations.Clear();

			// Filter to just locations we care about.
			foreach (GameLocation location in Game1.locations.Where(x => isLocationWithBeeHouses(x)))
			{
				AddLocation(location);
			}

			DebugLog($"{nameof(OnDayStarted)} - Ended");
		}

		/// <summary>Event handler for when the in-game clock changes.</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnTimeChanged(object sender, TimeChangedEventArgs e)
		{
			// We don't need to day anything right when we wake up, since that's handled by `OnDayStarted`,
			// and we don't want to have any race conditions with it, either.
			if (e.NewTime == startOfDayTime)
			{
				return;
			}

			foreach (KeyValuePair<GameLocation, List<SObject>> entry in beeHousesReadyToday)
			{
				List<SObject> newlyReadyBeeHouses = entry.Value.Where(x => x.readyForHarvest.Value).ToList();

				if (newlyReadyBeeHouses.Count == 0)
				{
					continue;
				}

				DebugLog($"{nameof(OnTimeChanged)} - Found {newlyReadyBeeHouses.Count} newly ready bee houses @ {entry.Key.Name} location");

				UpdateLocationBeeHouses(entry.Key, newlyReadyBeeHouses);

				if (!beeHousesReady.ContainsKey(entry.Key))
				{
					beeHousesReady.Add(entry.Key, new List<SObject>());
				}

				beeHousesReady[entry.Key].AddRange(newlyReadyBeeHouses);
				beeHousesReadyToday[entry.Key].RemoveAll(x => newlyReadyBeeHouses.Contains(x));
			}
			}

		/// <summary>Event handler for after the game state is updated, once per second.</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
		{
			// Check if flowers that would affect the honey nearby bee houses show have been harvested
			foreach (KeyValuePair<GameLocation, List<Vector2>> entry in closeFlowerTileLocations)
			{



				// TODO - Check if this can be improved, especially now that crops know which dirt they're in
				// Can we switch to just storing the HoeDirt directly now that they have both GameLocation and their Tile data directly on them?



				// Find any tiles we were tracking at this location that no longer have a crop (flower) attached to them
				List<Vector2> harvestedFlowerLocations = entry.Key.terrainFeatures.Pairs
					.Where(tfp => entry.Value.Contains(tfp.Key) && tfp.Value is HoeDirt && (tfp.Value as HoeDirt).crop == null)
					.Select(x => x.Key)
					.ToList();

				if (harvestedFlowerLocations.Count == 0)
				{
					continue;
				}

				DebugLog($"{nameof(OnOneSecondUpdateTicked)} - Found {harvestedFlowerLocations.Count} harvested flowers @ {entry.Key.Name} location.\n"
					+ $"    Flower coords: {String.Join(" | ", harvestedFlowerLocations)}");

				// Remove the flower tile(s) from being tracked
				closeFlowerTileLocations[entry.Key].RemoveAll(x => harvestedFlowerLocations.Contains(x));

				List<SObject> beeHousesToUpdate = new();

				foreach (Vector2 tileLocation in harvestedFlowerLocations)
				{
					// Update any bee house within the effective range of the removed flower.
					beeHousesToUpdate.AddRange(beeHousesReady[entry.Key].Where(beeHouse => !beeHousesToUpdate.Contains(beeHouse) && IsWithinFlowerRange(beeHouse.TileLocation, tileLocation)));
				}

				if (beeHousesToUpdate.Count == 0)
				{
					continue;
				}

				UpdateLocationBeeHouses(entry.Key, beeHousesToUpdate);

				DebugLog($"{nameof(OnOneSecondUpdateTicked)} - Found {beeHousesToUpdate.Count} ready bee houses that need updating @ {entry.Key.Name} location.\n"
					+ $"    Bee house coords: {String.Join(" | ", beeHousesToUpdate.Select(x => x.TileLocation))}");
			}
		}

		/// <summary>
		/// Event handler for after objects are added/removed in any location (including machines, fences, etc).
		/// This doesn't apply for floating items (see DebrisListChanged) or furniture (see FurnitureListChanged).
		/// This event isn't raised for objects already present when a location is added. If you need to handle those too, use `LocationListChanged` and check `e.Added → objects`.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
		{
			// Check the location and objects similar to how we location-filter elsewhere
			if (!e.Removed.Any()
				|| !e.Location.IsOutdoors
				|| (!beeHousesReady.ContainsKey(e.Location) && !beeHousesReadyToday.ContainsKey(e.Location))
				|| !e.Removed.Any(x => x.Value.QualifiedItemId == beeHouseQualifiedItemID))
			{
				return;
			}

			// Find all removed bee houses so we can remove them from our tracking dictionaries
			IEnumerable<SObject> removedBeeHouses = e.Removed.Select(y => y.Value).Where(z => z.QualifiedItemId == beeHouseQualifiedItemID);
			DebugLog($"{nameof(OnObjectListChanged)} - Found {removedBeeHouses.Count()} bee houses to attempt to remove from tracking");

			if (beeHousesReady.ContainsKey(e.Location) && beeHousesReady[e.Location].Any(x => removedBeeHouses.Contains(x)))
			{
				beeHousesReady[e.Location].RemoveAll(x => removedBeeHouses.Contains(x));
				DebugLog($"{nameof(OnObjectListChanged)} - {e.Location} location has {beeHousesReady[e.Location].Count} remaining tracked ready bee houses");
			}

			if (beeHousesReadyToday.ContainsKey(e.Location) && beeHousesReadyToday[e.Location].Any(x => removedBeeHouses.Contains(x)))
			{
				beeHousesReadyToday[e.Location].RemoveAll(x => removedBeeHouses.Contains(x));
				DebugLog($"{nameof(OnObjectListChanged)} - {e.Location} location has {beeHousesReadyToday[e.Location].Count} remaining tracked ready-today bee houses");
			}
		}

		/// <summary>Event handler for after a game location is added or removed (including building interiors).</summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event arguments.</param>
		internal static void OnLocationListChanged(object sender, LocationListChangedEventArgs e)
		{
			foreach (GameLocation addedLocation in e.Added.Where(x => isLocationWithBeeHouses(x)))
			{
				// If we somehow have the location as a key already, reset their lists before we (re-)add the location
				if (beeHousesReady.ContainsKey(addedLocation))
				{
					beeHousesReady[addedLocation].Clear();
				}

				if (beeHousesReadyToday.ContainsKey(addedLocation))
				{
					beeHousesReadyToday[addedLocation].Clear();
				}

				if (closeFlowerTileLocations.ContainsKey(addedLocation))
				{
					closeFlowerTileLocations[addedLocation].Clear();
				}

				AddLocation(addedLocation);
			}

			// Clear any data we are tracking about this location
			foreach (GameLocation removedLocation in e.Removed.Where(x => beeHousesReady.ContainsKey(x) || beeHousesReadyToday.ContainsKey(x)))
			{
				if (beeHousesReady.ContainsKey(removedLocation))
				{
					beeHousesReady.Remove(removedLocation);
				}

				if (beeHousesReadyToday.ContainsKey(removedLocation))
				{
					beeHousesReadyToday.Remove(removedLocation);
				}

				if (closeFlowerTileLocations.ContainsKey(removedLocation))
				{
					closeFlowerTileLocations.Remove(removedLocation);
				}
			}
		}

		/// <summary>
		/// Refresh the held object in all ready-for-harvest bee houses.
		/// This will refresh the icon shown overtop the bee houses.
		/// This can be used in cases such as if the bee houses should now be showing a different icon
		/// due to a config value that would affect the assigned item being changed.
		/// </summary>
		public static void RefreshBeeHouseHeldObjects()
		{
			DebugLog($"{nameof(RefreshBeeHouseHeldObjects)} - Started");

			foreach (KeyValuePair<GameLocation, List<SObject>> kvp in beeHousesReady)
			{
				UpdateLocationBeeHouses(kvp.Key, kvp.Value);
			}

			DebugLog($"{nameof(RefreshBeeHouseHeldObjects)} - Ended");
		}

		/// <summary>
		/// Updates the honey held by the given ready-for-harvest bee houses, which are at the given location.
		/// This also adds any nearby flowers to our tracked list of them.
		/// </summary>
		/// <param name="location">The location of the ready bee houses.</param>
		/// <param name="readyBeeHouses">The bee houses which are ready to be harvested which we should update the honey of.</param>
		private static void UpdateLocationBeeHouses(GameLocation location, List<SObject> readyBeeHouses)
		{
			DebugLog($"{nameof(UpdateLocationBeeHouses)} - Started");

			ObjectDataDefinition objectData = ItemRegistry.GetObjectTypeDefinition();
			List<SObject> invalidBeeHouses = new();

			foreach (SObject beeHouse in readyBeeHouses)
			{
				// If a bee house no longer qualifies, we'll remove it after we go through the list we were given
				if (beeHouse == null || !beeHouse.readyForHarvest.Value || beeHouse.QualifiedItemId != beeHouseQualifiedItemID)
				{
					invalidBeeHouses.Add(beeHouse);

					continue;
				}

				// Same flower check the game uses (see `MachineDataUtility.GetNearbyFlowerItemId()`) when collecting the honey out of the bee house
				Crop closeFlower = Utility.findCloseFlower(location, beeHouse.TileLocation, flowerRange, (Crop crop) => !crop.forageCrop.Value);
				SObject flowerIngredient = null;

				// If we found a qualifying flower crop, then get its harvested object form.
				if (closeFlower != null)
				{
					string flowerIngredientID = ItemRegistry.QualifyItemId(closeFlower.indexOfHarvest.Value);

					if (flowerIngredientID == null)
					{
						Monitor.Log($"Failed to get the qualified item ID of a nearby flower from the flower's `indexOfHarvest.Value` value of '{closeFlower.indexOfHarvest.Value}'.", LogLevel.Warn);
				}
				else
				{
						string itemCreationFailureMessage = $"Failed to create an `Item` (and then convert it to `Object`) via `ItemRegistry.Create` using a nearby flower's qualified item ID of '{flowerIngredientID}'.";

						// `StardewValley.Internal.ItemQueryResolver.ItemQueryResolver.DefaultResolvers.FLAVORED_ITEM()` has this in a `try/catch`, so mimicking that here 
						try
					{
							// If this comes back as `null` or the conversion fails (resulting in `null`), that's fine since we'll just get "Wild Honey" back when we attempt to create flavored honey below.
							flowerIngredient = ItemRegistry.Create(flowerIngredientID) as SObject;

							if (flowerIngredient == null)
							{
								Monitor.Log(itemCreationFailureMessage, LogLevel.Warn);
					}
				}
						catch (Exception ex)
				{
							Monitor.Log(itemCreationFailureMessage + $"\n\nException ({ex.GetType().Name}): {ex.Message}", LogLevel.Error);
				}
				}
				}

				/*
				We set the held honey either to the default honey item (such as if there are no nearby flowers or all nearby flowers were harvested),
				or to an object that will inform the player about what they'll receive at time of harvest due to a qualifying full-grown flower being nearby enough.

				If the player's mod config specifies to show the nearby flower (the default), we use the flower ingredient object we created.
				Otherwise, if they changed their option to show artisan honey, we'll use the flower ingredient to create the flavored honey object.
				Note that the user will need to have another mod to provide custom icons for artisan honey items for this option to show anything different.

				The game will create its own honey object at harvest to return to the farmer, so whatever we have the bee house hold in the meantime won't affect gameplay in any way.
				Previous to SD v1.6, though, the game only updated some of the held honey object's properties; it didn't create a new object,
				so having a ready-to-harvest bee house hold a flower object - and the farmer still receive honey upon harvest - was not an option without additional programming.

				Note that the ingredient passed to `ObjectDataDefinition.CreateFlavoredHoney()` being `null` is fine for honey as it will return the base/default "Wild Honey" object.
				Ref: `Object.CheckForActionOnMachine()`
				*/
				beeHouse.heldObject.Value = flowerIngredient != null && ModEntry.Config.BeeHouseReadyIconEnum == ModConfig.ReadyIcon.Flower
					? flowerIngredient
					: objectData.CreateFlavoredHoney(flowerIngredient);

				if (closeFlower != null)
				{
					// Track the tile location of the `HoeDirt` that holds the flower's `Crop` object so we can watch for it being harvested later.
					if (!closeFlowerTileLocations.ContainsKey(location) || !closeFlowerTileLocations[location].Contains(closeFlower.Dirt.Tile))
					{
						if (!closeFlowerTileLocations.ContainsKey(location))
						{
							closeFlowerTileLocations.Add(location, new List<Vector2>());
						}

						closeFlowerTileLocations[location].Add(closeFlower.Dirt.Tile);

						DebugLog($"Now tracking tile {closeFlower.Dirt.Tile} for nearby grown flower affecting bee house @ {beeHouse.TileLocation} tile @ {location.Name} location");
					}
				}

				DebugLog($"Assigned {beeHouse.heldObject.Value.Name} to bee house @ {beeHouse.TileLocation} tile @ {location.Name} location");
			}

			// Remove any invalid bee houses from the given list
			readyBeeHouses.RemoveAll(x => invalidBeeHouses.Contains(x));

			DebugLog($"{nameof(UpdateLocationBeeHouses)} - Ended");
		}

		/// <summary>
		/// Adds bee houses in the given location to our lists of bee houses.
		/// For "ready" bee houses, will also update the bee houses, which also adds flowers nearby to the bee houses to our tracked list.
		/// </summary>
		/// <param name="location">The location to add to tracking and immediately start tracking thing at.</param>
		private static void AddLocation(GameLocation location)
		{
			List<SObject> ready = location.Objects.Values.Where(x => x.QualifiedItemId == beeHouseQualifiedItemID && x.readyForHarvest.Value).ToList();
			List<SObject> readyToday = location.Objects.Values.Where(x => x.QualifiedItemId == beeHouseQualifiedItemID
				&& !x.readyForHarvest.Value && x.MinutesUntilReady <= maxMinutesAwake).ToList();

			if (ready.Count > 0)
			{
				beeHousesReady.Add(location, ready);
				UpdateLocationBeeHouses(location, ready);

				DebugLog($"{nameof(AddLocation)} - Found and updated {ready.Count} ready bee houses "
					+ (closeFlowerTileLocations.ContainsKey(location) ? $"and {closeFlowerTileLocations[location].Count} close flowers" : String.Empty)
					+ $" @ {location.Name} location");
			}

			if (readyToday.Count > 0)
			{
				beeHousesReadyToday.Add(location, readyToday);

				DebugLog($"{nameof(AddLocation)} - Found {ready.Count} bee houses that will be ready today @ {location.Name} location");
			}
		}

		/// <summary>Checks if a given location is within the effective range of a flower./// </summary>
		/// <param name="checkLocation">The tile location to check.</param>
		/// <param name="flowerLocation">The location of the flower.</param>
		/// <returns>True if the location is within range, False if not.</returns>
		internal static bool IsWithinFlowerRange(Vector2 checkLocation, Vector2 flowerLocation)
		{
			// Start with a quick check to see if it's in a square of the radius size since that's much faster to check
			if (!(checkLocation.X <= flowerLocation.X + flowerRange && checkLocation.X >= Math.Max(flowerLocation.X - flowerRange, 0)
				&& checkLocation.Y <= flowerLocation.Y + flowerRange && checkLocation.Y >= Math.Max(flowerLocation.Y - flowerRange, 0)))
			{
				return false;
			}

			int yCheck = 0;
			int xCheck = flowerRange;

			// This does kind of "middle out" checking of the diamond shape so we hit the horizontal rows with the most tiles first.
			// We start with the full-width middle row, then check the row above AND below that one at once, but with one less tile on each horizontal side,
			// then continue checking above and below those ones, each time checking less horizontal tiles, until we finish by checking the topmost tile and bottommost tile.
			// In testing, doing it this way takes on average about half the checks versus scanning from topmost tile down each row until bottommost tile.
			while (yCheck <= flowerRange)
			{
				if ((checkLocation.Y == flowerLocation.Y + yCheck || (yCheck != 0 && checkLocation.Y == Math.Max(flowerLocation.Y - yCheck, 0)))
					&& checkLocation.X >= Math.Max(flowerLocation.X - xCheck, 0)
					&& checkLocation.X <= flowerLocation.X + xCheck)
				{
					return true;
				}

				yCheck += 1;
				xCheck -= 1;
			}

			return false;
		}

		internal static void TestIsWithinFlowerRange(bool shouldTestDebugLocations = true, bool shouldTestRandomLocations = false)
		{
			// NOTE - If testing this function elsewhere (such as https://dotnetfiddle.net), will need to include
			// the 'MonoGame.Framework.Gtk' v3.8.0 Nuget package, add `using Microsoft.Xna.Framework;`, and declare the `flowerRange` const int.

			// This location should have at least double the `flowerRange` value for both axis to not break the below debug locations.
			Vector2 flower = new(flowerRange * 2, flowerRange * 2);

			// Debug Locations - these should show whether the algorithm is working or not
			System.Collections.Generic.List<Vector2> insideDiamondLocations = new() {
				new Vector2(flower.X, flower.Y + flowerRange),
				new Vector2(flower.X, flower.Y - flowerRange),
				new Vector2(flower.X - flowerRange, flower.Y),
				new Vector2(flower.X + flowerRange, flower.Y),
				new Vector2(flower.X + flowerRange / 2, flower.Y + flowerRange / 2),
				new Vector2(flower.X - flowerRange / 2, flower.Y + flowerRange / 2),
				new Vector2(flower.X - flowerRange / 2, flower.Y - flowerRange / 2),
				new Vector2(flower.X + flowerRange / 2, flower.Y - flowerRange / 2),
				new Vector2(flower.X - 1, flower.Y + flowerRange - 1),
				new Vector2(flower.X + 1, flower.Y + flowerRange - 1),
				new Vector2(flower.X - 1, flower.Y - flowerRange + 1),
				new Vector2(flower.X + 1, flower.Y - flowerRange + 1),
				new Vector2(flower.X - flowerRange + 1, flower.Y - 1),
				new Vector2(flower.X + flowerRange - 1, flower.Y - 1),
				new Vector2(flower.X - flowerRange + 1, flower.Y + 1),
				new Vector2(flower.X + flowerRange - 1, flower.Y + 1),
			};
			System.Collections.Generic.List<Vector2> outsideDiamondInsideSquareLocations = new() {
				new Vector2(flower.X + flowerRange, flower.Y + flowerRange),
				new Vector2(flower.X - flowerRange, flower.Y + flowerRange),
				new Vector2(flower.X - flowerRange, flower.Y - flowerRange),
				new Vector2(flower.X + flowerRange, flower.Y - flowerRange),
				new Vector2(flower.X + flowerRange, flower.Y + 1),
				new Vector2(flower.X + flowerRange, flower.Y - 1),
				new Vector2(flower.X - flowerRange, flower.Y + 1),
				new Vector2(flower.X - flowerRange, flower.Y - 1),
				new Vector2(flower.X + 1, flower.Y + flowerRange),
				new Vector2(flower.X - 1, flower.Y + flowerRange),
				new Vector2(flower.X + 1, flower.Y - flowerRange),
				new Vector2(flower.X - 1, flower.Y - flowerRange),
			};
			System.Collections.Generic.List<Vector2> outsideSquareLocations = new() {
				new Vector2(flower.X, flower.Y + flowerRange + 1),
				new Vector2(flower.X, flower.Y - flowerRange - 1),
				new Vector2(flower.X - flowerRange - 1, flower.Y),
				new Vector2(flower.X + flowerRange + 1, flower.Y),
				new Vector2(flower.X + flowerRange + 1, flower.Y + flowerRange + 1),
				new Vector2(flower.X - flowerRange - 1, flower.Y + flowerRange + 1),
				new Vector2(flower.X - flowerRange - 1, flower.Y - flowerRange - 1),
				new Vector2(flower.X + flowerRange + 1, flower.Y - flowerRange - 1),
			};

			// Random Locations - these can test real-world speed differences between algorithms
			System.Collections.Generic.List<Vector2> randomLocations = new();

			// Can mess with this to test checking locations at various max distances from the flower location
			int maxDistanceAway = flowerRange * 2;

			int minX = Math.Max(Convert.ToInt32(flower.X) - maxDistanceAway, 0);
			int maxX = Convert.ToInt32(flower.X) + maxDistanceAway;
			int minY = Math.Max(Convert.ToInt32(flower.Y) - maxDistanceAway, 0);
			int maxY = Convert.ToInt32(flower.Y) + maxDistanceAway;
			Random rand = new();

			for (int i = 0; i < 50; i++)
			{
				randomLocations.Add(new Vector2(rand.Next(minX, maxX + 1), rand.Next(minY, maxY + 1)));
			}

			System.Collections.Generic.List<string> fails = new();

			System.Collections.Generic.List<string> ins = new();
			System.Collections.Generic.List<string> outs = new();

			// TESTING STARTS

			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

			if (shouldTestDebugLocations)
			{
				Console.WriteLine("DEBUG LOCATIONS\n-- Within Diamond --");
				foreach (Vector2 test in insideDiamondLocations)
				{
					bool result = IsWithinFlowerRange(test, flower);

					if (!result)
					{
						fails.Add($"{test}");
					}
				}
				if (fails.Count > 0)
				{
					Console.WriteLine($"FAILS: {String.Join(" | ", fails)}");
					fails.Clear();
				}

				Console.WriteLine("\n-- Outside Diamond, but Inside Square --");
				foreach (Vector2 test in outsideDiamondInsideSquareLocations)
				{
					bool result = IsWithinFlowerRange(test, flower);

					if (result)
					{
						fails.Add($"{test}");
					}
				}
				if (fails.Count > 0)
				{
					Console.WriteLine($"FAILS: {String.Join(" | ", fails)}");
					fails.Clear();
				}

				Console.WriteLine("\n-- Outside Square --");
				foreach (Vector2 test in outsideSquareLocations)
				{
					bool result = IsWithinFlowerRange(test, flower);

					if (result)
					{
						fails.Add($"{test}");
					}
				}
				if (fails.Count > 0)
				{
					Console.WriteLine($"FAILS: {String.Join(" | ", fails)}");
					fails.Clear();
				}

				sw.Stop();
				Console.WriteLine($"\nTested {insideDiamondLocations.Count + outsideDiamondInsideSquareLocations.Count + outsideSquareLocations.Count} locations in {sw.ElapsedTicks} ticks ({sw.ElapsedMilliseconds}ms)");
			}

			if (shouldTestRandomLocations)
			{
				if (shouldTestDebugLocations)
				{
					sw.Start();
				}

				Console.WriteLine("\n\nRANDOMLY GENERATED LOCATIONS");
				foreach (Vector2 test in randomLocations)
				{
					bool result = IsWithinFlowerRange(test, flower);

					if (result)
					{
						ins.Add($"{test}");
					}
					else
					{
						outs.Add($"{test}");
					}
				}
				sw.Stop();

				Console.WriteLine($"Tested {randomLocations.Count} randomly generated locations in {sw.ElapsedTicks} ticks ({sw.ElapsedMilliseconds}ms)");
				Console.WriteLine($"Ins: {ins.Count} | Outs: {outs.Count}");
			}
		}
	}
}
