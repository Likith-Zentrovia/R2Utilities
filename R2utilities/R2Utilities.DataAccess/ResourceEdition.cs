using System.Collections.Generic;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourceEdition : FactoryBase, IDataEntity
{
	public int ResourceId { get; set; }

	public string Isbn { get; set; }

	public int PrevEditResourceId { get; set; }

	public int NewEditResourceId { get; set; }

	public string Edition { get; set; }

	public string Title { get; set; }

	public List<ChildResourceEdition> ResourcesToSetLatestEdition { get; set; }

	public void Populate(SqlDataReader reader)
	{
		ResourceId = GetInt32Value(reader, "iResourceId", 0);
		PrevEditResourceId = GetInt32Value(reader, "iPrevEditResourceID", 0);
		Isbn = GetStringValue(reader, "vchResourceIsbn");
	}
}
