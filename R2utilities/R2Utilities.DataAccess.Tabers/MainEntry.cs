using System;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess.Tabers;

public class MainEntry : FactoryBase, IDataEntity
{
	public string Name { get; set; }

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Name = GetStringValue(reader, "Name");
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}
}
