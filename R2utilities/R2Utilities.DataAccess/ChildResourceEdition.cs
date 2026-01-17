using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ChildResourceEdition : FactoryBase, IDataEntity
{
	public int ResourceId { get; set; }

	public string Isbn { get; set; }

	public int PrevEditResourceId { get; set; }

	public int? LatestEditResourceId { get; set; }

	public void Populate(SqlDataReader reader)
	{
		ResourceId = GetInt32Value(reader, "iResourceId", 0);
		PrevEditResourceId = GetInt32Value(reader, "iPrevEditResourceID", 0);
		LatestEditResourceId = GetInt32Value(reader, "iLatestEditResourceId");
		Isbn = GetStringValue(reader, "vchResourceIsbn");
	}
}
