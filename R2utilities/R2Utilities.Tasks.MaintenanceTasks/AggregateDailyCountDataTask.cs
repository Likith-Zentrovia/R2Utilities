using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class AggregateDailyCountDataTask : TaskBase, ITask
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ReportDataService _reportDataService;

	private readonly ReportWebDataService _reportWebDataService;

	private string _databaseTable;

	private string _databaseTableTemp;

	private string _delimiter;

	private string _file;

	public AggregateDailyCountDataTask(ReportDataService reportDataService, ReportWebDataService reportWebDataService, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("AggregateDailyCountData", "-AggregateDailyCountData", "11", TaskGroup.ContentLoading, "Task to aggregate daily counts for reports", enabled: true)
	{
		_reportDataService = reportDataService;
		_reportWebDataService = reportWebDataService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_file = GetArgument("file");
		_delimiter = GetArgument("delimiter");
		_databaseTable = GetArgument("databasetable");
		_databaseTableTemp = _databaseTable + "_Temp";
		R2UtilitiesBase.Log.Info("-job: AggregateDailyCountData, -file: " + _file + ", -delimiter: " + _delimiter + ", -databasetable: " + _databaseTable);
	}

	public override void Run()
	{
		base.TaskResult.Information = new StringBuilder().Append("This task will update the daily counts for multiple tables in the R2Reports Database. ").Append("After that it will BCP the data out, zip it up, and re-build the indexes. ").ToString();
		TaskResultStep step = new TaskResultStep
		{
			Name = "AggregateDailyCountData",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			if (string.IsNullOrWhiteSpace(_file))
			{
				StringBuilder stepResults = new StringBuilder();
				SetAggregateAndDeleteDates(out var aggregateStart, out var deleteStartDate);
				bool aggregateSuccess = AggregateData(aggregateStart, stepResults);
				bool deleteSuccess = BcpOutAndDeleteData(deleteStartDate, stepResults);
				_reportDataService.RebuildIndexTables();
				step.Results = stepResults.ToString();
				step.CompletedSuccessfully = aggregateSuccess && deleteSuccess;
			}
			else
			{
				DataTable dataTable = GetBulkInsertDataTable();
				int rowsErrors = BuildDataTableAndValidateFile(dataTable);
				int rowsInsert = InsertDataTable(dataTable);
				CopyFromTemptoDestination();
				step.Results = $"Rows: {rowsInsert} Insert into {_databaseTable}";
				step.CompletedSuccessfully = rowsErrors == 0;
			}
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			step.CompletedSuccessfully = false;
			step.Results = ex.Message;
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private void SetAggregateAndDeleteDates(out DateTime aggregateStartDate, out DateTime deleteStartDate)
	{
		DateTime newestDailyPageView = _reportDataService.GetNewestDailyPageView().AddDays(1.0);
		if (newestDailyPageView.Day != 1)
		{
			newestDailyPageView = newestDailyPageView.AddMonths(1);
		}
		aggregateStartDate = new DateTime(newestDailyPageView.Year, newestDailyPageView.Month, 1);
		DateTime oldestContentView = _reportDataService.GetOldestContentView();
		deleteStartDate = new DateTime(oldestContentView.Year, oldestContentView.Month, 1);
	}

	private bool AggregateData(DateTime aggregateMonth, StringBuilder results)
	{
		try
		{
			DateTime stopDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
			while (aggregateMonth != stopDate)
			{
				R2UtilitiesBase.Log.InfoFormat("- - - - - - - - - -");
				int contentViewCount = _reportDataService.AggregateContentViewCount(aggregateMonth);
				R2UtilitiesBase.Log.InfoFormat("- - - - - - - - - -");
				int contentViewTurnawayCount = _reportDataService.AggregateContentViewTurnawayCount(aggregateMonth);
				R2UtilitiesBase.Log.InfoFormat("- - - - - - - - - -");
				int pageViewCount = _reportDataService.AggregatePageViewCount(aggregateMonth);
				R2UtilitiesBase.Log.InfoFormat("- - - - - - - - - -");
				int pageViewSessionCount = _reportDataService.AggregatePageViewSessionCount(aggregateMonth);
				R2UtilitiesBase.Log.InfoFormat("- - - - - - - - - -");
				int pageViewResourceSessionCount = _reportDataService.AggregatePageViewResourceSessionCount(aggregateMonth);
				R2UtilitiesBase.Log.InfoFormat("- - - - - - - - - -");
				int searchCount = _reportDataService.AggregateSearchCount(aggregateMonth);
				R2UtilitiesBase.Log.InfoFormat("- - - - - - - - - -");
				results.AppendLine().AppendFormat("Aggregated Daily counts for {0}", aggregateMonth).AppendLine()
					.AppendFormat("{0} Content View", contentViewCount)
					.AppendLine()
					.AppendFormat("{0} Content View Turnaway", contentViewTurnawayCount)
					.AppendLine()
					.AppendFormat("{0} Page View", pageViewCount)
					.AppendLine()
					.AppendFormat("{0} Session", pageViewSessionCount)
					.AppendLine()
					.AppendFormat("{0} ResourceSession", pageViewResourceSessionCount)
					.AppendLine()
					.AppendFormat("{0} Search", searchCount)
					.AppendLine();
				DateTime viewMonth = aggregateMonth.AddMonths(1);
				_reportWebDataService.AlterDailyContentTurnawayCount(viewMonth);
				_reportWebDataService.AlterDailyContentViewCount(viewMonth);
				_reportWebDataService.AlterDailyPageViewCount(viewMonth);
				_reportWebDataService.AlterDailySearchCount(viewMonth);
				_reportWebDataService.AlterDailySessionCount(viewMonth);
				_reportWebDataService.AlterDailyResourceSessionCount(viewMonth);
				aggregateMonth = aggregateMonth.AddMonths(1);
			}
			if (aggregateMonth != stopDate)
			{
			}
			return true;
		}
		catch (Exception ex)
		{
			results.Append(ex.Message);
			return false;
		}
	}

	private bool BcpOutAndDeleteData(DateTime deleteStartDate, StringBuilder results)
	{
		try
		{
			DateTime monthToStop = GetBcpDeleteStopDate();
			if (deleteStartDate < monthToStop)
			{
				while (deleteStartDate != monthToStop)
				{
					bool exportSuccess = _reportDataService.BulkExportR2ReportsTables(deleteStartDate);
					int contentViewRowsDeleted = 0;
					int pageViewRowsDeleted = 0;
					int searchRowsDeleted = 0;
					if (exportSuccess)
					{
						contentViewRowsDeleted = _reportDataService.DeleteRangeFromTable(deleteStartDate, "ContentView", "cast(contentViewTimestamp as date)");
						pageViewRowsDeleted = _reportDataService.DeleteRangeFromTable(deleteStartDate, "PageView", "cast(pageViewTimestamp as date)");
						searchRowsDeleted = _reportDataService.DeleteRangeFromTable(deleteStartDate, "Search", "cast(searchTimestamp as date)");
					}
					results.AppendLine().AppendFormat("Removed data for {0}", deleteStartDate).AppendLine()
						.AppendFormat("{0} PageView rows deleted.", contentViewRowsDeleted)
						.AppendLine()
						.AppendFormat("{0} ContentView rows deleted.", pageViewRowsDeleted)
						.AppendLine()
						.AppendFormat("{0} Search rows deleted.", searchRowsDeleted)
						.AppendLine();
					deleteStartDate = deleteStartDate.AddMonths(1);
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			results.Append(ex.Message);
			return false;
		}
	}

	private DateTime GetBcpDeleteStopDate()
	{
		DateTime baseMonthToStop = DateTime.Now.AddMonths(-_r2UtilitiesSettings.AggregateDailyCountMonthsToGoBack);
		return new DateTime(baseMonthToStop.Year, baseMonthToStop.Month, 1);
	}

	private DataTable GetBulkInsertDataTable()
	{
		try
		{
			DataTable dataTable = new DataTable();
			using (SqlConnection connection = new SqlConnection(_r2UtilitiesSettings.R2ReportsConnection))
			{
				connection.Open();
				string sb = new StringBuilder().Append("select ").Append("CASE WHEN DATA_TYPE = 'varchar' then 'System.String' ").Append("WHEN DATA_TYPE = 'nvarchar' then 'System.String' ")
					.Append("WHEN DATA_TYPE = 'datetime' then 'System.DateTime' ")
					.Append("WHEN DATA_TYPE = 'smalldatetime' then 'System.DateTime' ")
					.Append("WHEN DATA_TYPE = 'bit' then 'System.Int32' ")
					.Append("WHEN DATA_TYPE = 'int' then 'System.Int32' ")
					.Append("when DATA_TYPE = 'tinyint' then 'System.Int32' ")
					.Append("when DATA_TYPE = 'char' then 'System.String' ")
					.Append("when DATA_TYPE = 'money' then 'System.Decimal' ")
					.Append("when DATA_TYPE = 'float' then 'System.Decimal' ")
					.Append("when DATA_TYPE = 'decimal' then 'System.Decimal' ")
					.Append("when DATA_TYPE = 'smallint' then 'System.Int16' ")
					.Append("when DATA_TYPE = 'bigint' then 'System.Int64' ")
					.Append("when DATA_TYPE = 'varbinary' then 'System.Byte[]' ")
					.Append("when DATA_TYPE = 'text' then 'System.String' ")
					.Append("END, COLUMN_NAME, IS_NULLABLE from ")
					.Append("INFORMATION_SCHEMA.COLUMNS IC where TABLE_NAME = '" + _databaseTableTemp.Replace("dbo.", "") + "' ")
					.ToString();
				SqlCommand tableInformationCommand = new SqlCommand(sb, connection);
				SqlDataReader reader = tableInformationCommand.ExecuteReader();
				while (reader.Read())
				{
					string datatype = reader.GetString(0);
					string columnName = reader.GetString(1);
					string allowNull = reader.GetString(2);
					dataTable.Columns.Add(new DataColumn
					{
						ColumnName = columnName,
						DataType = Type.GetType(datatype),
						AllowDBNull = (allowNull.ToLower() == "yes"),
						DefaultValue = null
					});
				}
				reader.Close();
			}
			R2UtilitiesBase.Log.Debug("GetBulkInsertDataTable was successfull");
			return dataTable;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}

	private int BuildDataTableAndValidateFile(DataTable dataTable)
	{
		int rowsErrors = 0;
		using (TextFieldParser parser = new TextFieldParser(_file))
		{
			parser.HasFieldsEnclosedInQuotes = false;
			if (_delimiter == "\\t")
			{
				parser.SetDelimiters("\t");
			}
			else
			{
				parser.SetDelimiters(_delimiter);
			}
			int columnCount = dataTable.Columns.Count;
			int rowCount = 0;
			while (!parser.EndOfData)
			{
				bool skipRow = false;
				rowCount++;
				string[] fields = parser.ReadFields();
				DataRow row = dataTable.NewRow();
				int i = 0;
				try
				{
					if (fields != null)
					{
						if (fields.Length > columnCount)
						{
							WriteErrorToFile(fields);
							rowsErrors++;
							skipRow = true;
						}
						else
						{
							string[] array = fields;
							foreach (string field in array)
							{
								if (field == null)
								{
									row.SetField(i, DBNull.Value);
								}
								else
								{
									row.SetField(i, field);
								}
								i++;
							}
						}
					}
				}
				catch (Exception ex)
				{
					R2UtilitiesBase.Log.Error(ex.Message, ex);
					throw;
				}
				if (!skipRow)
				{
					dataTable.Rows.Add(row);
				}
			}
		}
		return rowsErrors;
	}

	private void WriteErrorToFile(string[] errorRow)
	{
		string errorFile = _file.Replace(".dat", "_Error.dat");
		if (!File.Exists(errorFile))
		{
			using (StreamWriter sw = File.CreateText(errorFile))
			{
				sw.WriteLine(string.Join("\t", errorRow));
				return;
			}
		}
		using StreamWriter sw2 = File.AppendText(errorFile);
		sw2.WriteLine(string.Join("\t", errorRow));
	}

	private int InsertDataTable(DataTable dataTable)
	{
		int rows;
		try
		{
			string tSql = "Select Count(*) from " + _databaseTableTemp + ";";
			int rowCount = ExecuteBasicCountQuery(tSql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.R2ReportsConnection);
			R2UtilitiesBase.Log.Info($"Rows Before Insert: {rowCount}");
			tSql = "truncate table " + _databaseTableTemp + ";";
			rowCount = ExecuteBasicCountQuery(tSql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.R2ReportsConnection);
			R2UtilitiesBase.Log.Debug($"Rows Truncated: {rowCount}");
			using (SqlConnection connection = new SqlConnection(_r2UtilitiesSettings.R2ReportsConnection))
			{
				connection.Open();
				using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);
				bulkCopy.DestinationTableName = _databaseTableTemp;
				bulkCopy.WriteToServer(dataTable);
			}
			string sql = "Select Count(*) from " + _databaseTableTemp + ";";
			rows = ExecuteBasicCountQuery(sql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.R2ReportsConnection);
			R2UtilitiesBase.Log.Debug($"Rows Inserted: {rows}");
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		return rows;
	}

	private void CopyFromTemptoDestination()
	{
		StringBuilder sqlBuilder = new StringBuilder();
		if (_databaseTable.IndexOf("pageview", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			sqlBuilder.Append("INSERT INTO " + _databaseTable + "(institutionId, userId, ipAddressOctetA, ipAddressOctetB, ipAddressOctetC, ipAddressOctetD, ipAddressInteger ");
			sqlBuilder.Append(", pageViewTimestamp, pageViewRunTime, sessionId, url, requestId, referrer, countryCode, serverNumber, authenticationType) ");
			sqlBuilder.Append("select institutionId, userId, ipAddressOctetA, ipAddressOctetB, ipAddressOctetC, ipAddressOctetD, ipAddressInteger ");
			sqlBuilder.Append(", pageViewTimestamp, pageViewRunTime, sessionId, url, requestId, referrer, countryCode, serverNumber, authenticationType ");
			sqlBuilder.Append("from " + _databaseTableTemp);
		}
		if (_databaseTable.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			sqlBuilder.Append("INSERT INTO " + _databaseTable + "(institutionId , userId , searchTypeId , isArchive , isExternal , ipAddressOctetA ");
			sqlBuilder.Append(",ipAddressOctetB, ipAddressOctetC, ipAddressOctetD, ipAddressInteger, requestId, searchTimestamp) ");
			sqlBuilder.Append("select institutionId, userId, searchTypeId, isArchive, isExternal, ipAddressOctetA ");
			sqlBuilder.Append(", ipAddressOctetB, ipAddressOctetC, ipAddressOctetD, ipAddressInteger, requestId, searchTimestamp ");
			sqlBuilder.Append("from " + _databaseTableTemp);
		}
		if (_databaseTable.IndexOf("contentview", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			sqlBuilder.Append("INSERT INTO " + _databaseTable + "(institutionId, userId, resourceId, chapterSectionId, turnawayTypeId, ipAddressOctetA, ipAddressOctetB, ipAddressOctetC ");
			sqlBuilder.Append(", ipAddressOctetD, ipAddressInteger, contentViewTimestamp, actionTypeId, foundFromSearch, searchTerm, requestId, licenseType, resourceStatusId) ");
			sqlBuilder.Append("select institutionId, userId, resourceId, chapterSectionId, turnawayTypeId, ipAddressOctetA, ipAddressOctetB, ipAddressOctetC, ipAddressOctetD ");
			sqlBuilder.Append(", ipAddressInteger, contentViewTimestamp, actionTypeId, foundFromSearch, searchTerm, requestId, isnull(licenseType, 0), resourceStatusId ");
			sqlBuilder.Append("from " + _databaseTableTemp);
		}
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			cnn = GetConnection(_r2UtilitiesSettings.R2ReportsConnection);
			command = new SqlCommand("SET ARITHABORT ON", cnn);
			command.ExecuteNonQuery();
			command = GetSqlCommand(cnn, sqlBuilder.ToString(), null, 300, null);
			int rows = command.ExecuteNonQuery();
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
}
