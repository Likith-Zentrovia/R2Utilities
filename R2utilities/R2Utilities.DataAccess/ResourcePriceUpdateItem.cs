using System;
using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourcePriceUpdateItem : FactoryBase, IDataEntity
{
	public virtual int Id { get; set; }

	public virtual string ResourceIsbn { get; set; }

	public virtual decimal ListPrice { get; set; }

	public virtual DateTime UpdateDate { get; set; }

	public virtual string CreatedBy { get; set; }

	public virtual DateTime CreationDate { get; set; }

	public virtual string UpdatedBy { get; set; }

	public virtual DateTime? LastUpdated { get; set; }

	public virtual short RecordStatus { get; set; }

	public void Populate(SqlDataReader reader)
	{
		Id = GetInt32Value(reader, "iResourcePriceUpdateId", -1);
		ResourceIsbn = GetStringValue(reader, "vchResourceISBN");
		ListPrice = GetDecimalValue(reader, "decResourcePrice", 0m);
		UpdateDate = GetDateValue(reader, "dtUpdateDate");
		CreatedBy = GetStringValue(reader, "vchCreatorId");
		CreationDate = GetDateValue(reader, "dtCreationDate");
		UpdatedBy = GetStringValue(reader, "vchUpdaterId");
		LastUpdated = GetDateValueOrNull(reader, "dtLastUpdate");
		RecordStatus = GetByteValue(reader, "tiRecordStatus", 0);
	}
}
