using System;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourceIsbn : FactoryBase, IDataEntity
{
	public int TypeId { get; set; }

	public string Isbn { get; set; }

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Isbn = GetStringValue(reader, "vchIsbn");
			TypeId = GetByteValue(reader, "iResourceIsbnTypeId", 0);
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}
}
