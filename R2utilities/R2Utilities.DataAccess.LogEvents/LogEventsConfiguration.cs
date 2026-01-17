using System.Collections.Generic;

namespace R2Utilities.DataAccess.LogEvents;

public class LogEventsConfiguration
{
	public string TableName { get; set; }

	public List<ReportConfiguration> ReportConfigurations { get; set; }
}
