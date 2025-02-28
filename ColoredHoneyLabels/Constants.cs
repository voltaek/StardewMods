﻿using Microsoft.Xna.Framework;
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
		public static readonly Color WildHoneyLabelColor = Color.White;
		public static readonly Color SaveCompatibilityLabelColor = Color.White;

		public static string ModDataKey_LabelColorPackedValue => $"{ModEntry.ModID}_label_color_packed_value";

		#if DEBUG
			// For debug builds, show log messages as DEBUG so they show in the SMAPI console.
			public const LogLevel BuildLogLevel = LogLevel.Debug;
		#else
			public const LogLevel BuildLogLevel = LogLevel.Trace;
		#endif
	}
}
