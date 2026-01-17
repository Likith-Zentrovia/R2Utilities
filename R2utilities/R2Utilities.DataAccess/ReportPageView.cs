using System;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.R2Utility.DataServices;

namespace R2Utilities.DataAccess;

public class ReportPageView : DataServiceBase, IDataEntity
{
	public int InstitutionId { get; set; }

	public int UserId { get; set; }

	public long IpAddressInteger { get; set; }

	public DateTime PageViewTimeStamp { get; set; }

	public string Url { get; set; }

	public string SessionId { get; set; }

	public void Populate(SqlDataReader reader)
	{
		InstitutionId = GetInt32Value(reader, "institutionId", 0);
		UserId = GetInt32Value(reader, "userId", 0);
		IpAddressInteger = GetInt64Value(reader, "ipAddressInteger", 0L);
		PageViewTimeStamp = GetDateValue(reader, "pageViewTimestamp");
		Url = GetStringValue(reader, "url");
		SessionId = GetStringValue(reader, "sessionId");
	}
}
