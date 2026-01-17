using System.Collections.Generic;

namespace R2Utilities.DataAccess.LogEvents;

public class ReportConfiguration
{
	public string Name { get; set; }

	public int LevelInt { get; set; }

	public string FieldSelect { get; set; }

	public string WhereClause { get; set; }

	public string Grouping { get; set; }

	public string OrderByColumnNumber { get; set; }

	public int PerDayAlertThreshHold { get; set; }

	public int ReportedItems { get; set; }

	public List<LogEvent> LogEvents { get; set; }

	public int TotalLogEvents { get; set; }

	public string Having { get; set; }
}
