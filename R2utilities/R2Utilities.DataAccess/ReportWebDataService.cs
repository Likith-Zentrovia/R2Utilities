using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.DataAccess;

public class ReportWebDataService : R2UtilitiesBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public ReportWebDataService(IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public bool AlterDailyContentTurnawayCount(DateTime date)
	{
		StringBuilder sql = new StringBuilder().Append("ALTER VIEW [dbo].[vDailyContentTurnawayCount] AS ").AppendLine().Append("    select dctc.dailyContentTurnawayCountId, dctc.institutionId, dctc.userId, dctc.resourceId, dctc.chapterSectionId ")
			.AppendLine()
			.Append("         , dctc.turnawayTypeId, dctc.ipAddressOctetA, dctc.ipAddressOctetB, dctc.ipAddressOctetC, dctc.ipAddressOctetD ")
			.AppendLine()
			.Append("         , dctc.ipAddressInteger, dctc.contentTurnawayDate, dctc.contentTurnawayCount ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.DailyContentTurnawayCount dctc ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    union ")
			.AppendLine()
			.Append("    select 0, cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId ")
			.AppendLine()
			.Append("         , cv.turnawayTypeId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC, cv.ipAddressOctetD ")
			.AppendLine()
			.Append("         , cv.ipAddressInteger, cast(cv.contentViewTimestamp as date) as hitDate, count(*) ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.ContentView cv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    where  turnawayTypeId <> 0 ")
			.AppendLine()
			.AppendFormat("      and  contentViewTimestamp > '{0:MM/dd/yyyy} 00:00:00' ", date)
			.AppendLine()
			.Append("    group by cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId ")
			.AppendLine()
			.Append("           , cv.turnawayTypeId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC, cv.ipAddressOctetD ")
			.AppendLine()
			.Append("           , cv.ipAddressInteger, cast(cv.contentViewTimestamp as date) ")
			.AppendLine();
		return AlterDailyCountView(sql.ToString());
	}

	public bool AlterDailyContentViewCount(DateTime date)
	{
		StringBuilder sql = new StringBuilder().Append("ALTER VIEW [dbo].[vDailyContentViewCount] AS ").AppendLine().Append("    select dcvc.dailyContentViewCountId, dcvc.institutionId, dcvc.userId, dcvc.resourceId, dcvc.chapterSectionId, dcvc.ipAddressOctetA ")
			.AppendLine()
			.Append("         , dcvc.ipAddressOctetB, dcvc.ipAddressOctetC, dcvc.ipAddressOctetD, dcvc.ipAddressInteger, dcvc.contentViewDate ")
			.AppendLine()
			.Append("         , dcvc.contentViewCount, dcvc.actionTypeId, dcvc.foundFromSearch, dcvc.licenseType, dcvc.resourceStatusId, dcvc.uniqueContentViewCount ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.DailyContentViewCount dcvc ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    union ")
			.AppendLine()
			.Append("    select 0, cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC, cv.ipAddressOctetD, cv.ipAddressInteger ")
			.AppendLine()
			.Append("         , cast(cv.contentViewTimestamp as date) as hitDate, count(*), cv.actionTypeId, cv.foundFromSearch, cv.licenseType, cv.resourceStatusId, count(distinct pv.sessionId) ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.ContentView cv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.AppendFormat("    left join [{0}].dbo.PageView pv on pv.requestId = cv.requestId", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    where  turnawayTypeId = 0 ")
			.AppendLine()
			.AppendFormat("      and  contentViewTimestamp > '{0:MM/dd/yyyy} 00:00:00' ", date)
			.AppendLine()
			.Append("    group by cv.institutionId, cv.userId, cv.resourceId, cv.chapterSectionId, cv.ipAddressOctetA, cv.ipAddressOctetB, cv.ipAddressOctetC, cv.ipAddressOctetD, cv.ipAddressInteger ")
			.AppendLine()
			.Append("           , cast(cv.contentViewTimestamp as date), cv.actionTypeId, cv.foundFromSearch, cv.licenseType, cv.resourceStatusId ")
			.AppendLine();
		return AlterDailyCountView(sql.ToString());
	}

	public bool AlterDailyPageViewCount(DateTime date)
	{
		StringBuilder sql = new StringBuilder().Append("ALTER VIEW [dbo].[vDailyPageViewCount] AS ").AppendLine().Append("    select dpvc.dailyPageViewCountId, dpvc.institutionId, dpvc.userId, dpvc.ipAddressOctetA, dpvc.ipAddressOctetB ")
			.AppendLine()
			.Append("         , dpvc.ipAddressOctetC, dpvc.ipAddressOctetD, dpvc.ipAddressInteger, dpvc.pageViewDate, dpvc.pageViewCount ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.DailyPageViewCount dpvc", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    union ")
			.AppendLine()
			.Append("    select 0, pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB ")
			.AppendLine()
			.Append("         , pv.ipAddressOctetC, pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date) as pageViewDate, count(*) ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.PageView pv", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.AppendFormat("    where  pageViewTimestamp > '{0:MM/dd/yyyy} 00:00:00' ", date)
			.AppendLine()
			.Append("    group by pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.AppendLine()
			.Append("           , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date)")
			.AppendLine();
		return AlterDailyCountView(sql.ToString());
	}

	public bool AlterDailySearchCount(DateTime date)
	{
		StringBuilder sql = new StringBuilder().Append("ALTER VIEW [dbo].[vDailySearchCount] AS ").AppendLine().Append("    select dsc.dailySearchCountId, dsc.institutionId, dsc.userId, dsc.searchTypeId, dsc.isArchive, dsc.isExternal, dsc.ipAddressOctetA ")
			.AppendLine()
			.Append("         , dsc.ipAddressOctetB, dsc.ipAddressOctetC, dsc.ipAddressOctetD, dsc.ipAddressInteger, dsc.searchDate, dsc.searchCount ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.DailySearchCount dsc ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    union ")
			.AppendLine()
			.Append("    select 0, s.institutionId, s.userId, s.searchTypeId, s.isArchive, s.isExternal, s.ipAddressOctetA ")
			.AppendLine()
			.Append("         , s.ipAddressOctetB, s.ipAddressOctetC, s.ipAddressOctetD, s.ipAddressInteger, cast(s.searchTimestamp as date) as searchDate, count(*) ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.Search s", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.AppendFormat("    where  s.searchTimestamp >  '{0:MM/dd/yyyy} 00:00:00' ", date)
			.AppendLine()
			.Append("    group by s.institutionId, s.userId, s.searchTypeId, s.isArchive, s.isExternal, s.ipAddressOctetA ")
			.AppendLine()
			.Append("           , s.ipAddressOctetB, s.ipAddressOctetC, s.ipAddressOctetD, s.ipAddressInteger, cast(s.searchTimestamp as date) ")
			.AppendLine();
		return AlterDailyCountView(sql.ToString());
	}

	public bool AlterDailySessionCount(DateTime date)
	{
		StringBuilder sql = new StringBuilder().Append("ALTER VIEW [dbo].[vDailySessionCount] AS ").AppendLine().Append("    select dsc.dailySessionCountId, dsc.institutionId, dsc.userId, dsc.ipAddressOctetA, dsc.ipAddressOctetB, dsc.ipAddressOctetC ")
			.AppendLine()
			.Append("         , dsc.ipAddressOctetD, dsc.ipAddressInteger, dsc.sessionDate, dsc.sessionCount ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.DailySessionCount dsc ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    union ")
			.AppendLine()
			.Append("    select 0, pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.AppendLine()
			.Append("         , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date), count(distinct pv.sessionId) ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.PageView pv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.AppendFormat("    where  pageViewTimestamp > '{0:MM/dd/yyyy} 00:00:00' ", date)
			.AppendLine()
			.Append("    group by pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.AppendLine()
			.Append("           , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date) ")
			.AppendLine();
		return AlterDailyCountView(sql.ToString());
	}

	public bool AlterDailyResourceSessionCount(DateTime date)
	{
		StringBuilder sql = new StringBuilder().Append("ALTER VIEW [dbo].[vDailyResourceSessionCount] AS ").AppendLine().Append("    select drsc.dailyResourceSessionCountId, drsc.institutionId, drsc.userId, drsc.ipAddressOctetA, drsc.ipAddressOctetB ")
			.AppendLine()
			.Append("         , drsc.ipAddressOctetC, drsc.ipAddressOctetD, drsc.ipAddressInteger, drsc.sessionDate, drsc.sessionCount, drsc.resourceId ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.DailyResourceSessionCount drsc ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.Append("    union ")
			.AppendLine()
			.Append("    select 0, pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.AppendLine()
			.Append("         , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date), count(distinct pv.sessionId), cv.resourceId ")
			.AppendLine()
			.AppendFormat("    from   [{0}].dbo.PageView pv ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.AppendFormat("    join [{0}].dbo.ContentView cv on pv.requestId = cv.requestId ", _r2UtilitiesSettings.R2ReportsDatabaseName)
			.AppendLine()
			.AppendFormat("    where  pageViewTimestamp > '{0:MM/dd/yyyy} 00:00:00' ", date)
			.AppendLine()
			.Append("    group by pv.institutionId, pv.userId, pv.ipAddressOctetA, pv.ipAddressOctetB, pv.ipAddressOctetC ")
			.AppendLine()
			.Append("           , pv.ipAddressOctetD, pv.ipAddressInteger, cast(pv.pageViewTimestamp as date), cv.resourceId ")
			.AppendLine();
		return AlterDailyCountView(sql.ToString());
	}

	private bool AlterDailyCountView(string alterSqlStatement)
	{
		R2UtilitiesBase.Log.DebugFormat("alterSqlStatement: {0}", alterSqlStatement);
		SqlConnection cnn = null;
		SqlCommand command = null;
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		try
		{
			cnn = GetConnection();
			command = cnn.CreateCommand();
			command.CommandText = alterSqlStatement;
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			int rows = command.ExecuteNonQuery();
			stopWatch.Stop();
			R2UtilitiesBase.Log.DebugFormat("AlterDailyCountView() time: {0}ms, rows: {1}", stopWatch.ElapsedMilliseconds, rows);
			return true;
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
}
