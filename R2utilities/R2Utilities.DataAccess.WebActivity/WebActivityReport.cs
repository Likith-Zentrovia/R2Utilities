using System.Collections.Generic;

namespace R2Utilities.DataAccess.WebActivity;

public class WebActivityReport
{
	public int PageRequests { get; set; }

	public int AveragePageRequestTime { get; set; }

	public int MedianPageRequestTime { get; set; }

	public int AllContentRequests { get; set; }

	public int TocRequests { get; set; }

	public int ContentRequests { get; set; }

	public int TurnawayConcurrency { get; set; }

	public int TurnawayAccess { get; set; }

	public int PrintRequests { get; set; }

	public int EmailRequests { get; set; }

	public int SessionCount { get; set; }

	public int SearchCount { get; set; }

	public int SearchTimeAverage { get; set; }

	public int SearchTimeMax { get; set; }

	public NumberOfRequestTimes NumberOfRequestTimes { get; set; }

	public IEnumerable<TopInstitution> TopInstitutionPageRequests { get; set; }

	public IEnumerable<TopInstitutionResource> TopInstitutionResourceRequests { get; set; }

	public IEnumerable<TopResource> TopResources { get; set; }

	public IEnumerable<TopIpAddress> TopInstitutionIpRanges { get; set; }

	public IEnumerable<TopIpAddress> TopIpRanges { get; set; }

	public IEnumerable<TopInstitutionResource> TopInstitutionResourcePrintRequests { get; set; }

	public IEnumerable<TopInstitutionResource> TopInstitutionResourceEmailRequests { get; set; }

	public IEnumerable<TopInstitution> TopInstitutionSessionRequests { get; set; }
}
