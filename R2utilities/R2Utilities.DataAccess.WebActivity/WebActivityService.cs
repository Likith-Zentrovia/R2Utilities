using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.DataAccess.WebActivity;

public class WebActivityService : R2ReportsBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public WebActivityService(IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public WebActivityReport GetWebActivityReport(DateTime startDate)
	{
		WebActivityReport webActivityReport = new WebActivityReport
		{
			PageRequests = GetPageRequestCount(startDate),
			AveragePageRequestTime = GetAveragePageRequestTime(startDate),
			MedianPageRequestTime = GetMedianPageRequestTime(startDate),
			TopInstitutionPageRequests = GetTopInstitutionsPageRequests(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay),
			TopInstitutionResourceRequests = GetTopInstitutionsResourceRequests(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay),
			TopResources = GetTopResources(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay),
			TopInstitutionIpRanges = GetTopInstitutionIpAddresses(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay),
			TopIpRanges = GetTopIpAddresses(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay),
			AllContentRequests = GetAllContentRequests(startDate),
			TocRequests = GetTocOnlyRequests(startDate),
			ContentRequests = GetSuccessfulContentOnlyRequests(startDate),
			TurnawayConcurrency = GetTurnawayConcurrency(startDate),
			TurnawayAccess = GetTurnawayAccess(startDate),
			PrintRequests = GetPrintRequests(startDate),
			EmailRequests = GetEmailRequests(startDate),
			NumberOfRequestTimes = GetPageViewRunTimeCounts(startDate),
			SessionCount = GetSessionCount(startDate),
			TopInstitutionResourcePrintRequests = GetPrintRequests(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay),
			TopInstitutionResourceEmailRequests = GetEmailRequests(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay),
			TopInstitutionSessionRequests = GetSessionRequests(startDate, _r2UtilitiesSettings.MaxActivityReportInstitutionDisplay)
		};
		SetSearchData(webActivityReport, startDate);
		return webActivityReport;
	}

	public int GetPageRequestCount(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(pv.pageViewId) ").AppendFormat("     from PageView pv ").AppendFormat("     where pageViewTimestamp >= '{0}'", startDate)
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetAveragePageRequestTime(DateTime startDate)
	{
		string sql = new StringBuilder().Append("select avg(pageViewRunTime) ").AppendFormat("from PageView ").AppendFormat("where pageViewTimestamp >= '{0}'", startDate)
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetMedianPageRequestTime(DateTime startDate)
	{
		string sql = new StringBuilder().Append("SELECT ((SELECT MAX(pageViewRunTime) FROM ").AppendFormat("(SELECT TOP 50 PERCENT pageViewRunTime FROM [PageView] where pageViewTimestamp >= '{0}'  ORDER BY pageViewRunTime) AS BottomHalf) ", startDate).Append("+ (SELECT MIN(pageViewRunTime) FROM ")
			.AppendFormat("(SELECT TOP 50 PERCENT pageViewRunTime FROM [PageView] where pageViewTimestamp >= '{0}'  ORDER BY pageViewRunTime DESC) AS TopHalf) ", startDate)
			.Append(") / 2 AS Median")
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetAllContentRequests(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(cv.contentTurnawayId)").AppendFormat("     from ContentView cv").AppendFormat("     where cast(cv.contentViewTimestamp as date) = '{0}'", startDate.Date)
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetTocOnlyRequests(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(cv.contentTurnawayId)").AppendFormat("     from ContentView cv").AppendFormat("     where cast(cv.contentViewTimestamp as date) = '{0}'", startDate.Date)
			.Append("           and cv.chapterSectionId is null")
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetSuccessfulContentOnlyRequests(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(cv.contentTurnawayId)").AppendFormat("     from ContentView cv").AppendFormat("     where cast(cv.contentViewTimestamp as date) = '{0}'", startDate.Date)
			.Append("           and cv.chapterSectionId is not null and cv.turnawayTypeId = 0")
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetTurnawayConcurrency(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(cv.contentTurnawayId)").AppendFormat("     from ContentView cv").AppendFormat("     where cast(cv.contentViewTimestamp as date) = '{0}'", startDate.Date)
			.Append("           and cv.turnawayTypeId = 20")
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetTurnawayAccess(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(cv.contentTurnawayId)").AppendFormat("     from ContentView cv").AppendFormat("     where cast(cv.contentViewTimestamp as date) = '{0}'", startDate.Date)
			.Append("           and cv.turnawayTypeId = 21")
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetPrintRequests(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(cv.contentTurnawayId)").AppendFormat("     from ContentView cv").AppendFormat("     where cast(cv.contentViewTimestamp as date) = '{0}'", startDate.Date)
			.Append("           and cv.actionTypeId = 16")
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetSessionCount(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(distinct pv.sessionId)").AppendFormat("     from PageView pv").AppendFormat("     where cast(pv.pageViewTimestamp as date) = '{0}'", startDate.Date)
			.ToString();
		return ProcessQueryValue(sql);
	}

	public int GetEmailRequests(DateTime startDate)
	{
		string sql = new StringBuilder().Append("           select count(cv.contentTurnawayId)").AppendFormat("     from ContentView cv").AppendFormat("     where cast(cv.contentViewTimestamp as date) = '{0}'", startDate.Date)
			.Append("           and cv.actionTypeId = 17")
			.ToString();
		return ProcessQueryValue(sql);
	}

	public NumberOfRequestTimes GetPageViewRunTimeCounts(DateTime startDate)
	{
		string sql = new StringBuilder().Append("select count(pageViewId) as total ").Append(", sum(case when pageViewRunTime > 1999 and pageViewRunTime < 4999 then 1 else 0 end) as 'Two_Five' ").Append(", sum(case when pageViewRunTime > 4999 and pageViewRunTime < 9999 then 1 else 0 end) as 'Five_Ten' ")
			.Append(", sum(case when pageViewRunTime > 9999 then 1 else 0 end) as 'Over_Ten' ")
			.AppendFormat("from [PageView] ")
			.AppendFormat("where pageViewTimestamp > '{0}' ", startDate)
			.ToString();
		return ProcessQueryValues(sql);
	}

	public IEnumerable<TopInstitutionResource> GetPrintRequests(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat("     select top {0} count(cv.contentTurnawayId) as 'Count', i.iInstitutionId ", max).Append("           , i.vchInstitutionName , i.vchInstitutionAcctNum ").Append("           , r.vchResourceISBN, r.vchResourceTitle, r.iResourceId")
			.Append("     from ContentView cv ")
			.AppendFormat("           join {0}..tInstitution i on cv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.AppendFormat("           join {0}..tResource r on cv.resourceId = r.iResourceId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("           where cast(cv.contentViewTimestamp as date) = @StartDate ")
			.Append("           and cv.actionTypeId = 16 ")
			.Append("           group by i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum, r.vchResourceISBN, r.vchResourceTitle, r.iResourceId ")
			.Append("           order by 1 desc ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopInstitutionResource> topInstitutionResources = new List<TopInstitutionResource>();
			while (reader.Read())
			{
				TopInstitutionResource topInstitutionResource = new TopInstitutionResource
				{
					Count = GetInt32Value(reader, "Count", 0),
					InstitutionId = GetInt32Value(reader, "iInstitutionId", 0),
					AccountName = GetStringValue(reader, "vchInstitutionName"),
					AccountNumber = GetStringValue(reader, "vchInstitutionAcctNum"),
					Isbn = GetStringValue(reader, "vchResourceISBN"),
					ResourceId = GetInt32Value(reader, "iResourceId", 0),
					Title = GetStringValue(reader, "vchResourceTitle")
				};
				topInstitutionResources.Add(topInstitutionResource);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topInstitutionResources.Count);
			return topInstitutionResources;
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

	public IEnumerable<TopInstitutionResource> GetEmailRequests(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat("     select top {0} count(cv.contentTurnawayId) as 'Count', i.iInstitutionId ", max).Append("           , i.vchInstitutionName , i.vchInstitutionAcctNum ").Append("           , r.vchResourceISBN, r.vchResourceTitle, r.iResourceId")
			.Append("     from ContentView cv ")
			.AppendFormat("           join {0}..tInstitution i on cv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.AppendFormat("           join {0}..tResource r on cv.resourceId = r.iResourceId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("           where cast(cv.contentViewTimestamp as date) = @StartDate ")
			.Append("           and cv.actionTypeId = 17 ")
			.Append("           group by i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum, r.vchResourceISBN, r.vchResourceTitle, r.iResourceId ")
			.Append("           order by 1 desc ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopInstitutionResource> topInstitutionResources = new List<TopInstitutionResource>();
			while (reader.Read())
			{
				TopInstitutionResource topInstitutionResource = new TopInstitutionResource
				{
					Count = GetInt32Value(reader, "Count", 0),
					InstitutionId = GetInt32Value(reader, "iInstitutionId", 0),
					AccountName = GetStringValue(reader, "vchInstitutionName"),
					AccountNumber = GetStringValue(reader, "vchInstitutionAcctNum"),
					Isbn = GetStringValue(reader, "vchResourceISBN"),
					ResourceId = GetInt32Value(reader, "iResourceId", 0),
					Title = GetStringValue(reader, "vchResourceTitle")
				};
				topInstitutionResources.Add(topInstitutionResource);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topInstitutionResources.Count);
			return topInstitutionResources;
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

	public WebActivityReport SetSearchData(WebActivityReport webActivityReport, DateTime startDate)
	{
		string sql = new StringBuilder().Append(" select count(s.searchId) as Total, AVG(pv.pageViewRunTime) as Average, max(pv.pageViewRunTime) as Max ").Append(" from Search s ").Append(" join PageView pv on s.requestId = pv.requestId ")
			.Append(" where cast(s.searchTimestamp as date) = @StartDate ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			while (reader.Read())
			{
				int total = GetInt32Value(reader, "Total", 0);
				int average = GetInt32Value(reader, "Average", 0);
				int max = GetInt32Value(reader, "Max", 0);
				webActivityReport.SearchCount = total;
				webActivityReport.SearchTimeAverage = average;
				webActivityReport.SearchTimeMax = max;
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.Debug($"query time: {stopWatch.ElapsedMilliseconds}ms, for SetSearchData");
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
		return webActivityReport;
	}

	public IEnumerable<TopInstitution> GetSessionRequests(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat("     select top {0} count(distinct pv.sessionId) as Count, pv.institutionId ", max).Append("           , i.vchInstitutionName , i.vchInstitutionAcctNum ").Append("     from PageView pv ")
			.AppendFormat("           join {0}..tInstitution i on pv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("           where cast(pv.pageViewTimestamp as date) = @StartDate ")
			.Append("           group by pv.institutionId, i.vchInstitutionName, i.vchInstitutionAcctNum ")
			.Append("           order by 1 desc ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopInstitution> topInstitutions = new List<TopInstitution>();
			while (reader.Read())
			{
				TopInstitution topInstitution = new TopInstitution
				{
					Count = GetInt32Value(reader, "Count", 0),
					InstitutionId = GetInt32Value(reader, "institutionId", 0),
					AccountName = GetStringValue(reader, "vchInstitutionName"),
					AccountNumber = GetStringValue(reader, "vchInstitutionAcctNum")
				};
				topInstitutions.Add(topInstitution);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topInstitutions.Count);
			return topInstitutions;
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

	public IEnumerable<TopInstitution> GetTopInstitutionsPageRequests(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat("     select top {0} i.iInstitutionId as 'InstitutionId', i.vchInstitutionName as 'AccountName' ", max).Append("           , i.vchInstitutionAcctNum as 'AccountNumber', count(pv.pageViewId) as 'Count' ").Append("     from   PageView pv ")
			.AppendFormat("           join {0}..tInstitution i on pv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("           where cast(pv.pageViewTimestamp as date) = @StartDate ")
			.Append("           group by i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum ")
			.Append("           order by count(pv.pageViewId) desc ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopInstitution> topInstitutions = new List<TopInstitution>();
			while (reader.Read())
			{
				TopInstitution topInstitution = new TopInstitution
				{
					Count = GetInt32Value(reader, "Count", 0),
					InstitutionId = GetInt32Value(reader, "InstitutionId", 0),
					AccountName = GetStringValue(reader, "AccountName"),
					AccountNumber = GetStringValue(reader, "AccountNumber")
				};
				topInstitutions.Add(topInstitution);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topInstitutions.Count);
			return topInstitutions;
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

	public IEnumerable<TopInstitutionResource> GetTopInstitutionsResourceRequests(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat("     select top {0} i.iInstitutionId as 'InstitutionId', i.vchInstitutionName as 'AccountName', i.vchInstitutionAcctNum as 'AccountNumber' ", max).Append("           , r.iResourceId as 'ResourceId', r.vchResourceTitle as 'Title', r.vchResourceISBN as 'Isbn', count(cv.contentTurnawayId) as 'Count' ").Append("     from  ContentView cv ")
			.AppendFormat("           join {0}..tInstitution i on cv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.AppendFormat("           join {0}..tResource r on cv.resourceId = r.iResourceId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("           where cast(cv.contentViewTimestamp as date) = @StartDate and cv.turnawayTypeId = 0  ")
			.Append("           group by  i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum, r.iResourceId, r.vchResourceTitle, r.vchResourceISBN ")
			.Append("           order by count(cv.contentTurnawayId) desc ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopInstitutionResource> topInstitutionResources = new List<TopInstitutionResource>();
			while (reader.Read())
			{
				TopInstitutionResource topResource = new TopInstitutionResource
				{
					ResourceId = GetInt32Value(reader, "ResourceId", 0),
					Isbn = GetStringValue(reader, "Isbn"),
					Title = GetStringValue(reader, "Title"),
					Count = GetInt32Value(reader, "Count", 0),
					InstitutionId = GetInt32Value(reader, "InstitutionId", 0),
					AccountName = GetStringValue(reader, "AccountName"),
					AccountNumber = GetStringValue(reader, "AccountNumber")
				};
				topInstitutionResources.Add(topResource);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topInstitutionResources.Count);
			return topInstitutionResources;
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

	public IEnumerable<TopResource> GetTopResources(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat("         select top {0} r.iResourceId as 'ResourceId', r.vchResourceTitle as 'Title' ", max).Append("               , r.vchResourceISBN as 'Isbn', count(cv.contentTurnawayId) as 'Count'").Append("         from ContentView cv ")
			.AppendFormat("               join {0}..tResource r on cv.resourceId = r.iResourceId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("               where cast(cv.contentViewTimestamp as date) = @StartDate and cv.turnawayTypeId = 0")
			.Append("               group by r.iResourceId, r.vchResourceTitle, r.vchResourceISBN ")
			.Append("               order by count(cv.contentTurnawayId) desc ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopResource> topResources = new List<TopResource>();
			while (reader.Read())
			{
				TopResource topResource = new TopResource
				{
					ResourceId = GetInt32Value(reader, "ResourceId", 0),
					Isbn = GetStringValue(reader, "Isbn"),
					Title = GetStringValue(reader, "Title"),
					Count = GetInt32Value(reader, "Count", 0)
				};
				topResources.Add(topResource);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topResources.Count);
			return topResources;
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

	public IEnumerable<TopIpAddress> GetTopInstitutionIpAddresses(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat("select top {0} i.iInstitutionId as 'InstitutionId', i.vchInstitutionName as 'AccountName'", max).Append("        , i.vchInstitutionAcctNum as 'AccountNumber', pv.ipAddressOctetA as 'OctetA', pv.ipAddressOctetB as 'OctetB' ").Append("        , pv.ipAddressOctetC as 'OctetC', pv.ipAddressOctetD as 'OctetD', count(pv.pageViewId) as 'Count', pv.countryCode as 'CountryCode' ")
			.Append("from  PageView pv  ")
			.AppendFormat("join {0}..tInstitution i on pv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("where cast(pv.pageViewTimestamp as date) = @StartDate ")
			.Append("group by i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum, pv.ipAddressOctetA, pv.countryCode ")
			.Append("         , pv.ipAddressOctetB, pv.ipAddressOctetC, pv.ipAddressOctetD ")
			.Append("order by count(pv.pageViewId) desc ")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopIpAddress> topIpAddresses = new List<TopIpAddress>();
			while (reader.Read())
			{
				TopIpAddress ipAddress = new TopIpAddress
				{
					InstitutionId = GetInt32Value(reader, "InstitutionId", 0),
					AccountName = GetStringValue(reader, "AccountName"),
					AccountNumber = GetStringValue(reader, "AccountNumber"),
					OctetA = GetByteValue(reader, "OctetA", 0),
					OctetB = GetByteValue(reader, "OctetB", 0),
					OctetC = GetByteValue(reader, "OctetC", 0),
					OctetD = GetByteValue(reader, "OctetD", 0),
					Count = GetInt32Value(reader, "Count", 0),
					CountryCode = GetStringValue(reader, "CountryCode")
				};
				topIpAddresses.Add(ipAddress);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topIpAddresses.Count);
			return topIpAddresses;
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

	public IEnumerable<TopIpAddress> GetTopIpAddresses(DateTime startDate, int max)
	{
		string sql = new StringBuilder().AppendFormat(" select top {0} i.iInstitutionId as 'InstitutionId', i.vchInstitutionName as 'AccountName'", max).Append("            , i.vchInstitutionAcctNum as 'AccountNumber', pv.ipAddressOctetA as 'OctetA', pv.ipAddressOctetB as 'OctetB'").Append("            , pv.ipAddressOctetC as 'OctetC', pv.ipAddressOctetD as 'OctetD', count(pv.pageViewId) as 'Count', pv.countryCode as 'CountryCode'")
			.Append("           from  PageView pv ")
			.AppendFormat("     left outer join {0}..tInstitution i on pv.institutionId = i.iInstitutionId ", _r2UtilitiesSettings.R2DatabaseName)
			.Append("       where cast(pv.pageViewTimestamp as date) = @StartDate ")
			.Append("       group by i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum")
			.Append("                , pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC, pv.ipAddressOctetD, pv.countryCode ")
			.Append("       order by count(pv.pageViewId) desc")
			.ToString();
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			DbCommandHelper.SetCommandParmater(command, "StartDate", startDate.Date);
			reader = command.ExecuteReader();
			List<TopIpAddress> topIpAddresses = new List<TopIpAddress>();
			while (reader.Read())
			{
				TopIpAddress ipAddress = new TopIpAddress
				{
					InstitutionId = GetInt32Value(reader, "InstitutionId", 0),
					AccountName = GetStringValue(reader, "AccountName"),
					AccountNumber = GetStringValue(reader, "AccountNumber"),
					OctetA = GetByteValue(reader, "OctetA", 0),
					OctetB = GetByteValue(reader, "OctetB", 0),
					OctetC = GetByteValue(reader, "OctetC", 0),
					OctetD = GetByteValue(reader, "OctetD", 0),
					Count = GetInt32Value(reader, "Count", 0),
					CountryCode = GetStringValue(reader, "CountryCode")
				};
				topIpAddresses.Add(ipAddress);
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: {1}", stopWatch.ElapsedMilliseconds, topIpAddresses.Count);
			return topIpAddresses;
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

	public int ProcessQueryValue(string sql)
	{
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			reader = command.ExecuteReader();
			int value = 0;
			while (reader.Read())
			{
				value = (int)reader[0];
			}
			stopWatch.Stop();
			return value;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.WarnFormat(ex.Message);
			return 0;
		}
		finally
		{
			DisposeConnections(cnn, command, reader);
		}
	}

	public NumberOfRequestTimes ProcessQueryValues(string sql)
	{
		SqlConnection cnn = null;
		SqlCommand command = null;
		SqlDataReader reader = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = sql;
			command.CommandTimeout = 120;
			reader = command.ExecuteReader();
			NumberOfRequestTimes numberOfRequestTimes = null;
			while (reader.Read())
			{
				numberOfRequestTimes = new NumberOfRequestTimes
				{
					Total = GetInt32Value(reader, "total", 0),
					TwoToFive = GetInt32Value(reader, "Two_Five", 0),
					FiveToTen = GetInt32Value(reader, "Five_Ten", 0),
					MoreThanTen = GetInt32Value(reader, "Over_Ten", 0)
				};
			}
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("query time: {0}ms, count: 1", stopWatch.ElapsedMilliseconds);
			return numberOfRequestTimes;
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
}
