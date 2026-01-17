using System.Collections.Generic;
using R2V2.Core.Configuration;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Infrastructure.Settings;

public static class SettingsInitializer
{
	public static void Initialize(List<SettingGroup> configurationSettings)
	{
		SettingsInitializerBase sib = new SettingsInitializerBase(configurationSettings);
		sib.SetValues<R2UtilitiesSettings>(configurationSettings);
		sib.SetValues<IndexTermHighlightSettings>(configurationSettings);
		sib.SetValues<TabersTermHighlightSettings>(configurationSettings);
	}
}
