using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;

namespace R2Utilities.DataAccess;

public class ResourceFileDataService : DataServiceBase
{
	private readonly string _deleteAll;

	private readonly string _deleteByResourseIdStatement;

	private readonly string _insert;

	private readonly bool _logSql;

	private readonly string _selectByResourceId;

	private readonly string _selectResourceDocIds;

	private readonly string _selectResourceDocIdsByResourceId;

	private readonly string _tableName;

	private readonly string _truncate;

	public ResourceFileDataService(string tableName, bool logSql)
	{
		_logSql = logSql;
		_tableName = tableName;
		_insert = new StringBuilder().AppendFormat("insert into {0} (iResourceId, vchFileNameFull, vchFileNamePart1, vchFileNamePart3, iDocumentId ", tableName).Append("      , vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate, tiRecordStatus) ").Append("values (@ResourceId, @FileNameFull, @FileNamePart1, @FileNamePart3, @DocumentId")
			.Append("      , @CreatorId, @CreationDate, @UpdaterId, @LastUpdate, @RecordStatus);")
			.ToString();
		_deleteByResourseIdStatement = "delete from " + tableName + " where iResourceId = @ResourceId;";
		_deleteAll = "delete from " + tableName;
		_truncate = "truncate table " + tableName;
		_selectByResourceId = new StringBuilder().Append("select iResourceFileId, iResourceId, vchFileNameFull, vchFileNamePart1, vchFileNamePart3, iDocumentId, vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate, tiRecordStatus ").AppendFormat("from   dbo.{0} ", tableName).Append("where iResourceId = @ResourceId ")
			.ToString();
		_selectResourceDocIds = new StringBuilder().Append("select r.iResourceId as [iResourceId], min(rf.iDocumentId) as [iMinDocumentId], max(rf.iDocumentId) as [iMaxDocumentId] ").Append("from dbo.tResource r ").AppendFormat(" join  dbo.{0} rf on r.iResourceId = rf.iResourceId ", tableName)
			.Append("group by r.iResourceId ")
			.Append("order by iResourceId;")
			.ToString();
		_selectResourceDocIdsByResourceId = new StringBuilder().Append("select r.iResourceId as [iResourceId], min(rf.iDocumentId) as [iMinDocumentId], max(rf.iDocumentId) as [iMaxDocumentId] ").Append("from dbo.tResource r ").AppendFormat(" join  dbo.{0} rf on r.iResourceId = rf.iResourceId ", tableName)
			.Append("where r.iResourceId = @ResourceId ")
			.Append("group by r.iResourceId ")
			.ToString();
	}

	public int DeleteByResourceId(int resourceId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return ExecuteUpdateStatement(_deleteByResourseIdStatement, parameters, logSql: true);
	}

	public int DeleteBatch(int[] docIds, int resourceId)
	{
		if (docIds.Length == 0)
		{
			FactoryBase.Log.WarnFormat("DeleteBatch() - docIds is EMPTY!!!");
			return 0;
		}
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		StringBuilder inClause = new StringBuilder();
		for (int i = 0; i < docIds.Length; i++)
		{
			inClause.AppendFormat("{0}{1}", (i == 0) ? " and iDocumentId in (" : ",", docIds[i]);
		}
		inClause.Append(")");
		string sql = _deleteByResourseIdStatement.Replace(";", inClause.ToString());
		return ExecuteUpdateStatement(sql, parameters, logSql: true);
	}

	public int DeleteAll()
	{
		ISqlCommandParameter[] parameters = new ISqlCommandParameter[0];
		return ExecuteUpdateStatement(_deleteAll, parameters, logSql: true);
	}

