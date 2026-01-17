using System;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess.Tabers;

public class TermContent : FactoryBase, IDataEntity
{
	public string Term { get; set; }

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Term = GetStringValue(reader, "Term");
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}
}
