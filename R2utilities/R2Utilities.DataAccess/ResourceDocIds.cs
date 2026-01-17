using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourceDocIds : FactoryBase, IDataEntity
{
	public int Id { get; set; }

	public int MinDocId { get; set; }

	public int MaxDocId { get; set; }

	public void Populate(SqlDataReader reader)
	{
		Id = GetInt32Value(reader, "iResourceId", -1);
		MinDocId = GetInt32Value(reader, "iMinDocumentId", -1);
		MaxDocId = GetInt32Value(reader, "iMaxDocumentId", -1);
	}
}
