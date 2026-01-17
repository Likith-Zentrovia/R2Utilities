using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Compression;

namespace R2Utilities.DataAccess;

public class ReportDataService : R2ReportsBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private static string DatabaseServerName { get; set; }

	private static string DatabaseUserName { get; set; }

	private static string DatabasePassword { get; set; }

	public ReportDataService(IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public List<ReportPageView> GetPageViews(DateTime startDate, DateTime endDate)
	{
		R2UtilitiesBase.Log.InfoFormat("GetPageViews(startDate: {0:u}, endDate: {1:u})", startDate, endDate);
		string sql = new StringBuilder().Append("select pv.institutionId, pv.userId, pv.ipAddressInteger, pv.pageViewTimestamp, pv.url, pv.sessionId ").AppendFormat("from [{0}]..PageView pv ", _r2UtilitiesSettings.R2ReportsDatabaseName).AppendFormat("join [{0}]..tInstitution i on pv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("where ((pv.url like '/Search?q%') or (pv.url like '/Resource/%')) ")
			.AppendFormat("and pv.pageViewTimestamp between '{0}' and '{1}' ", startDate, endDate)
			.Append("order by pv.pageViewTimestamp asc")
			.ToString();
		R2UtilitiesBase.Log.DebugFormat("sql: {0}", sql);
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		List<ReportPageView> items = new List<ReportPageView>();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			reader = command.ExecuteReader();
			while (reader.Read())
			{
				ReportPageView item = new ReportPageView();
				item.Populate(reader);
				items.Add(item);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.InfoFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, items.Count);
			return items;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command, reader);
		}
	}

	public List<ReportContentView> GetContentViews(DateTime startDate, DateTime endDate)
	{
		R2UtilitiesBase.Log.InfoFormat("GetContentViews(startDate: {0:u}, endDate: {1:u})", startDate, endDate);
		string sql = new StringBuilder().Append("select cv.contentTurnawayId, cv.institutionId, cv.userId, cv.resourceId, r.vchResourceISBN,  ").Append("cv.chapterSectionId, cv.ipAddressInteger, cv.contentViewTimestamp ").AppendFormat("from [{0}]..ContentView cv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendFormat("join [{0}]..tResource r on cv.resourceId = r.iResourceId ", _r2UtilitiesSettings.R2DatabaseName)
			.AppendFormat("join [{0}]..tInstitution i on cv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.AppendFormat("where cv.contentViewTimestamp between '{0}' and '{1}' ", startDate, endDate)
			.Append("and cv.actionTypeId = 0 and cv.foundFromSearch = 0 ")
			.Append("order by cv.contentViewTimestamp asc")
			.ToString();
		R2UtilitiesBase.Log.DebugFormat("sql: {0}", sql);
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		List<ReportContentView> items = new List<ReportContentView>();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			reader = command.ExecuteReader();
			while (reader.Read())
			{
				ReportContentView item = new ReportContentView();
				item.Populate(reader);
				items.Add(item);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.InfoFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, items.Count);
			return items;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command, reader);
		}
	}

	public int SaveContentViews(Dictionary<ReportContentView, string> foundSearchResourceHits)
	{
		StringBuilder sql = new StringBuilder();
		int count = 0;
		int totalCount = 0;
		foreach (KeyValuePair<ReportContentView, string> item in foundSearchResourceHits)
		{
			string updateSql = string.Format("Update [{0}]..ContentView set foundFromSearch = 1, searchTerm = '{1}' where contentTurnawayId = {2}; ", _r2UtilitiesSettings.R2ReportsDatabaseName, item.Value.Replace("'", "''"), item.Key.ContentId);
			sql.Append(updateSql);
			count++;
			totalCount++;
			if (count == 100)
			{
				int rows = ExecuteStatement(sql.ToString(), logSql: true, base.ConnectionString);
				R2UtilitiesBase.Log.DebugFormat("rows: {0}, sql: {1}", rows, sql);
				sql = new StringBuilder();
				count = 0;
			}
		}
		if (sql.Length > 0)
		{
			ExecuteStatement(sql.ToString(), logSql: true, base.ConnectionString);
		}
		return totalCount;
	}

	public int ExecuteAggregateInsert(string insertStatement)
	{
		Stopwatch stopwatch = new Stopwatch();
		SqlConnection cnn = null;
		SqlCommand command = null;
		string sql = insertStatement + ";";
		try
		{
			cnn = GetConnection(base.ConnectionString);
			command = GetSqlCommand(cnn, sql);
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			LogCommandDebug(command);
			stopwatch.Start();
			int rows = command.ExecuteNonQuery();
			stopwatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("rows effected: {0}, insert time: {1}ms", rows, stopwatch.ElapsedMilliseconds);
			return rows;
		}
		catch (Exception ex)
		{
			LogCommandInfo(command);
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	public int GetDailyContentViewCountRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("DailyContentViewCount", "contentViewDate", startOfMonth);
	}

	public int GetDailyContentTurnawayCountRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("DailyContentTurnawayCount", "contentTurnawayDate", startOfMonth);
	}

	public int GetDailyPageViewCountRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("DailyPageViewCount", "pageViewDate", startOfMonth);
	}

	public int GetDailySearchCountRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("DailySearchCount", "searchDate", startOfMonth);
	}

	public int GetDailySessionCountRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("DailySessionCount", "sessionDate", startOfMonth);
	}

	public int GetContentViewRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("ContentView", "contentViewTimestamp", startOfMonth);
	}

	public int GetPageViewRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("PageView", "pageViewTimestamp", startOfMonth);
	}

	public int GetSearchRecordCount(DateTime startOfMonth)
	{
		return GetRecordCount("Search", "searchTimestamp", startOfMonth);
	}

	private int GetRecordCount(string tableName, string dateFieldName, DateTime startOfMonth)
	{
		R2UtilitiesBase.Log.InfoFormat("GetRecordCount(tableName: {0}, dateFieldName: {1}, startOfMonth: {2:u})", tableName, dateFieldName, startOfMonth);
		string sql = new StringBuilder().AppendFormat("select count(*) from [{0}]..{1} ", _r2UtilitiesSettings.R2ReportsDatabaseName, tableName).Append(GetWhereClauseDateFilter(dateFieldName, startOfMonth)).ToString();
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		int count = ExecuteBasicCountQuery(sql, parameters, logSql: true);
		R2UtilitiesBase.Log.InfoFormat("GetRecordCount() - count: {0}", count);
		return count;
	}

	private string GetWhereClauseDateFilter(string dateFieldName, DateTime startOfMonth)
	{
		return string.Format(" where  {0} >= '{1:MM/dd/yyyy} 00:00:00' and {0} < '{2:MM/dd/yyyy} 00:00:00' ", dateFieldName, startOfMonth, startOfMonth.AddMonths(1));
	}

	public DateTime GetNewestDailyPageView()
	{
		Stopwatch stopwatch = new Stopwatch();
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			string sql = "\nselect top 1 pageViewDate\nfrom DailyPageViewCount\norder by 1 desc\n";
			cnn = GetConnection(base.ConnectionString);
			command = GetSqlCommand(cnn, sql);
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			LogCommandDebug(command);
			stopwatch.Start();
			SqlDataReader reader = command.ExecuteReader();
			DateTime contentViewDate = DateTime.Now;
			while (reader.Read())
			{
				contentViewDate = GetDateValue(reader, "pageViewDate");
			}
			stopwatch.Stop();
			return contentViewDate;
		}
		catch (Exception ex)
		{
			LogCommandInfo(command);
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	public DateTime GetOldestContentView()
	{
		Stopwatch stopwatch = new Stopwatch();
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			string sql = "\nselect top 1 contentViewTimestamp\nfrom ContentView\norder by 1 asc\n";
			cnn = GetConnection(base.ConnectionString);
			command = GetSqlCommand(cnn, sql);
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			LogCommandDebug(command);
			stopwatch.Start();
			SqlDataReader reader = command.ExecuteReader();
			DateTime contentViewDate = DateTime.Now;
			while (reader.Read())
			{
				contentViewDate = GetDateValue(reader, "contentViewTimestamp");
			}
			stopwatch.Stop();
			return contentViewDate;
		}
		catch (Exception ex)
		{
			LogCommandInfo(command);
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	public int AggregateContentViewCount(DateTime startOfMonth)
	{
		R2UtilitiesBase.Log.InfoFormat("AggregateContentViewCount(startOfMonth: {0:u})", startOfMonth);
		int recordsDeleted = DeleteRangeFromTable(startOfMonth, "DailyContentViewCount", "contentViewDate");
		R2UtilitiesBase.Log.InfoFormat("AggregateContentViewCount() - recordsDeleted: {0}", recordsDeleted);
		string sql = new StringBuilder().AppendFormat("Insert Into [{0}]..DailyContentViewCount(institutionId, userId, resourceId, chapterSectionId, ", _r2UtilitiesSettings.R2ReportsDatabaseName).Append(" ipAddressOctetA, ipAddressOctetB, ipAddressOctetC, ipAddressOctetD, ipAddressInteger, contentViewDate, contentViewCount, actionTypeId, foundFromSearch ").Append(" , licenseType, resourceStatusId, uniqueContentViewCount)")
			.Append("select cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC ")
			.Append("        , cv.ipAddressOctetD, cv.ipAddressInteger, cast(cv.contentViewTimestamp as date) as hitDate, count(*), cv.actionTypeId, cv.foundFromSearch ")
			.Append("       , cv.licenseType, isnull(cv.resourceStatusId, 0), count(distinct pv.sessionId) ")
			.AppendFormat("from   [{0}]..ContentView cv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendFormat("left join   [{0}]..PageView pv on pv.requestId = cv.requestId", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.Append(GetWhereClauseDateFilter("cv.contentViewTimestamp", startOfMonth))
			.Append("  and  cv.turnawayTypeId = 0 ")
			.Append("group by cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC ")
			.Append("    , cv.ipAddressOctetD, cv.ipAddressInteger, cast(cv.contentViewTimestamp as date), cv.actionTypeId, cv.foundFromSearch, cv.licenseType, isnull(cv.resourceStatusId, 0) ")
			.ToString();
		int rows = ExecuteAggregateInsert(sql);
		R2UtilitiesBase.Log.InfoFormat("AggregateContentViewCount() - rows inserted: {0}", rows);
		return rows;
	}

	public int AggregateContentViewTurnawayCount(DateTime startOfMonth)
	{
		R2UtilitiesBase.Log.InfoFormat("AggregateContentViewTurnawayCount(startOfMonth: {0:u})", startOfMonth);
		int recordsDeleted = DeleteRangeFromTable(startOfMonth, "DailyContentTurnawayCount", "contentTurnawayDate");
		R2UtilitiesBase.Log.InfoFormat("AggregateContentViewTurnawayCount() - recordsDeleted: {0}", recordsDeleted);
		string sql = new StringBuilder().AppendFormat("Insert Into [{0}]..DailyContentTurnawayCount(institutionId, userId, resourceId, chapterSectionId, turnawayTypeId, ", _r2UtilitiesSettings.R2ReportsDatabaseName).Append("    ipAddressOctetA, ipAddressOctetB, ipAddressOctetC, ipAddressOctetD, ipAddressInteger, contentTurnawayDate, contentTurnawayCount) ").Append("select cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId ")
			.Append("     , cv.turnawayTypeId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC, cv.ipAddressOctetD ")
			.Append("     , cv.ipAddressInteger, cast(cv.contentViewTimestamp as date) as hitDate, count(*) ")
			.AppendFormat("from   [{0}]..ContentView cv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.Append(GetWhereClauseDateFilter("cv.contentViewTimestamp", startOfMonth))
			.Append("  and  cv.turnawayTypeId <> 0 ")
			.Append("group by cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId ")
			.Append("     , cv.turnawayTypeId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC, cv.ipAddressOctetD ")
			.Append("     , cv.ipAddressInteger, cast(cv.contentViewTimestamp as date) ")
			.ToString();
		int rows = ExecuteAggregateInsert(sql);
		R2UtilitiesBase.Log.InfoFormat("AggregateContentViewTurnawayCount() - rows inserted: {0}", rows);
		return rows;
	}

	public int AggregatePageViewCount(DateTime startOfMonth)
	{
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewCount(startOfMonth: {0:u})", startOfMonth);
		int recordsDeleted = DeleteRangeFromTable(startOfMonth, "DailyPageViewCount", "pageViewDate");
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewCount() - recordsDeleted: {0}", recordsDeleted);
		string sql = new StringBuilder().AppendFormat("Insert Into [{0}]..DailyPageViewCount (institutionId, userId, ipAddressOctetA, ipAddressOctetB, ", _r2UtilitiesSettings.R2ReportsDatabaseName).Append("    ipAddressOctetC, ipAddressOctetD, ipAddressInteger, pageViewDate, pageViewCount) ").Append("select pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB ")
			.Append("     , pv.ipAddressOctetC, pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date) as pageViewDate, count(*) ")
			.AppendFormat("from   [{0}]..PageView pv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.Append(GetWhereClauseDateFilter("pv.pageViewTimestamp", startOfMonth))
			.Append("group by pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.Append("        , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date) ")
			.ToString();
		int rows = ExecuteAggregateInsert(sql);
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewCount() - rows inserted: {0}", rows);
		return rows;
	}

	public int AggregatePageViewSessionCount(DateTime startOfMonth)
	{
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewSessionCount(startOfMonth: {0:u})", startOfMonth);
		int recordsDeleted = DeleteRangeFromTable(startOfMonth, "DailySessionCount", "sessionDate");
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewSessionCount() - recordsDeleted: {0}", recordsDeleted);
		string sql = new StringBuilder().AppendFormat("Insert Into [{0}]..DailySessionCount (institutionId, userId, ipAddressOctetA, ipAddressOctetB, ", _r2UtilitiesSettings.R2ReportsDatabaseName).Append("    ipAddressOctetC, ipAddressOctetD, ipAddressInteger, sessionDate, sessionCount) ").Append("select pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.Append("     , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date), count(distinct pv.sessionId) ")
			.AppendFormat("from   [{0}]..PageView pv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.Append(GetWhereClauseDateFilter("pv.pageViewTimestamp", startOfMonth))
			.Append("group by pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.Append("     , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date) ")
			.ToString();
		int rows = ExecuteAggregateInsert(sql);
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewSessionCount() - rows inserted: {0}", rows);
		return rows;
	}

	public int AggregatePageViewResourceSessionCount(DateTime startOfMonth)
	{
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewResourceSessionCount(startOfMonth: {0:u})", startOfMonth);
		int recordsDeleted = DeleteRangeFromTable(startOfMonth, "DailyResourceSessionCount", "sessionDate");
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewResourceSessionCount() - recordsDeleted: {0}", recordsDeleted);
		string sql = new StringBuilder().AppendFormat("Insert Into [{0}]..DailyResourceSessionCount (institutionId, userId, ipAddressOctetA, ipAddressOctetB, ", _r2UtilitiesSettings.R2ReportsDatabaseName).Append("    ipAddressOctetC, ipAddressOctetD, ipAddressInteger, sessionDate, sessionCount, resourceId) ").Append("select pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.Append("     , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date), count(distinct pv.sessionId), cv.resourceId ")
			.AppendFormat("from   [{0}]..PageView pv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendFormat("join [{0}]..ContentView cv on pv.requestId = cv.requestId ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.Append(GetWhereClauseDateFilter("pv.pageViewTimestamp", startOfMonth))
			.Append("group by pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.Append("     , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date), cv.resourceId ")
			.ToString();
		int rows = ExecuteAggregateInsert(sql);
		R2UtilitiesBase.Log.InfoFormat("AggregatePageViewResourceSessionCount() - rows inserted: {0}", rows);
		return rows;
	}

	public int AggregateSearchCount(DateTime startOfMonth)
	{
		R2UtilitiesBase.Log.InfoFormat("AggregateSearchCount(startOfMonth: {0:u})", startOfMonth);
		int recordsDeleted = DeleteRangeFromTable(startOfMonth, "DailySearchCount", "searchDate");
		R2UtilitiesBase.Log.InfoFormat("AggregateSearchCount() - recordsDeleted: {0}", recordsDeleted);
		string sql = new StringBuilder().AppendFormat("Insert Into [{0}]..DailySearchCount(institutionId, userId, searchTypeId, isArchive, isExternal, ipAddressOctetA, ", _r2UtilitiesSettings.R2ReportsDatabaseName).Append("    ipAddressOctetB, ipAddressOctetC, ipAddressOctetD, ipAddressInteger, searchDate, searchCount) ").Append("select s.institutionId, s.userId, s.searchTypeId, s.isArchive, s.isExternal, s.ipAddressOctetA ")
			.Append("     , s.ipAddressOctetB, s.ipAddressOctetC, s.ipAddressOctetD, s.ipAddressInteger, cast(s.searchTimestamp as date) as searchDate, count(*) ")
			.AppendFormat("from   [{0}]..Search s ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.Append(GetWhereClauseDateFilter("s.searchTimestamp", startOfMonth))
			.Append("group by s.institutionId, s.userId, s.searchTypeId, s.isArchive, s.isExternal, s.ipAddressOctetA ")
			.Append("       , s.ipAddressOctetB, s.ipAddressOctetC, s.ipAddressOctetD, s.ipAddressInteger, cast(s.searchTimestamp as date) ")
			.ToString();
		int rows = ExecuteAggregateInsert(sql);
		R2UtilitiesBase.Log.InfoFormat("AggregateSearchCount() - rows inserted: {0}", rows);
		return rows;
	}

	public int DeleteRangeFromTable(DateTime startOfMonth, string tableName, string databaseTimeStampName)
	{
		R2UtilitiesBase.Log.InfoFormat("DeleteRangeFromTable(startOfMonth: {0:u}, tableName: {1}, databaseTimeStampName: {2})", startOfMonth, tableName, databaseTimeStampName);
		string sql = new StringBuilder().AppendFormat("delete from [{0}]..{1} ", _r2UtilitiesSettings.R2ReportsDatabaseName, tableName).AppendFormat("where  {0} >= '{1:MM/dd/yyyy} 00:00:00' and {0} < '{2:MM/dd/yyyy} 00:00:00';", databaseTimeStampName, startOfMonth, startOfMonth.AddMonths(1)).ToString();
		R2UtilitiesBase.Log.DebugFormat("sql: {0}", sql);
		SqlConnection cnn = null;
		SqlCommand command = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			int rows = command.ExecuteNonQuery();
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("command time: {0}ms, rows: {1}", stopWatch.ElapsedMilliseconds, rows);
			return rows;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	public bool BulkExportR2ReportsTables(DateTime monthToExport)
	{
		bool wasSuccessful = BulkExportTable(monthToExport, "PageView", "pageViewId", "pageViewTimestamp") && BulkExportTable(monthToExport, "ContentView", "contentTurnawayId", "contentViewTimestamp") && BulkExportTable(monthToExport, "Search", "searchId", "searchTimestamp");
		R2UtilitiesBase.Log.DebugFormat("BulkExportR2ReportsTables() - wasSuccessful: {0}", wasSuccessful);
		return wasSuccessful;
	}

	private bool BulkExportTable(DateTime monthToExport, string tableName, string tableOrderByName, string timestampFieldName)
	{
		R2UtilitiesBase.Log.InfoFormat("BulkExportTable(startOfMonth: {0:u}, tableName: {1}, tableOrderByName: {2})", monthToExport, tableName, tableOrderByName);
		string selectCommand = "select * from [" + _r2UtilitiesSettings.R2ReportsDatabaseName + "].." + tableName + " " + GetWhereClauseDateFilter(timestampFieldName, monthToExport) + " order by " + tableOrderByName + " desc";
		string outputFile = Path.Combine(_r2UtilitiesSettings.AggregateDailyCountFolder, $"{monthToExport:yyyy-MM}-{tableName}.dat");
		string resultFile = Path.Combine(_r2UtilitiesSettings.AggregateDailyCountFolder, $"{monthToExport:yyyy-MM}-{tableName}.log");
		R2UtilitiesBase.Log.DebugFormat("selectCommand: {0}", selectCommand);
		R2UtilitiesBase.Log.DebugFormat("outputFile: {0}", outputFile);
		R2UtilitiesBase.Log.DebugFormat("resultFile: {0}", resultFile);
		SetBcpCredientials();
		bool wasSuccessful = Export(selectCommand, outputFile, resultFile);
		R2UtilitiesBase.Log.DebugFormat("wasSuccessful: {0}", wasSuccessful);
		if (wasSuccessful)
		{
			string[] fileArray = new string[2] { outputFile, resultFile };
			string fileName = $"{monthToExport:yyyy-MM}-{tableName}__{DateTime.Now:yyyyMMddhhmmss}.zip";
			string tempZipFileLocation = Path.Combine(_r2UtilitiesSettings.AggregateDailyCountFolder, fileName);
			string zipFileLocation = Path.Combine(_r2UtilitiesSettings.AggregateDailyCountFolderZipLocation, fileName);
			wasSuccessful = ZipFiles(fileArray, tempZipFileLocation);
			if (wasSuccessful)
			{
				File.Copy(tempZipFileLocation, zipFileLocation);
				File.Delete(tempZipFileLocation);
			}
		}
		return wasSuccessful;
	}

	public bool RebuildIndexTables()
	{
		RebuildAndReorgIndexes(_r2UtilitiesSettings.R2ReportsDatabaseName);
		return true;
	}

	public void RebuildAndReorgIndexes(string database)
	{
		string sql = new StringBuilder().Append("declare @script as nvarchar(MAX) ").AppendLine().Append("declare @objectId as int ")
			.AppendLine()
			.Append("declare @tableName as varchar(255) ")
			.AppendLine()
			.Append("declare @indexId as int ")
			.AppendLine()
			.Append("declare @indexName as varchar(255) ")
			.AppendLine()
			.Append("declare @fragPercentage as decimal ")
			.AppendLine()
			.Append("declare @pageCount as int ")
			.AppendLine()
			.Append("declare @fillFactor as int ")
			.AppendLine()
			.Append("declare index_cursor cursor for ")
			.AppendLine()
			.Append("select ddips.object_id, o.[name], ddips.index_id, i.[name], ddips.avg_fragmentation_in_percent, ddips.page_count, i.fill_factor ")
			.AppendLine()
			.Append("from sys.dm_db_index_physical_stats(DB_ID(@DbName), NULL, NULL, NULL , NULL) ddips ")
			.AppendLine()
			.Append("join sys.objects o on o.object_id = ddips.object_id ")
			.AppendLine()
			.Append("join sys.indexes i on i.index_id = ddips.index_id and i.[object_id] = o.[object_id] and i.[name] is not null ")
			.AppendLine()
			.Append("where ddips.avg_fragmentation_in_percent > @MinFragmentation ")
			.AppendLine()
			.Append("order by ddips.avg_fragmentation_in_percent desc ")
			.AppendLine()
			.Append("open index_cursor ")
			.AppendLine()
			.Append("fetch next from index_cursor into @objectId, @tableName, @indexId, @indexName, @fragPercentage, @pageCount, @fillFactor ")
			.AppendLine()
			.Append("set @script = ''; ")
			.AppendLine()
			.Append("while @@FETCH_STATUS = 0 ")
			.AppendLine()
			.Append("begin ")
			.AppendLine()
			.Append("print 'table: ' + @tableName + ', index: ' + @indexName + ', frag percent:' + cast(@fragPercentage as varchar(100)) ")
			.AppendLine()
			.Append("if ((@fragPercentage >= @RebuildThreshold) or (@fillFactor <> @RebuildFillFactor)) ")
			.AppendLine()
			.Append("set @script = 'alter index [' + @indexName + '] on dbo.[' + @tableName + '] REBUILD PARTITION = ALL WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = ' + cast(@RebuildFillFactor as varchar(20)) + ')'; ")
			.AppendLine()
			.Append("else ")
			.AppendLine()
			.Append("set @script = 'alter index [' + @indexName + '] on dbo.[' + @tableName + '] REORGANIZE WITH ( LOB_COMPACTION = ON )'; ")
			.AppendLine()
			.Append("print @script; ")
			.AppendLine()
			.Append("EXECUTE sp_executesql @script ")
			.AppendLine()
			.Append("fetch next from index_cursor into @objectId, @tableName, @indexId, @indexName, @fragPercentage, @pageCount, @fillFactor ")
			.AppendLine()
			.Append("end ")
			.AppendLine()
			.Append("close index_cursor ")
			.AppendLine()
			.Append("deallocate index_cursor ")
			.AppendLine()
			.Append("EXEC [dbo].[sp_updstats] ")
			.ToString();
		R2UtilitiesBase.Log.DebugFormat("sql: {0}", sql);
		SqlConnection cnn = null;
		SqlCommand command = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			DbCommandHelper.SetCommandParmater(command, "DbName", database);
			DbCommandHelper.SetCommandParmater(command, "MinFragmentation ", 5);
			DbCommandHelper.SetCommandParmater(command, "RebuildThreshold ", 30);
			DbCommandHelper.SetCommandParmater(command, "RebuildFillFactor ", 95);
			command.ExecuteNonQuery();
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("reindex time: {0}ms", stopWatch.ElapsedMilliseconds);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	private static bool Export(string selectCommand, string outputFileName, string resultFileName)
	{
		bool success = true;
		string bcpCommand = new StringBuilder().Append("\"").Append(selectCommand).Append("\" queryout ")
			.Append(outputFileName)
			.Append(" -c -S")
			.Append(DatabaseServerName)
			.Append(" -o")
			.Append(resultFileName)
			.Append(" -U")
			.Append(DatabaseUserName)
			.Append(" -P")
			.Append(DatabasePassword)
			.ToString();
		R2UtilitiesBase.Log.Debug(bcpCommand);
		R2UtilitiesBase.Log.InfoFormat("BCP Started - resultFileName: {0}", resultFileName);
		try
		{
			Process bcpProcess = Process.Start("BCP", bcpCommand);
			while (bcpProcess != null && !bcpProcess.HasExited)
			{
				bcpProcess.Refresh();
				Thread.Sleep(3000);
			}
			if (bcpProcess == null)
			{
				R2UtilitiesBase.Log.Error("    bcpProcess is null!");
				return false;
			}
			if (bcpProcess.ExitCode != 0)
			{
				R2UtilitiesBase.Log.ErrorFormat("    Error Code: {0} \\r\\n BCP Export aborted", bcpProcess.ExitCode);
				success = false;
			}
			else
			{
				R2UtilitiesBase.Log.Info("BCP Completed");
			}
			bcpProcess.Close();
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			success = false;
		}
		return success;
	}

	private static bool ZipFiles(string[] fileNames, string zipFileName)
	{
		R2UtilitiesBase.Log.InfoFormat("ZipFiles() - zipFileName: {0}", zipFileName);
		try
		{
			ZipHelper.CompressFiles(fileNames, zipFileName);
			foreach (string file in fileNames)
			{
				File.Delete(file);
			}
			return true;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			return false;
		}
	}

	private void SetBcpCredientials()
	{
		R2UtilitiesBase.Log.Debug("SetBcpCredientials() >>>");
		if (string.IsNullOrWhiteSpace(DatabaseServerName) || string.IsNullOrWhiteSpace(DatabaseUserName) || string.IsNullOrWhiteSpace(DatabasePassword))
		{
			string[] connectionArray = base.ConnectionString.Split(';');
			string workingVariable = connectionArray.FirstOrDefault((string x) => x.ToLower().Contains("server"));
			if (workingVariable != null)
			{
				DatabaseServerName = workingVariable.Split('=')[1];
			}
			workingVariable = connectionArray.FirstOrDefault((string x) => x.ToLower().Contains("user id"));
			if (workingVariable != null)
			{
				DatabaseUserName = workingVariable.Split('=')[1];
			}
			workingVariable = connectionArray.FirstOrDefault((string x) => x.ToLower().Contains("password"));
			if (workingVariable != null)
			{
				DatabasePassword = workingVariable.Split('=')[1];
			}
		}
		R2UtilitiesBase.Log.Debug("SetBcpCredientials() <<<");
	}
}
