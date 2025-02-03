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

		public int SpriteIndex { get; set; } = 0;

		public bool ColorOverlayFromNextIndex { get; set; } = true;

		public override string ToString()
		{
			return $"{nameof(TextureName)}: '{TextureName}' | "
				+ $"{nameof(DisplayName)}: '{displayName}' | "
				+ $"{nameof(SpriteIndex)}: '{SpriteIndex}' | "
				+ $"{nameof(ColorOverlayFromNextIndex)}: '{ColorOverlayFromNextIndex}'";
		}
	}
}
