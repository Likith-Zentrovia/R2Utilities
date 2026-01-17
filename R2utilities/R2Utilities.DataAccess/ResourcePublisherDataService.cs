using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;

namespace R2Utilities.DataAccess;

public class ResourcePublisherDataService : DataServiceBase
{
	public IList<ResourcePublisher> GetResourcePubslihers(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("SELECT publisher.iPublisherId, publisher.vchPublisherName, publisher.iConsolidatedPublisherId ").Append("FROM tResource AS r ").Append("INNER JOIN tPublisher AS publisher ON r.iPublisherId = publisher.iPublisherId ")
			.Append("WHERE (r.iResourceId = @ResourceId) AND (publisher.iPublisherId IS NOT NULL) ")
			.Append("AND (publisher.tiRecordStatus = 1) ")
			.Append("UNION ")
			.Append("SELECT publisher.iPublisherId, publisher.vchPublisherName, publisher.iConsolidatedPublisherId ")
			.Append("FROM tResource AS r ")
			.Append("INNER JOIN tPublisher AS p ON r.iPublisherId = p.iPublisherId ")
			.Append("LEFT OUTER JOIN tPublisher AS publisher ON publisher.iConsolidatedPublisherId = p.iPublisherId ")
			.Append("WHERE (r.iResourceId = @ResourceId) AND (publisher.iPublisherId IS NOT NULL) ")
			.Append("AND (publisher.tiRecordStatus = 1) ")
			.Append("UNION ")
			.Append("SELECT publisher.iPublisherId, publisher.vchPublisherName, publisher.iConsolidatedPublisherId ")
			.Append("FROM tResource AS r ")
			.Append("INNER JOIN tPublisher AS p ON r.iPublisherId = p.iPublisherId ")
			.Append("LEFT OUTER JOIN tPublisher AS publisher ON publisher.iPublisherId = p.iConsolidatedPublisherId ")
			.Append("WHERE (r.iResourceId = @ResourceId) AND (publisher.iPublisherId IS NOT NULL) ")
			.Append("AND (publisher.tiRecordStatus = 1) ")
			.Append("UNION ")
			.Append("SELECT publisher.iPublisherId, publisher.vchPublisherName, publisher.iConsolidatedPublisherId ")
			.Append("FROM tResource AS r ")
			.Append("INNER JOIN tPublisher AS p ON r.iPublisherId = p.iPublisherId ")
			.Append("LEFT OUTER JOIN tPublisher AS mp ON mp.iPublisherId = p.iConsolidatedPublisherId ")
			.Append("LEFT OUTER JOIN tPublisher AS publisher ON publisher.iConsolidatedPublisherId = mp.iPublisherId ")
			.Append("WHERE (r.iResourceId = @ResourceId) AND (publisher.iPublisherId IS NOT NULL) ")
			.Append("AND (publisher.tiRecordStatus = 1) ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return GetEntityList<ResourcePublisher>(sql.ToString(), parameters, logSql: true);
	}
}
