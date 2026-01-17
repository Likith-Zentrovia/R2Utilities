using System;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourcePracticeArea : FactoryBase, IDataEntity
{
	public virtual int Id { get; set; }

	public virtual int ResourceId { get; set; }

	public virtual short RecordStatus { get; set; }

	public string Name { get; set; }

	public string Code { get; set; }

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Id = GetInt32Value(reader, "iPracticeAreaId", -1);
			Code = GetStringValue(reader, "vchPracticeAreaCode");
			Name = GetStringValue(reader, "vchPracticeAreaName");
			RecordStatus = GetByteValue(reader, "tiRecordStatus", 0);
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}
}
