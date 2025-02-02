using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels.Models
{
	internal class InternalSpriteData : SpriteData
	{
		public string AssetDictionaryKey { get; set; }
		public string ImagePath { get; set; }
		public bool IsDefault { get; set; } = false;
		public bool IsDebug { get; set; } = false;

		public InternalSpriteData(string textureName, string displayName, string assetDictionaryKey, string imagePath)
		{
			TextureName = textureName;
			DisplayName = displayName;
			AssetDictionaryKey = assetDictionaryKey;
			ImagePath = imagePath;
		}
	}
}
