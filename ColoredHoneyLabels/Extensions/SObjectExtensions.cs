using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels.Extensions
{
	internal static class SObjectExtensions
	{
		/// <summary>Store the label color value in the object's mod data.</summary>
		/// <param name="obj">The object to store in.</param>
		/// <param name="labelColor">The color to store.</param>
		internal static void StoreLabelColor(this SObject obj, Color labelColor)
		{
			if (!obj.modData.TryAdd(Constants.ModDataKey_LabelColorPackedValue, labelColor.PackedValue.ToString()))
			{
				obj.modData[Constants.ModDataKey_LabelColorPackedValue] = labelColor.PackedValue.ToString();
			}
		}

		/// <summary>Whether the object has a label color stored in its mod data.</summary>
		/// <param name="obj">The object to check.</param>
		internal static bool HasStoredLabelColor(this SObject obj)
		{
			return obj.modData.ContainsKey(Constants.ModDataKey_LabelColorPackedValue);
		}

		/// <summary>Attempt to fetch the label color potentially stored in the object's mod data.</summary>
		/// <param name="obj">The object to use.</param>
		/// <returns>Either the retrieved color or `null` if the object has no color stored or the value failed to parse for use.</returns>
		internal static Color? TryGetStoredLabelColor(this SObject obj)
		{
			if (obj.HasStoredLabelColor() && UInt32.TryParse(obj.modData[Constants.ModDataKey_LabelColorPackedValue], out UInt32 packedValue))
			{
				return new(packedValue);
			}

			return null;
		}
	}
}
