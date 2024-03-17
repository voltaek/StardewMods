﻿using StardewModdingAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestSync
{
	public sealed class ModConfig
	{
		public enum ReadyIcon
		{
			Flower,
			Honey
		}

		// TEMP FORCE HONEY UNTIL SD v1.6
		//private const ReadyIcon defaultBeeHouseReadyIcon = ReadyIcon.Flower;
		private const ReadyIcon defaultBeeHouseReadyIcon = ReadyIcon.Honey;

		internal ReadyIcon BeeHouseReadyIconEnum { get; private set; } = defaultBeeHouseReadyIcon;
		public string BeeHouseReadyIcon
		{
			// get => Enum.GetName(BeeHouseReadyIconEnum);
			// TEMP FORCE HONEY UNTIL SD v1.6
			get => Enum.GetName(defaultBeeHouseReadyIcon);

			set
			{
				/* TEMP FORCE HONEY UNTIL SD v1.6
				if (Enum.TryParse(value, true, out ReadyIcon parsed))
				{
					BeeHouseReadyIconEnum = parsed;

					return;
				}
				*/

				BeeHouseReadyIconEnum = defaultBeeHouseReadyIcon;
			}
		}
	}
}