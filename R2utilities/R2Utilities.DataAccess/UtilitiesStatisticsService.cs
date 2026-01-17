using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Reports;

namespace R2Utilities.DataAccess;

public class UtilitiesStatisticsService : R2ReportsBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ReportDataService _reportDataService;

	public UtilitiesStatisticsService(IR2UtilitiesSettings r2UtilitiesSettings, ReportDataService reportDataService)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_reportDataService = reportDataService;
	}

	public bool InsertInstitutionStatistics(InstitutionStatistics institutionStatistics)
	{
		string sql = new StringBuilder().AppendFormat("INSERT INTO InstitutionMonthlyStatisticsCount ").Append("           ([institutionId] ").Append("           ,[aggregationDate] ")
			.Append("           ,[mostAccessedResourceId] ")
			.Append("           ,[mostAccessedCount] ")
			.Append("           ,[leastAccessedResourceId] ")
			.Append("           ,[leastAccessedCount] ")
			.Append("           ,[mostTurnawayConcurrentResourceId] ")
			.Append("           ,[mostTurnawayConcurrentCount] ")
			.Append("           ,[mostTurnawayAccessResourceId] ")
			.Append("           ,[mostTurnawayAccessCount] ")
			.Append("           ,[mostPopularSpecialtyName] ")
			.Append("           ,[mostPopularSpecialtyCount] ")
			.Append("           ,[leastPopularSpecialtyName] ")
			.Append("           ,[leastPopularSpecialtyCount] ")
			.Append("           ,[totalResourceCount] ")
			.Append("           ,[contentCount] ")
			.Append("           ,[tocCount] ")
			.Append("           ,[sessionCount] ")
			.Append("           ,[printCount] ")
			.Append("           ,[emailCount] ")
			.Append("           ,[turnawayConcurrencyCount] ")
			.Append("           ,[turnawayAccessCount]) ")
			.Append("            VALUES (")
			.AppendFormat("                {0},", institutionStatistics.InstitutionId)
			.AppendFormat("                  '{0}',", institutionStatistics.StartDate)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.MostAccessedResourceId)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.MostAccessedCount)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.LeastAccessedResourceId)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.LeastAccessedCount)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.MostTurnawayConcurrentResourceId)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.MostTurnawayConcurrentCount)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.MostTurnawayAccessResourceId)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.MostTurnawayAccessCount)
			.AppendFormat("                  '{0}',", institutionStatistics.Highlights.MostPopularSpecialtyName)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.MostPopularSpecialtyCount)
			.AppendFormat("                  '{0}',", institutionStatistics.Highlights.LeastPopularSpecialtyName)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.LeastPopularSpecialtyCount)
			.AppendFormat("                    {0},", institutionStatistics.Highlights.TotalResourceCount)
			.AppendFormat("                    {0},", institutionStatistics.AccountUsage.ContentCount)
			.AppendFormat("                    {0},", institutionStatistics.AccountUsage.TocCount)
			.AppendFormat("                    {0},", institutionStatistics.AccountUsage.SessionCount)
			.AppendFormat("                    {0},", institutionStatistics.AccountUsage.PrintCount)
			.AppendFormat("                    {0},", institutionStatistics.AccountUsage.EmailCount)
			.AppendFormat("                    {0},", institutionStatistics.AccountUsage.TurnawayConcurrencyCount)
			.AppendFormat("                    {0})", institutionStatistics.AccountUsage.TurnawayAccessCount)
			.ToString();
		int count = ExecuteInsertStatementReturnRowCount(sql, null, logSql: false);
		return count > 0;
	}

	public int InsertMonthlyResourceStatistics(InstitutionStatistics institutionStatistics)
	{
		string sqlStatement = string.Format("\nInsert into InstitutionMonthlyResourceStatistics (institutionId, aggregationDate, resourceId, purchased, archivedPurchased\n, newEditionPreviousPurchased, pdaAdded, pdaAddedToCart, pdaNewEdition, expertRecommended)\nselect @InstitutionId, @StartDate, * from \n(\n    select r.iResourceId\n    ,case when (irl.tiLicenseTypeId = 1  and irl.dtFirstPurchaseDate between @StartDate and @EndDate)\t\t\t\tthen 1 else 0 end as Purchased\n    ,case when (irl.tiLicenseTypeId = 1  and re.dateArchivedEmail between @StartDate and @EndDate)\t\t\t\t\tthen 1 else 0 end as ArchivedPurchased\n    ,case when (irl2.iInstitutionResourceLicenseId > 0 and irl.dtFirstPurchaseDate between @StartDate and @EndDate)\tthen 1 else 0 end as NewEditionPreviousPurchased\n    ,case when (irl.tiLicenseTypeId = 3 and irl.dtPdaAddedDate between @StartDate and @EndDate)\t\t\t\t\t\tthen 1 else 0 end as PdaAdded\n    ,case when (irl.tiLicenseTypeId = 3 and irl.dtPdaAddedToCartDate between @StartDate and @EndDate)\t\t\t\tthen 1 else 0 end as PdaAddedToCart\n    ,case when (re3.dateNewResourceEmail between @StartDate and @EndDate)\t\t\t\t\t\t\t\t\t\t\tthen 1 else 0 end as PdaNewEdition\n    ,case when (ir.dtCreationDate between @StartDate and @EndDate)\t\t\t\t\t\t\t\t\t\t\t\t\tthen 1 else 0 end as ExpertRecommended\n    from {0}..tInstitutionResourceLicense irl \n    join {0}..tResource r on irl.iResourceId = r.iResourceId\n    left join {1}..ResourceEmails re on r.vchIsbn10 = re.resourceISBN\n    left join {0}..tResource r2 on r.iPrevEditResourceID = r2.iResourceId\n    left join {0}..tInstitutionResourceLicense irl2 on r2.iResourceId = irl2.iResourceId and irl.iInstitutionId = irl2.iInstitutionId\n    left join {0}..tResource r3 on irl.iResourceId = r3.iPrevEditResourceID and irl.tiLicenseTypeId = 3\n    left join {1}..ResourceEmails re3 on  r3.vchIsbn10 = re3.resourceISBN\n    left join {0}..tInstitutionRecommendation ir on r.iResourceId = ir.iResourceId and ir.iInstitutionId = @InstitutionId\n\t\t\t\tand ir.tiRecordStatus = 1 and ir.dtDeletedDate is null\n    where irl.iInstitutionId = @InstitutionId and irl.tiRecordStatus = 1\n) t\nwhere t.Purchased > 0 or t.ArchivedPurchased > 0 or NewEditionPreviousPurchased > 0 or PdaAdded > 0 or PdaAddedToCart > 0 or PdaNewEdition >  0 or ExpertRecommended > 0\norder by iResourceId\n", _r2UtilitiesSettings.R2DatabaseName, _r2UtilitiesSettings.R2UtilitiesDatabaseName);
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("InstitutionId", institutionStatistics.InstitutionId),
			new DateTimeParameter("StartDate", institutionStatistics.StartDate),
			new DateTimeParameter("EndDate", institutionStatistics.StartDate.AddMonths(1).AddSeconds(-1.0))
		};
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			cnn = GetConnection(base.ConnectionString);
			command = new SqlCommand("SET ARITHABORT ON", cnn);
			command.ExecuteNonQuery();
			command = GetSqlCommand(cnn, sqlStatement, parameters, 300, null);
			return command.ExecuteNonQuery();
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

	public int AggregateInstitutionResourceStatisticsCount(DateTime startDate, DateTime endDate)
	{
		R2UtilitiesBase.Log.InfoFormat("AggregateInstitutionResourceStatisticsCount(StartDate: {0} - EndDate: {1})", startDate, endDate);
		bool useNewerQuery = startDate > DateTime.Now.AddMonths(-6);
		string sqlStatement = $"\nINSERT INTO DailyInstitutionResourceStatisticsCount\n           ([institutionId]\n           ,[resourceId]\n           ,[ipAddressInteger]\n           ,[institutionResourceStatisticsDate]\n           ,[licenseType]\n           ,[resourceStatusId]\n           ,[contentRetrievalCount]\n           ,[tocRetrievalCount]\n           ,[sessionCount]\n           ,[printCount]\n           ,[emailCount]\n           ,[accessTurnawayCount]\n           ,[concurrentTurnawayCount])\n select agg.institutionId, agg.resourceId, agg.ipAddressInteger, agg.institutionResourceStatisticsDate, agg.licenseType, agg.resourceStatusId\n, sum(agg.contentCount) as contentCount\n, sum(agg.tocCount) as tocCount\n, sum(agg.sessionCount) as sessionCount\n, sum(agg.printCount) as printCount\n, sum(agg.emailCount) as emailCount\n, sum(agg.accessCount) as accessCount\n, sum(agg.concurrencyCount) as concurrencyCount\nfrom\n{(useNewerQuery ? GetInstitutionResourceStatisticsBaseWithinSixMonthsQuery(startDate, endDate) : GetInstitutionResourceStatisticsBaseBeforeSixMonthsQuery(startDate, endDate))}\nleft join DailyInstitutionResourceStatisticsCount dirsc on \nagg.institutionId = dirsc.institutionId\nand agg.resourceId = dirsc.resourceId\nand agg.ipAddressInteger = dirsc.ipAddressInteger\nand agg.institutionResourceStatisticsDate = dirsc.institutionResourceStatisticsDate\nand agg.licenseType = dirsc.licenseType\nand agg.resourceStatusId = dirsc.resourceStatusId\n\nwhere dirsc.dailyInstitutionResourceStatisticsCountId is null\n\ngroup by agg.institutionId, agg.resourceId, agg.ipAddressInteger, agg.institutionResourceStatisticsDate, agg.licenseType, agg.resourceStatusId\norder by 4, 1, 2\n";
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			cnn = GetConnection(base.ConnectionString);
			command = cnn.CreateCommand();
			command.CommandTimeout = _r2UtilitiesSettings.AggregateDailyCommandTimeout;
			command.CommandText = sqlStatement;
			int rows = command.ExecuteNonQuery();
			if (rows == 0)
			{
				sqlStatement = $"\nINSERT INTO DailyInstitutionResourceStatisticsCount\n           ([institutionId]\n           ,[resourceId]\n           ,[ipAddressInteger]\n           ,[institutionResourceStatisticsDate]\n           ,[licenseType]\n           ,[resourceStatusId]\n           ,[contentRetrievalCount]\n           ,[tocRetrievalCount]\n           ,[sessionCount]\n           ,[printCount]\n           ,[emailCount]\n           ,[accessTurnawayCount]\n           ,[concurrentTurnawayCount])\n select agg.institutionId, agg.resourceId, agg.ipAddressInteger, agg.institutionResourceStatisticsDate, agg.licenseType, agg.resourceStatusId\n, sum(agg.contentCount) as contentCount\n, sum(agg.tocCount) as tocCount\n, sum(agg.sessionCount) as sessionCount\n, sum(agg.printCount) as printCount\n, sum(agg.emailCount) as emailCount\n, sum(agg.accessCount) as accessCount\n, sum(agg.concurrencyCount) as concurrencyCount\nfrom\n{(useNewerQuery ? GetInstitutionResourceStatisticsBaseBeforeSixMonthsQuery(startDate, endDate) : GetInstitutionResourceStatisticsBaseWithinSixMonthsQuery(startDate, endDate))}\nleft join DailyInstitutionResourceStatisticsCount dirsc on \nagg.institutionId = dirsc.institutionId\nand agg.resourceId = dirsc.resourceId\nand agg.ipAddressInteger = dirsc.ipAddressInteger\nand agg.institutionResourceStatisticsDate = dirsc.institutionResourceStatisticsDate\nand agg.licenseType = dirsc.licenseType\nand agg.resourceStatusId = dirsc.resourceStatusId\n\nwhere dirsc.dailyInstitutionResourceStatisticsCountId is null\n\ngroup by agg.institutionId, agg.resourceId, agg.ipAddressInteger, agg.institutionResourceStatisticsDate, agg.licenseType, agg.resourceStatusId\norder by 4, 1, 2\n";
				command.CommandText = sqlStatement;
				rows = command.ExecuteNonQuery();
			}
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

	public int UpdateInstitutionResourceStatisticsCount(DateTime startDate, DateTime endDate)
	{
		string sql = "\nupdate dirsc\nset contentRetrievalCount = agg2.contentCount\n, tocRetrievalCount = agg2.tocCount\n, sessionCount = agg2.sessionCount\n, printCount = agg2.printCount\n, emailCount = agg2.emailCount\n, accessTurnawayCount = agg2.accessCount\n, concurrentTurnawayCount = agg2.concurrencyCount\nFROM            DailyInstitutionResourceStatisticsCount AS dirsc\nJOIN (\n\nSELECT        agg.institutionId, agg.resourceId, agg.ipAddressInteger, agg.institutionResourceStatisticsDate, SUM(agg.contentCount) AS contentCount, SUM(agg.tocCount) \n                         AS tocCount, SUM(agg.sessionCount) AS sessionCount, SUM(agg.printCount) AS printCount, SUM(agg.emailCount) AS emailCount, SUM(agg.accessCount) \n                         AS accessCount, SUM(agg.concurrencyCount) AS concurrencyCount\n\t\t\t\t\t\t from\n\n{0}\njoin DailyInstitutionResourceStatisticsCount AS dirsc \nON  agg.institutionId = dirsc.institutionId \nAND agg.resourceId = dirsc.resourceId \nAND agg.ipAddressInteger = dirsc.ipAddressInteger \nAND agg.institutionResourceStatisticsDate = dirsc.institutionResourceStatisticsDate\nGROUP BY \nagg.institutionId, agg.resourceId, agg.ipAddressInteger, agg.institutionResourceStatisticsDate\n, dirsc.accessTurnawayCount\n, dirsc.concurrentTurnawayCount\n, dirsc.contentRetrievalCount\n, dirsc.emailCount\n, dirsc.printCount\n, dirsc.sessionCount\n, dirsc.tocRetrievalCount\nhaving \nsum(agg.accessCount) <> dirsc.accessTurnawayCount\nor sum(agg.concurrencyCount) <> dirsc.concurrentTurnawayCount\nor sum(agg.contentCount) <> dirsc.contentRetrievalCount\nor sum(agg.emailCount) <> dirsc.emailCount\nor sum(agg.printCount) <> dirsc.printCount\nor sum(agg.sessionCount) <> dirsc.sessionCount\nor sum(agg.tocCount) <> dirsc.tocRetrievalCount\n\n)  AS agg2 ON agg2.institutionId = dirsc.institutionId \nAND agg2.resourceId = dirsc.resourceId \nAND agg2.ipAddressInteger = dirsc.ipAddressInteger \nAND agg2.institutionResourceStatisticsDate = dirsc.institutionResourceStatisticsDate\n";
		bool useNewerQuery = startDate >= DateTime.Now.AddMonths(-6);
		string sqlStatement = string.Format(sql, useNewerQuery ? GetInstitutionResourceStatisticsBaseWithinSixMonthsQuery(startDate, endDate) : GetInstitutionResourceStatisticsBaseBeforeSixMonthsQuery(startDate, endDate));
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			cnn = GetConnection(base.ConnectionString);
			command = cnn.CreateCommand();
			command.CommandText = sqlStatement;
			int rows = command.ExecuteNonQuery();
			if (rows == 0)
			{
				sqlStatement = string.Format(sql, useNewerQuery ? GetInstitutionResourceStatisticsBaseBeforeSixMonthsQuery(startDate, endDate) : GetInstitutionResourceStatisticsBaseWithinSixMonthsQuery(startDate, endDate));
				command.CommandText = sqlStatement;
				rows = command.ExecuteNonQuery();
			}
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

	private string GetInstitutionResourceStatisticsBaseWithinSixMonthsQuery(DateTime startDate, DateTime endDate)
	{
		string sql = "\n\n (SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date) AS institutionResourceStatisticsDate, count(institutionId) \n\t\t\t\t\t\t\tAS concurrencyCount, 0 AS accessCount, 0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            ContentView cv \n  WHERE        (turnawayTypeId = 20) AND (institutionId > 0) AND (contentViewTimestamp >= '{0}' AND contentViewTimestamp < '{1}')\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(contentViewTimestamp as date)\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date) AS institutionResourceStatisticsDate, 0 AS concurrencyCount, \n\t\t\t\t\t\t   count(institutionId) AS accessCount, 0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            ContentView cv \n  WHERE        (turnawayTypeId = 21) AND (institutionId > 0) AND (contentViewTimestamp >= '{0}' AND contentViewTimestamp < '{1}')\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date)\n  UNION ALL\n  SELECT        pv.institutionId, resourceId, pv.ipAddressInteger, licenseType, resourceStatusId, cast(pv.pageViewTimestamp as date) AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\tcount(distinct pv.sessionId) AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            PageView pv\n  join\t\t      ContentView cv on pv.requestId = cv.requestId \n  WHERE        ( pv.institutionId > 0) AND (pageViewTimestamp >= '{0}' AND pageViewTimestamp < '{1}')\n  GROUP BY  pv.institutionId, resourceId, pv.ipAddressInteger, licenseType, resourceStatusId, cast(pv.pageViewTimestamp as date)\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date) AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\t\t\t\t\t\t   0 AS sessionCount, count(institutionId) AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            ContentView cv\n  WHERE        (institutionId > 0) AND (contentViewTimestamp >= '{0}' AND contentViewTimestamp < '{1}') AND (chapterSectionId IS NULL) \n\t\t\t\tAND (actionTypeId = 0) and turnawayTypeId = 0\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date)\n  UNION ALL\n  SELECT        cv.institutionId, cv.resourceId, cv.ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date) AS institutionResourceStatisticsDate, 0 AS concurrencyCount, \n\t\t\t\t\t\t   0 AS accessCount, 0 AS sessionCount, 0 AS tocCount, count(cv.institutionId) AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            ContentView cv \n  WHERE        (cv.institutionId > 0) AND (contentViewTimestamp >= '{0}' AND contentViewTimestamp < '{1}') AND (cv.chapterSectionId IS NOT NULL) \n\t\t\t\t\tAND (cv.actionTypeId = 0) and turnawayTypeId = 0\n  GROUP BY cv.institutionId, cv.resourceId, cv.ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date)\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date) AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\t\t\t\t\t\t   0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, count(institutionId) AS printCount, 0 AS emailCount\n  FROM            ContentView cv\n  WHERE        (institutionId > 0) AND (contentViewTimestamp >= '{0}' AND contentViewTimestamp < '{1}') AND (actionTypeId = 16)\n\t\t\t\t and turnawayTypeId = 0\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date)\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date) AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\t\t\t\t\t\t   0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, count(institutionId) AS emailCount\n  FROM            ContentView cv\n  WHERE        (institutionId > 0) AND (contentViewTimestamp >= '{0}' AND contentViewTimestamp < '{1}') AND (actionTypeId = 17)\n\t\t\t\t and turnawayTypeId = 0\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, cast(cv.contentViewTimestamp as date)) AS agg \n";
		return string.Format(sql, startDate, endDate);
	}

	private string GetInstitutionResourceStatisticsBaseBeforeSixMonthsQuery(DateTime startDate, DateTime endDate)
	{
		string sql = "\n (SELECT        institutionId, resourceId, ipAddressInteger, 0 as licenseType, 0 as resourceStatusId, contentTurnawayDate AS institutionResourceStatisticsDate, sum(dctc.contentTurnawayCount) \n\t\t\t\t\t\t\tAS concurrencyCount, 0 AS accessCount, 0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            DailyContentTurnawayCount dctc \n  WHERE        (turnawayTypeId = 20) AND (institutionId > 0) AND (contentTurnawayDate = '{0}')\n  GROUP BY institutionId, resourceId, ipAddressInteger, contentTurnawayDate\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, 0 as licenseType, 0 as resourceStatusId, contentTurnawayDate AS institutionResourceStatisticsDate, 0 AS concurrencyCount, \n\t\t\t\t\t\t   sum(contentTurnawayCount) AS accessCount, 0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            DailyContentTurnawayCount\n  WHERE        (turnawayTypeId = 21) AND (institutionId > 0) AND (contentTurnawayDate = '{0}' )\n  GROUP BY institutionId, resourceId, ipAddressInteger, contentTurnawayDate\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, 0 as licenseType, 0 as resourceStatusId, sessionDate AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\tsum(sessionCount) AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            DailyResourceSessionCount\n  WHERE        ( institutionId > 0) AND (sessionDate = '{0}')\n  GROUP BY  institutionId, resourceId, ipAddressInteger, sessionDate\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\t\t\t\t\t\t   0 AS sessionCount, sum(contentViewCount) AS tocCount, 0 AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            DailyContentViewCount\n  WHERE        (institutionId > 0) AND (contentViewDate = '{0}') AND (chapterSectionId IS NULL) \n\t\t\t\tAND (actionTypeId = 0)\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate AS institutionResourceStatisticsDate, 0 AS concurrencyCount, \n\t\t\t\t\t\t   0 AS accessCount, 0 AS sessionCount, 0 AS tocCount, sum(contentViewCount) AS contentCount, 0 AS printCount, 0 AS emailCount\n  FROM            DailyContentViewCount \n  WHERE        (institutionId > 0) AND (contentViewDate = '{0}') AND (chapterSectionId IS NOT NULL) \n\t\t\t\t\tAND (actionTypeId = 0)\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\t\t\t\t\t\t   0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, sum(contentViewCount) AS printCount, 0 AS emailCount\n  FROM            DailyContentViewCount\n  WHERE        (institutionId > 0) AND (contentViewDate = '{0}') AND (actionTypeId = 16)\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate\n  UNION ALL\n  SELECT        institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate AS institutionResourceStatisticsDate, 0 AS concurrencyCount, 0 AS accessCount, \n\t\t\t\t\t\t   0 AS sessionCount, 0 AS tocCount, 0 AS contentCount, 0 AS printCount, count(institutionId) AS emailCount\n  FROM            DailyContentViewCount\n  WHERE        (institutionId > 0) AND (contentViewDate = '{0}') AND (actionTypeId = 17)\n  GROUP BY institutionId, resourceId, ipAddressInteger, licenseType, resourceStatusId, contentViewDate) AS agg \n";
		return string.Format(sql, startDate.ToString("d"));
	}

	public void RebuildAndReorgIndexes()
	{
		_reportDataService.RebuildAndReorgIndexes(_r2UtilitiesSettings.R2ReportsDatabaseName);
	}
}
