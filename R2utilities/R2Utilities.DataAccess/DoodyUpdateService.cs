using System;
using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Logging;

namespace R2Utilities.DataAccess;

public class DoodyUpdateService : DataServiceBase
{
	private readonly ILog<DoodyUpdateService> _log;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public DoodyUpdateService(ILog<DoodyUpdateService> log, IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_log = log;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public List<CoreResource> GetDctCoreResources()
	{
		List<CoreResource> newDctResources = new List<CoreResource>();
		try
		{
			StringBuilder sqlBuilder = new StringBuilder();
			sqlBuilder.Append("select r.iResourceId, r.vchResourceIsbn ").Append("from tResource r ").Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 and dct.isEssential = 0 ")
				.Append("left join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 5 ")
				.Append("where rc.iResourceCollectionId is null ")
				.Append(" group by r.iResourceId, r.vchResourceIsbn");
			List<CoreResource> insertedDct = GetEntityList<CoreResource>(sqlBuilder.ToString(), new List<ISqlCommandParameter>(), logSql: true);
			newDctResources.AddRange(insertedDct);
			sqlBuilder = new StringBuilder();
			sqlBuilder.Append("select r.iResourceId, r.vchResourceIsbn ").Append("from tResource r ").Append("join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 5 and rc.tiRecordStatus = 0 ")
				.Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 and dct.isEssential = 0 ")
				.Append(" group by r.iResourceId, r.vchResourceIsbn");
			List<CoreResource> udpatedDct = GetEntityList<CoreResource>(sqlBuilder.ToString(), new List<ISqlCommandParameter>(), logSql: true);
			newDctResources.AddRange(udpatedDct);
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
		return newDctResources;
	}

	public List<CoreResource> GetDctEssentialCoreResources()
	{
		List<CoreResource> newDctResoruces = new List<CoreResource>();
		try
		{
			StringBuilder sqlBuilder = new StringBuilder();
			sqlBuilder.Append("select r.iResourceId, r.vchResourceIsbn ").Append("from tResource r ").Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 and dct.isEssential = 1")
				.Append("left join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 6 ")
				.Append("where rc.iResourceCollectionId is null ")
				.Append(" group by r.iResourceId, r.vchResourceIsbn");
			List<CoreResource> insertedDctEssential = GetEntityList<CoreResource>(sqlBuilder.ToString(), new List<ISqlCommandParameter>(), logSql: true);
			newDctResoruces.AddRange(insertedDctEssential);
			sqlBuilder = new StringBuilder();
			sqlBuilder.Append("select r.iResourceId, r.vchResourceIsbn ").Append("from tResource r ").Append("join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 6  and rc.tiRecordStatus = 0 ")
				.Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 and dct.isEssential = 1 ")
				.Append(" group by r.iResourceId, r.vchResourceIsbn");
			List<CoreResource> udpatedDctEssential = GetEntityList<CoreResource>(sqlBuilder.ToString(), new List<ISqlCommandParameter>(), logSql: true);
			newDctResoruces.AddRange(udpatedDctEssential);
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
		return newDctResoruces;
	}

	public void UpdateDct(out int inserted, out int updated, out int deleted)
	{
		try
		{
			DateTime date = DateTime.Now;
			string sql = new StringBuilder().Append("Insert into tResourceCollection (iCollectionId, iResourceId, vchCreatorId, dtCreationDate, tiRecordStatus) ").AppendFormat("select 5, r.iResourceId, 'UpdateDct', '{0}', 1 ", date).Append("from tResource r ")
				.Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 ")
				.Append("left join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 5 ")
				.Append("where rc.iResourceCollectionId is null ")
				.Append(" group by r.iResourceId")
				.ToString();
			inserted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select r.iResourceId, {0}, 'UpdateDct', getdate() ", 1).Append(" , '[Resource has become a Doody Core Title]' ")
				.Append(" from tResource r ")
				.Append(" join tResourceCollection rc on r.iResourceId = rc.iResourceId ")
				.AppendFormat(" where rc.vchCreatorId = 'UpdateDct' and rc.iCollectionId = 5 and rc.dtCreationDate = '{0}' ", date)
				.ToString();
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append("Update tResourceCollection ").Append("set tiRecordStatus = 1, ").Append("vchUpdaterId = 'UpdateDct', ")
				.AppendFormat("dtLastUpdate = '{0}' ", date)
				.Append("from tResource r ")
				.Append("join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 5 and rc.tiRecordStatus = 0 ")
				.Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 ")
				.ToString();
			updated = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select r.iResourceId, {0}, 'UpdateDct', getdate() ", 1).Append(" , '[Resource has become a Doody Core Title]' ")
				.Append(" from tResource r ")
				.Append(" join tResourceCollection rc on r.iResourceId = rc.iResourceId ")
				.AppendFormat(" where rc.vchUpdaterId = 'UpdateDct' and rc.iCollectionId = 5 and rc.dtLastUpdate = '{0}' ", date)
				.ToString();
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append("Update tResourceCollection ").Append("set tiRecordStatus = 0, ").Append("vchUpdaterId = 'UpdateDct', ")
				.AppendFormat("dtLastUpdate = '{0}' ", date)
				.Append("from tResource r ")
				.Append("join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 5 and rc.tiRecordStatus = 1 ")
				.Append("left join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 ")
				.Append("where  dct.isbn13 is null ")
				.ToString();
			deleted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	public void UpdateDctEssential(out int inserted, out int updated, out int deleted)
	{
		try
		{
			DateTime date = DateTime.Now;
			string sql = new StringBuilder().Append("Insert into tResourceCollection (iCollectionId, iResourceId, vchCreatorId, dtCreationDate, tiRecordStatus) ").AppendFormat("select 6, r.iResourceId, 'UpdateDctEssential', '{0}', 1 ", date).Append("from tResource r ")
				.Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 and dct.isEssential = 1")
				.Append("left join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 6 ")
				.Append("where rc.iResourceCollectionId is null ")
				.Append(" group by r.iResourceId")
				.ToString();
			inserted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append("Insert into tResourceCollection (iCollectionId, iResourceId, vchCreatorId, dtCreationDate, tiRecordStatus) ").AppendFormat("select 5, r.iResourceId, 'UpdateDctEssential', '{0}', 1 ", date).Append("from tResource r ")
				.Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 and dct.isEssential = 1")
				.Append("left join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 5 ")
				.Append("where rc.iResourceCollectionId is null ")
				.Append(" group by r.iResourceId")
				.ToString();
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select r.iResourceId, {0}, 'UpdateDctEssential', getdate() ", 1).Append(" , '[Resource has become a Doody Core Essential Purchase]' ")
				.Append(" from tResource r ")
				.Append(" join tResourceCollection rc on r.iResourceId = rc.iResourceId ")
				.AppendFormat(" where rc.vchCreatorId = 'UpdateDctEssential' and rc.dtCreationDate = '{0}'", date)
				.ToString();
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append("Update tResourceCollection ").Append("set tiRecordStatus = 1, ").Append("vchUpdaterId = 'DoodyUpdateTask', ")
				.Append("dtLastUpdate = GETDATE() ")
				.Append("from tResource r ")
				.Append("join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId in (5,6)  and rc.tiRecordStatus = 0 ")
				.Append("join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13 and dct.isEssential = 1 ")
				.ToString();
			updated = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select r.iResourceId, {0}, 'UpdateDctEssential', getdate() ", 1).Append(" , '[Resource has become a Doody Core Essential Purchase]' ")
				.Append(" from tResource r ")
				.Append(" join tResourceCollection rc on r.iResourceId = rc.iResourceId ")
				.AppendFormat(" where rc.vchUpdaterId = 'UpdateDctEssential' and rc.dtLastUpdate = '{0}' ", date)
				.ToString();
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append(" Update tResourceCollection ").Append(" set tiRecordStatus = 0, ").Append(" vchUpdaterId = 'UpdateDctEssential', ")
				.AppendFormat(" dtLastUpdate = '{0}' ", date)
				.Append("from tResource r ")
				.Append("join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 6 and rc.tiRecordStatus = 1 ")
				.Append("left join RittenhouseWeb..DoodyCoreTitleScore dct on r.vchIsbn13 = dct.isbn13  and dct.isEssential = 1 ")
				.Append("where dct.isbn13 is null ")
				.ToString();
			deleted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	public void UpdateDoodyReview(out int inserted, out int deleted)
	{
		try
		{
			DateTime date = DateTime.Now;
			string sql = new StringBuilder().Append("update tResource ").Append("set tiDoodyReview = 1 ").Append(" , vchUpdaterId = 'UpdateDoodyReview' ")
				.AppendFormat(" , dtLastUpdate = '{0}' ", date)
				.Append("from tResource r ")
				.Append("join RittenhouseWeb..Product p on r.vchIsbn13 = p.isbn13 ")
				.Append("where  p.doodyRating > 0 ")
				.Append("and p.sku not like 'R2P%' ")
				.Append("and r.tiDoodyReview = 0 ")
				.ToString();
			inserted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select iResourceId, {0}, 'UpdateDoodyReview', getdate() ", 1).Append(" , '[tiDoodyReview changed from 0 to 1]' ")
				.AppendFormat(" from tResource where dtLastUpdate = '{0}' and vchUpdaterId = 'UpdateDoodyReview' and tiDoodyReview = 1", date)
				.ToString();
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append("update tResource ").Append("set tiDoodyReview = 0 ").Append(" , vchUpdaterId = 'UpdateDoodyReview' ")
				.Append(" , dtLastUpdate = GETDATE() ")
				.Append("from tResource r ")
				.Append("join RittenhouseWeb..Product p on r.vchIsbn13 = p.isbn13 ")
				.Append("where p.doodyRating is null and r.tiDoodyReview = 1 ")
				.Append("and p.sku not like 'R2P%' ")
				.Append("and r.tiDoodyReview = 1 ")
				.ToString();
			deleted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select iResourceId, {0}, 'UpdateDoodyReview', getdate() ", 1).Append(" , '[tiDoodyReview changed from 1 to 0]' ")
				.AppendFormat(" from tResource where dtLastUpdate = '{0}' and vchUpdaterId = 'UpdateDoodyReview' and tiDoodyReview = 0", date)
				.ToString();
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyReview', GETDATE(), '[Inserted to iCollectionId 52]'\nfrom tResource r\nleft join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 52\nwhere rc.iCollectionId is null and r.tiDoodyReview = 1;\nInsert into tResourceCollection(iCollectionId, iResourceId, vchCreatorId, dtCreationDate, tiRecordStatus)\nselect 52, r.iResourceId, 'UpdateDoodyReview', GETDATE(), 1\nfrom tResource r\nleft join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 52\nwhere rc.iCollectionId is null and r.tiDoodyReview = 1;";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyReview', GETDATE(), '[Updated to iCollectionId 52]'\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and r.tiDoodyReview = 1\nwhere rc.tiRecordStatus = 0 and rc.iCollectionId = 52;\nUpdate tResourceCollection\nset tiRecordStatus = 1,\nvchUpdaterId = 'UpdateDoodyReview',\ndtLastUpdate = GETDATE()\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and r.tiDoodyReview = 1\nwhere rc.tiRecordStatus = 0 and rc.iCollectionId = 52;";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyReview', GETDATE(), '[Deleted from iCollectionId 52]'\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and r.tiDoodyReview = 0\nwhere rc.tiRecordStatus = 1 and rc.iCollectionId = 52\nUpdate tResourceCollection\nset tiRecordStatus = 0,\nvchUpdaterId = 'UpdateDoodyReview',\ndtLastUpdate = GETDATE()\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and r.tiDoodyReview = 0\nwhere rc.tiRecordStatus = 1 and rc.iCollectionId = 52";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	public void UpdateDoodyRating(out int inserted, out int deleted)
	{
		try
		{
			string sql = $"INSERT INTO tResourceAudit\nselect iResourceId, {1}, 'UpdateDoodyRating', getdate()\n, '[siDoodyRating changed from ' + isnull(cast(r.siDoodyRating as varchar(20)), 'null') + ' to ' + cast(p.doodyRating as varchar(20)) + ']'\nfrom tResource r\njoin RittenhouseWeb..Product p on r.vchIsbn13 = p.isbn13\nwhere p.doodyRating is not null and p.doodyRating > 0 and (r.siDoodyRating is null or r.siDoodyRating <> p.doodyRating) and p.sku not like 'R2P%'";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = "update tResource\nset siDoodyRating = p.doodyRating\n, vchUpdaterId = 'UpdateDoodyRating'\n, dtLastUpdate = GETDATE()\nfrom tResource r\njoin RittenhouseWeb..Product p on r.vchIsbn13 = p.isbn13\nwhere p.doodyRating is not null and p.doodyRating > 0 and(r.siDoodyRating is null or r.siDoodyRating <> p.doodyRating) and p.sku not like 'R2P%'";
			inserted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"INSERT INTO tResourceAudit\nselect iResourceId, {1}, 'UpdateDoodyRating', getdate()\n, '[siDoodyRating changed from ' + isnull(cast(r.siDoodyRating as varchar(20)), 'null') + ' to ' + isnull(cast(p.doodyRating as varchar(20)), 'null') + ']'\nfrom tResource r\njoin RittenhouseWeb..Product p on r.vchIsbn13 = p.isbn13\nwhere (p.doodyRating is null or p.doodyRating = 0) and (r.siDoodyRating is not null)\nand p.sku not like 'R2P%'";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = "update tResource\nset siDoodyRating = null\n, vchUpdaterId = 'UpdateDoodyRating'\n, dtLastUpdate = GETDATE()\nfrom tResource r\njoin RittenhouseWeb..Product p on r.vchIsbn13 = p.isbn13\nwhere (p.doodyRating is null or p.doodyRating = 0) and (r.siDoodyRating is not null)\nand p.sku not like 'R2P%'";
			deleted = ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyRating', GETDATE(), '[Inserted to iCollectionId 51]'\nfrom tResource r\nleft join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 51\nwhere rc.iCollectionId is null and (r.siDoodyRating is not null and r.siDoodyRating >= 90 );\nInsert into tResourceCollection(iCollectionId, iResourceId, vchCreatorId, dtCreationDate, tiRecordStatus)\nselect 51, r.iResourceId, 'UpdateDoodyRating', GETDATE(), 1\nfrom tResource r\nleft join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 51\nwhere rc.iCollectionId is null and (r.siDoodyRating is not null and r.siDoodyRating >= 90 );";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyRating', GETDATE(), '[Updated to iCollectionId 51]'\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is not null and r.siDoodyRating >= 90)\nwhere rc.tiRecordStatus = 0 and rc.iCollectionId = 51;\nUpdate tResourceCollection\nset tiRecordStatus = 1,\nvchUpdaterId = 'UpdateDoodyRating',\ndtLastUpdate = GETDATE()\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is not null and r.siDoodyRating >= 90)\nwhere rc.tiRecordStatus = 0 and rc.iCollectionId = 51;";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyRating', GETDATE(), '[Deleted from iCollectionId 51]'\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is null or r.siDoodyRating < 90)\nwhere rc.tiRecordStatus = 1 and rc.iCollectionId = 51\nUpdate tResourceCollection\nset tiRecordStatus = 0,\nvchUpdaterId = 'UpdateDoodyRating',\ndtLastUpdate = GETDATE()\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is null or r.siDoodyRating < 90)\nwhere rc.tiRecordStatus = 1 and rc.iCollectionId = 51";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyRating', GETDATE(), '[Inserted to iCollectionId 50]'\nfrom tResource r\nleft join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 50\nwhere rc.iCollectionId is null and (r.siDoodyRating is not null and r.siDoodyRating >= 97 );\nInsert into tResourceCollection(iCollectionId, iResourceId, vchCreatorId, dtCreationDate, tiRecordStatus)\nselect 50, r.iResourceId, 'UpdateDoodyRating', GETDATE(), 1\nfrom tResource r\nleft join tResourceCollection rc on r.iResourceId = rc.iResourceId and rc.iCollectionId = 50\nwhere rc.iCollectionId is null and (r.siDoodyRating is not null and r.siDoodyRating >= 97 );";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyRating', GETDATE(), '[Updated to iCollectionId 50]'\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is not null and r.siDoodyRating >= 97)\nwhere rc.tiRecordStatus = 0 and rc.iCollectionId = 50;\nUpdate tResourceCollection\nset tiRecordStatus = 1,\nvchUpdaterId = 'UpdateDoodyRating',\ndtLastUpdate = GETDATE()\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is not null and r.siDoodyRating >= 97)\nwhere rc.tiRecordStatus = 0 and rc.iCollectionId = 22;";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
			sql = $"\nINSERT INTO tResourceAudit\nselect r.iResourceId, {1}, 'UpdateDoodyRating', GETDATE(), '[Deleted from iCollectionId 50]'\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is null or r.siDoodyRating < 97)\nwhere rc.tiRecordStatus = 1 and rc.iCollectionId = 50\nUpdate tResourceCollection\nset tiRecordStatus = 0,\nvchUpdaterId = 'UpdateDoodyRating',\ndtLastUpdate = GETDATE()\nfrom tResourceCollection rc\njoin tResource r on rc.iResourceId = r.iResourceId and (r.siDoodyRating is null or r.siDoodyRating < 97)\nwhere rc.tiRecordStatus = 1 and rc.iCollectionId = 50;";
			ExecuteStatement(sql, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}
}
