using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	internal class Constants
	{
		public const string HoneyObjectUnqualifiedIndentifier = "340";
		public static readonly string HoneyObjectQualifiedIndentifier = $"(O){HoneyObjectUnqualifiedIndentifier}";
		public const string HoneyObjectParentAssetName = "Data/Objects";

		public static string ModDataKey_HasColoredLabel => $"{ModEntry.ModID}_has_colored_label";

		#if DEBUG
			// For debug builds, show log messages as DEBUG so they show in the SMAPI console.
			public const LogLevel BuildLogLevel = LogLevel.Debug;
		#else
			public const LogLevel BuildLogLevel = LogLevel.Trace;
		#endif
	}
}