	public int TruncateTable()
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		return ExecuteUpdateStatement(_truncate, parameters, logSql: true);
	}

	public int Insert(ResourceFile resourceFile)
	{
		Stopwatch stopwatch = new Stopwatch();
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
			{
				new Int32Parameter("ResourceId", resourceFile.ResourceId),
				new StringParameter("FileNameFull", resourceFile.FilenameFull),
				new StringParameter("FileNamePart1", resourceFile.FilenamePart1),
				new StringParameter("FileNamePart3", resourceFile.FilenamePart3),
				new Int32Parameter("DocumentId", resourceFile.DocumentId),
				new StringParameter("CreatorId", resourceFile.CreatedBy),
				new DateTimeParameter("CreationDate", resourceFile.CreationDate),
				new StringParameter("UpdaterId", resourceFile.UpdatedBy),
				new DateTimeNullParameter("LastUpdate", resourceFile.LastUpdated),
				new Int32Parameter("RecordStatus", resourceFile.StatusId)
			};
			cnn = GetConnection();
			command = GetSqlCommand(cnn, _insert, parameters);
			if (_logSql)
			{
				LogCommandDebug(command);
				stopwatch.Start();
			}
			int rows = command.ExecuteNonQuery();
			if (_logSql)
			{
				stopwatch.Stop();
				FactoryBase.Log.DebugFormat("rows effected: {0}, insert time: {1}ms", rows, stopwatch.ElapsedMilliseconds);
			}
			return rows;
		}
		catch (Exception ex)
		{
			LogCommandInfo(command);
			FactoryBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	public int InsertBatch(IEnumerable<ResourceFile> resourceFiles, int maxBatchSize)
	{
		int insertCount = 0;
		int batchInsertCount = 1;
		List<ResourceFile> batch = new List<ResourceFile>();
		foreach (ResourceFile resourceFile in resourceFiles)
		{
			batch.Add(resourceFile);
			if (batch.Count >= maxBatchSize)
			{
				insertCount += InsertBatchLimited(batch);
				FactoryBase.Log.DebugFormat("batchInsertCount: {0}", batchInsertCount);
				batchInsertCount++;
				batch.Clear();
			}
		}
		if (batch.Count > 0)
		{
			insertCount += InsertBatchLimited(batch);
			FactoryBase.Log.DebugFormat("batchInsertCount: {0}", batchInsertCount);
		}
		return insertCount;
	}

	public int InsertBatchLimited(IEnumerable<ResourceFile> resourceFiles)
	{
		Stopwatch stopwatch = new Stopwatch();
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			StringBuilder insert = new StringBuilder();
			List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
			int x = 0;
			foreach (ResourceFile resourceFile in resourceFiles)
			{
				insert.AppendFormat("insert into {0} (iResourceId, vchFileNameFull, vchFileNamePart1, vchFileNamePart3, iDocumentId ", _tableName).Append(" , vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate, tiRecordStatus) ").AppendFormat("values (@ResourceId_{0}, @FileNameFull_{0}, @FileNamePart1_{0}, @FileNamePart3_{0}, @DocumentId_{0} ", x)
					.AppendFormat(" , @CreatorId_{0}, @CreationDate_{0}, @UpdaterId_{0}, @LastUpdate_{0}, @RecordStatus_{0});", x);
				parameters.Add(new Int32Parameter($"ResourceId_{x}", resourceFile.ResourceId));
				parameters.Add(new StringParameter($"FileNameFull_{x}", resourceFile.FilenameFull));
				parameters.Add(new StringParameter($"FileNamePart1_{x}", resourceFile.FilenamePart1));
				parameters.Add(new StringParameter($"FileNamePart3_{x}", resourceFile.FilenamePart3));
				parameters.Add(new Int32Parameter($"DocumentId_{x}", resourceFile.DocumentId));
				parameters.Add(new StringParameter($"CreatorId_{x}", resourceFile.CreatedBy));
				parameters.Add(new DateTimeParameter($"CreationDate_{x}", resourceFile.CreationDate));
				parameters.Add(new StringParameter($"UpdaterId_{x}", resourceFile.UpdatedBy));
				parameters.Add(new DateTimeNullParameter($"LastUpdate_{x}", resourceFile.LastUpdated));
				parameters.Add(new Int32Parameter($"RecordStatus_{x}", resourceFile.StatusId));
				x++;
			}
			cnn = GetConnection();
			command = GetSqlCommand(cnn, insert.ToString(), parameters);
			if (_logSql)
			{
				LogCommandDebug(command);
				stopwatch.Start();
			}
			int rows = command.ExecuteNonQuery();
			if (_logSql)
			{
				stopwatch.Stop();
				FactoryBase.Log.DebugFormat("rows effected: {0}, insert time: {1}ms", rows, stopwatch.ElapsedMilliseconds);
			}
			return rows;
		}
		catch (Exception ex)
		{
			LogCommandInfo(command);
			FactoryBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	public IList<ResourceFile> GetResourceFiles(int resourceId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return GetEntityList<ResourceFile>(_selectByResourceId, parameters, logSql: true);
	}

	public ResourceDocIds GetResourceDocIds(int resourceId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return GetFirstEntity<ResourceDocIds>(_selectResourceDocIdsByResourceId, parameters, logSql: true);
	}

	public IList<ResourceDocIds> GetAllResourceDocIds()
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		return GetEntityList<ResourceDocIds>(_selectResourceDocIds, parameters, logSql: true);
	}
}
