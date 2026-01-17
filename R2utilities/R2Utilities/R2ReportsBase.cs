using R2Library.Data.ADO.Config;

namespace R2Utilities;

public class R2ReportsBase : R2UtilitiesBase
{
	public R2ReportsBase()
	{
		base.ConnectionString = DbConfigSettings.Settings.R2ReportsConnection;
	}
}
