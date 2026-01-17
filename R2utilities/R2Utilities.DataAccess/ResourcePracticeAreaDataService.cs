using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;

namespace R2Utilities.DataAccess;

public class ResourcePracticeAreaDataService : DataServiceBase
{
	public IList<ResourcePracticeArea> GetResourcePracticeArea(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("select pa.iPracticeAreaId, pa.vchPracticeAreaCode, pa.vchPracticeAreaName, pa.tiRecordStatus ").Append("from   tResourcePracticeArea rpa ").Append(" join  dbo.tPracticeArea pa on pa.iPracticeAreaId = rpa.iPracticeAreaId and pa.tiRecordStatus = 1 ")
			.Append("where  rpa.iResourceId = @ResourceId and rpa.tiRecordStatus = 1 ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return GetEntityList<ResourcePracticeArea>(sql.ToString(), parameters, logSql: true);
	}

	public int Insert(int resourceId, string practiceAreaCode, string creatorId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("PracticeAreaCode", practiceAreaCode)
		};
		StringBuilder insert = new StringBuilder().Append("insert into tResourcePracticeArea (iResourceId, iPracticeAreaId, vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate, tiRecordStatus) ").AppendFormat("    select {0}, pa.iPracticeAreaId, '{1}', getdate(), null, null, 1 ", resourceId, creatorId).Append("    from   tPracticeArea pa ")
			.Append("    where  vchPracticeAreaCode = @PracticeAreaCode ");
		int rowCount = ExecuteUpdateStatement(insert.ToString(), parameters.ToArray(), logSql: true);
		FactoryBase.Log.DebugFormat("insert row count: {0}", rowCount);
		return rowCount;
	}
}
