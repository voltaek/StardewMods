using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestSync
{
	internal sealed class ModEntry : Mod
	{
		/// <summary>The mod entry point, called after the mod is first loaded.</summary>
		/// <param name="helper">Provides simplified APIs for writing mods.</param>
		public override void Entry(IModHelper helper)
		{
			// Hold onto the monitor so we can do logging
			HoneyUpdater.Monitor = Monitor;

			// Rig up the event handlers we need to do proper tracking of bee houses and flowers
			Helper.Events.GameLoop.DayStarted += HoneyUpdater.OnDayStarted;
			Helper.Events.GameLoop.TimeChanged += HoneyUpdater.OnTimeChanged;
			Helper.Events.GameLoop.OneSecondUpdateTicked += HoneyUpdater.OnOneSecondUpdateTicked;
			Helper.Events.World.ObjectListChanged += HoneyUpdater.OnObjectListChanged;
			Helper.Events.World.LocationListChanged += HoneyUpdater.OnLocationListChanged;
		}
	}
}
