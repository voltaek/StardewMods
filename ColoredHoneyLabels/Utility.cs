using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	internal static class Utility
	{
		/// <summary>
		/// Shorthand property for creating a verbose log entry header.
		/// We want to use the verbose log method directly for best performance, both when actually using verbose and not.
		/// </summary>
		internal static string VerboseStart
		{
			// Show microsecond, so we can tell if something is slow.
			get { return ModEntry.Logger.IsVerbose ? DateTime.Now.ToString("ffffff") : String.Empty; }
		}
	}
}
