using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	internal class Utility
	{
		/// <summary>
		/// Shift a given color to a close, but distinct color. The new color should ideally be not too close to the next closest primary color.
		/// </summary>
		/// <param name="color">The color to shift</param>
		/// <returns>The shifted color.</returns>
		public static Color ShiftColor(Color color)
		{
			StardewValley.Utility.RGBtoHSL(color.R, color.G, color.B, out double colorHue, out double colorSat, out double colorLum);

			// Darken bright colors
			if (colorLum > 0.75)
			{
				colorLum -= 0.15;
			}
			// Hue-shift warm colors a little since their primary colors are close in hue number
			else if (colorHue < 90)
			{
				colorHue += 15;
			}
			// Hue-shift other colors more
			else
			{
				colorHue += 30;
			}

			StardewValley.Utility.HSLtoRGB(colorHue, colorSat, colorLum, out int colorRed, out int colorGreen, out int colorBlue);

			return new Color(colorRed, colorGreen, colorBlue);
		}
	}
}
