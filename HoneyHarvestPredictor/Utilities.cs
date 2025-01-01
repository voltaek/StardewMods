using Microsoft.Xna.Framework;
using StardewValley.GameData.Machines;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestPredictor
{
	internal static class Utilities
	{
		/// <summary>
		/// Returns a string the uniquely identifies the current in-game day.
		/// </summary>
		internal static string UniqueDay
		{
			get { return $"Year_{Game1.year}-{Game1.season}-Day_{Game1.dayOfMonth}"; }
		}

		/// <summary>
		/// Shorthand property for creating a verbose log entry header.
		/// We want to use the verbose log method directly for best performance, both when actually using verbose and not.
		/// </summary>
		internal static string VerboseStart
		{
			// Show microsecond, so we can tell if something is slow.
			get { return ModEntry.Logger.IsVerbose ? DateTime.Now.ToString("ffffff") : String.Empty; }
		}

		/// <summary>
		/// Shorthand method for creating a standard log entry, depending on debug build or not.
		/// </summary>
		/// <param name="message">The message to log.</param>
		internal static void Log(string message) => ModEntry.Logger.Log(message, Constants.buildLogLevel);

		internal static int MinutesUntilEndOfDay
		{
			get { return Constants.maxMinutesAwake - Utility.CalculateMinutesBetweenTimes(Constants.startOfDayTime, Game1.timeOfDay); }
		}

		internal static MachineData BeeHouseMachineData
		{
			get { return DataLoader.Machines(Game1.content).GetValueOrDefault(Constants.beeHouseQualifiedItemID); }
		}

		private static bool areAllBeeHouseOutputRulesByDay = false;
		private static string beeHouseDailyRefreshCheckTimestamp = String.Empty;

		/// <summary>Whether or not we can be sure that bee houses only refresh overnight.</summary>
		internal static bool DoBeeHousesOnlyRefreshDaily
		{
			get
			{
				if (beeHouseDailyRefreshCheckTimestamp == UniqueDay)
				{
					return areAllBeeHouseOutputRulesByDay;
				}

				// We have to check two things to determine if bee houses only refresh in full day increments AKA only at 6am each morning:
				// * If all the rules have days defined (AKA are not -1), then days are used, regardless of if minutes are defined or not.
				// * If any ready-time modifiers are defined, then we can't assume anything about when bee houses will be ready.
				areAllBeeHouseOutputRulesByDay = (BeeHouseMachineData?.OutputRules?.All(rule => rule.DaysUntilReady >= 0) ?? false)
					&& ((BeeHouseMachineData?.ReadyTimeModifiers?.Count ?? 0) == 0);
				
				// Note when we last checked so we check fresh each day, just in case something changes.
				beeHouseDailyRefreshCheckTimestamp = UniqueDay;

				return areAllBeeHouseOutputRulesByDay;
			}
		}

		/// <summary>
		/// Whether the given item has characteristics that identify it as a potential honey-flavor source.
		/// </summary>
		/// <param name="item">The item to check.</param>
		/// <returns>True if the item can flavor honey, False if not.</returns>
		internal static bool IsHoneyFlavorSource(Item item)
		{
			// The base game data has the flowers category and the "flower_item" tag on crop flowers and some forage flowers, but not all.
			// Better Beehouses tags the four base game forage flowers all with "honey_source".
			return item.Category == SObject.flowersCategory || item.HasContextTag("flower_item") || item.HasContextTag("honey_source") || ModEntry.Compat.IsAnythingHoney;
		}

		/// <summary>Filter to test locations with to see if they can and do have relevant bee houses in them.</summary>
		/// <param name="location">The location to test.</param>
		/// <returns>Whether the location has bee houses.</returns>
		internal static bool IsLocationWithBeeHouses(GameLocation location)
		{
			return (location.IsOutdoors || ModEntry.Compat.SyncIndoorBeeHouses) && location.Objects.Values.Any(x => x.QualifiedItemId == Constants.beeHouseQualifiedItemID);
		}

		/// <summary>
		/// This uses a base game method that handles all of our needs (caching + inside locs), plus will do a `LogOnce` for a location if it can't be found.
		/// We can't really trust a Location property on - for example - a TerrainFeature or ResourceClump since it gets set to `null` when they're removed by the game
		/// from its location's list of them, so we fetch location instances ourselves instead of trying to use an instance's location property.
		/// </summary>
		/// <param name="locationName">The game's name for a location</param>
		/// <returns>The `GameLocation` object if found; `null` if not.</returns>
		internal static GameLocation FetchLocationByName(string locationName)
		{
			// This base game method will get from cache where possible and handles locations which are buildings.
			GameLocation location = Game1.getLocationFromName(locationName);

			if (location == null)
			{
				ModEntry.Logger.LogOnce($"Failed to get GameLocation with location name '{locationName}'. Will be unable to refresh bee houses in this location.", LogLevel.Warn);
			}

			return location;
		}

		/// <summary>
		/// Output text directly to the console - only for debug builds - using optional specific text and background colors.
		/// </summary>
		/// <param name="message">The message to output to the console. Gets no prefixing like SMAPI might do.</param>
		/// <param name="textColor">Optional. The text color to use for the message. Defaults to the 'DarkGray' that SMAPI uses for DEBUG and TRACE logs.</param>
		/// <param name="backColor">Optional. The background color to use for the message. If the default 'Black' is left or passed, will skip setting the background color.</param>
		[Conditional("DEBUG")]
		internal static void DebugConsoleLog(string message, ConsoleColor textColor = ConsoleColor.DarkGray, ConsoleColor backColor = ConsoleColor.Black)
		{
			if (backColor != ConsoleColor.Black)
			{
				Console.BackgroundColor = backColor;
			}

			Console.ForegroundColor = textColor;
			Console.WriteLine(message);
			Console.ResetColor();
		}

		/// <summary>Checks if a given location is within the effective range of a flower.</summary>
		/// <param name="checkLocation">The tile location to check.</param>
		/// <param name="flowerLocation">The location of the flower.</param>
		/// <returns>True if the location is within range, False if not.</returns>
		internal static bool IsWithinFlowerRange(Vector2 checkLocation, Vector2 flowerLocation)
		{
			return Vector2.Distance(checkLocation, flowerLocation) <= ModEntry.Compat.FlowerRange;
		}

		internal static void TestIsWithinFlowerRange(bool shouldTestDebugLocations = true, bool shouldTestRandomLocations = false)
		{
			// NOTE - If testing this function elsewhere (such as https://dotnetfiddle.net), will need to include
			// the 'MonoGame.Framework.Gtk' v3.8.0 Nuget package, add `using Microsoft.Xna.Framework;`, and set `flowerRange` to a constant value.

			int flowerRange = ModEntry.Compat.FlowerRange;

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
				Console.WriteLine($"\nTested {insideDiamondLocations.Count + outsideDiamondInsideSquareLocations.Count + outsideSquareLocations.Count} locations "
					+ $"in {sw.ElapsedTicks} ticks ({sw.ElapsedMilliseconds}ms)");
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
