using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels.Models
{
	public class SpriteData
	{
		public string? TextureName { get; set; }

		public int SpriteIndex { get; set; } = 0;

		public bool ColorOverlayFromNextIndex { get; set; } = true;

		private string? displayName;
		public string? DisplayName
		{
			get
			{
				return String.IsNullOrEmpty(displayName) ? TextureName : displayName;
			}

			set
			{
				displayName = value?.Trim();
			}
		}
	}
}
