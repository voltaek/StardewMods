using ColoredHoneyLabels.Integrations;
using ColoredHoneyLabels.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColoredHoneyLabels
{
	public sealed class ModConfig
	{
		public string? SpriteDataKey { get; set; } = AssetManager.DefaultSpriteDataKey;

		public bool MoreLabelColorVariety { get; set; } = false;

		internal void Register(IModHelper helper, IManifest manifest)
		{
			// Get Generic Mod Config Menu's API (if it's installed)
			IGenericModConfigMenuApi? configMenu = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

			if (configMenu is null)
			{
				return;
			}

			// Register mod
			configMenu.Register(
				mod: manifest,
				reset: () => ModEntry.Config = new(),
				save: () => helper.WriteConfig(this)
			);

			// Add each config value

			configMenu.AddTextOption(
				mod: manifest,
				name: () => "Honey Sprite",
				tooltip: () => "Select the honey sprite to use from this or other mods' compatible options.",
				getValue: () => SpriteDataKey ?? AssetManager.DefaultSpriteDataKey,
				setValue: value => {
					string? oldValue = SpriteDataKey;
					SpriteDataKey = value;

					ModEntry.Logger.Log($"Updated {nameof(SpriteDataKey)} config value via GMCM from {(oldValue == null ? "`null`" : $"'{oldValue}'")} to '{value}'", LogLevel.Debug);

					AssetManager.RefreshHoneyData();
				},
				allowedValues: AssetManager.AllSpriteData.Keys.ToArray(),
				formatAllowedValue: (value) => {
					if (AssetManager.AllSpriteData.TryGetValue(value, out SpriteData? data))
					{
						return data?.DisplayName ?? value;
					}

					return value;
				}
			);

			configMenu.AddBoolOption(
				mod: manifest,
				name: () => "More Label Color Variety",
				tooltip: () => "Enable this to slightly shift the label color of some honey types, resulting in a larger variety of label colors.",
				getValue: () => MoreLabelColorVariety,
				setValue: value => {
					bool oldValue = MoreLabelColorVariety;
					MoreLabelColorVariety = value;

					ModEntry.Logger.Log($"Updated {nameof(MoreLabelColorVariety)} config value via GMCM from '{oldValue}' to '{value}'", LogLevel.Debug);
				}
			);
		}
	}
}
