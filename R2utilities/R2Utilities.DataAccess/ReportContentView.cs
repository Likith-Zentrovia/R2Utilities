using System;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.R2Utility.DataServices;

namespace R2Utilities.DataAccess;

public class ReportContentView : DataServiceBase, IDataEntity
{
	public int ContentId { get; set; }

	public int InstitutionId { get; set; }

	public int UserId { get; set; }

	public int ResourceId { get; set; }

	public string Isbn { get; set; }

	public string ChapterSection { get; set; }

	public long IpAddressInteger { get; set; }

	public DateTime ContentViewTimestamp { get; set; }

	public void Populate(SqlDataReader reader)
	{
		ContentId = GetInt32Value(reader, "contentTurnawayId", 0);
		InstitutionId = GetInt32Value(reader, "institutionId", 0);
		UserId = GetInt32Value(reader, "userId", 0);
		ResourceId = GetInt32Value(reader, "resourceId", 0);
		Isbn = GetStringValue(reader, "vchResourceISBN");
		ChapterSection = GetStringValue(reader, "chapterSectionId");
		IpAddressInteger = GetInt64Value(reader, "ipAddressInteger", 0L);
		ContentViewTimestamp = GetDateValue(reader, "contentViewTimestamp");
	}
}
