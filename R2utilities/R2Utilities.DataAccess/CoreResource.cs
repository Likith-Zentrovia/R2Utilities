using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class CoreResource : FactoryBase, IDataEntity
{
	public int ResourceId { get; set; }

	public string Isbn { get; set; }

	public void Populate(SqlDataReader reader)
	{
		ResourceId = GetInt32Value(reader, "iResourceId", 0);
		Isbn = GetStringValue(reader, "vchResourceIsbn");
	}
}
