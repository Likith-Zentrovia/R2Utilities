using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;

namespace R2Utilities.DataAccess;

public class ResourceSpecialtyDataService : DataServiceBase
{
	public IList<ResourceSpecialty> GetResourceSpecialty(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("select s.iSpecialtyId, s.vchSpecialtyCode, s.vchSpecialtyName, s.tiRecordStatus ").Append("from   tResourceSpecialty rs ").Append(" join  dbo.tSpecialty s on s.iSpecialtyId = rs.iSpecialtyId ")
			.Append("where  rs.iResourceId = @ResourceId ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return GetEntityList<ResourceSpecialty>(sql.ToString(), parameters, logSql: true);
	}

	public int Insert(int resourceId, string specialtyCode, string creatorId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("SpecialtyCode", specialtyCode)
		};
		StringBuilder insert = new StringBuilder().Append("insert into tResourceSpecialty (iResourceId, iSpecialtyId, vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate, tiRecordStatus) ").AppendFormat("    select {0}, s.iSpecialtyId, '{1}', getdate(), null, null, 1 ", resourceId, creatorId).Append("    from   tSpecialty s")
			.Append("    where  vchSpecialtyCode = @SpecialtyCode ");
		int rowCount = ExecuteUpdateStatement(insert.ToString(), parameters.ToArray(), logSql: true);
		FactoryBase.Log.DebugFormat("insert row count: {0}", rowCount);
		return rowCount;
	}
}
