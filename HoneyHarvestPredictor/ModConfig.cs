﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HoneyHarvestPredictor
{
	public sealed class ModConfig
	{
		public enum ReadyIcon
		{
			Flower,
			Honey
		}

		private const ReadyIcon defaultBeeHouseReadyIcon = ReadyIcon.Flower;

		internal ReadyIcon BeeHouseReadyIconEnum { get; private set; } = defaultBeeHouseReadyIcon;
		public string BeeHouseReadyIcon
		{
			get => Enum.GetName(BeeHouseReadyIconEnum) ?? $"{nameof(ReadyIcon.Flower)}";

			set
			{
				if (Enum.TryParse(value, true, out ReadyIcon parsed))
				{
					BeeHouseReadyIconEnum = parsed;

					return;
				}

				BeeHouseReadyIconEnum = defaultBeeHouseReadyIcon;
			}
		}
	}
}
