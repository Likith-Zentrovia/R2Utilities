using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Compression;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class Ip2LocationTask : TaskBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public Ip2LocationTask(IR2UtilitiesSettings r2UtilitiesSettings)
		: base("Ip2LocationTask", "-Ip2LocationTask", "25", TaskGroup.DiagnosticsMaintenance, "Task will load Ip2Location data", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will download the latest Ip2Location database and update the database.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "Ip2LocationTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			DownloadFileAndExtract(_r2UtilitiesSettings.Ip2LocationWorkingFolder);
			string ip2LocationFile = Path.Combine(_r2UtilitiesSettings.Ip2LocationWorkingFolder, "IPCountry.csv");
			CopyFileToLocations(ip2LocationFile);
			int rowsInserted = ProcessFile(ip2LocationFile);
			step.Results = $"Ip2Location Rows Insertred: {rowsInserted}";
			step.CompletedSuccessfully = rowsInserted > 0;
			Directory.Delete(_r2UtilitiesSettings.Ip2LocationWorkingFolder, recursive: true);
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

	private int ProcessFile(string ip2LocationFile)
	{
		try
		{
			DataTable baseDataTable = GetBulkInsertDataTable(_r2UtilitiesSettings.Ip2LocationTableName);
			DataTable populatedDataTable = BuildDataTableAndValidateFile(baseDataTable, ip2LocationFile, ",");
			InsertIp2LocationData(populatedDataTable);
			return UpdateWebDatabase();
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	private void DownloadFileAndExtract(string workingDirectory)
	{
		try
		{
			string filePathAndName = GetDownloadFileName(workingDirectory);
			WebClient webClient = new WebClient();
			webClient.DownloadFile(_r2UtilitiesSettings.Ip2LocationDownloadUrl, filePathAndName);
			ZipHelper.ExtractAll(filePathAndName, workingDirectory);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	private string GetDownloadFileName(string workingDirectory)
	{
		if (Directory.Exists(workingDirectory))
		{
			Directory.Delete(workingDirectory, recursive: true);
		}
		Directory.CreateDirectory(workingDirectory);
		string fileName = "Ip2Location-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".zip";
		return Path.Combine(workingDirectory, fileName);
	}

	private DataTable BuildDataTableAndValidateFile(DataTable dataTable, string fileNameAndPath, string delimiter)
	{
		using (TextFieldParser parser = new TextFieldParser(fileNameAndPath))
		{
			parser.HasFieldsEnclosedInQuotes = true;
			parser.SetDelimiters(delimiter);
			while (!parser.EndOfData)
			{
				string[] fields = parser.ReadFields();
				DataRow row = dataTable.NewRow();
				int i = 0;
				try
				{
					if (fields != null)
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
				catch (Exception ex)
				{
					R2UtilitiesBase.Log.Error(ex.Message, ex);
					throw;
				}
				dataTable.Rows.Add(row);
			}
		}
		return dataTable;
	}

	private DataTable GetBulkInsertDataTable(string databaseTableName)
	{
		try
		{
			DataTable dataTable = new DataTable();
			using (SqlConnection connection = new SqlConnection(_r2UtilitiesSettings.Ip2LocationConnection))
			{
				connection.Open();
				string sb = new StringBuilder().Append("select ").Append("CASE WHEN DATA_TYPE = 'varchar' then 'System.String' ").Append("WHEN DATA_TYPE = 'nvarchar' then 'System.String' ")
					.Append("WHEN DATA_TYPE = 'datetime' then 'System.DateTime' ")
					.Append("WHEN DATA_TYPE = 'smalldatetime' then 'System.DateTime' ")
					.Append("WHEN DATA_TYPE = 'bit' then 'System.Int32' ")
					.Append("WHEN DATA_TYPE = 'int' then 'System.Int32' ")
					.Append("when DATA_TYPE = 'char' then 'System.String' ")
					.Append("when DATA_TYPE = 'money' then 'System.Decimal' ")
					.Append("when DATA_TYPE = 'float' then 'System.Decimal' ")
					.Append("when DATA_TYPE = 'decimal' then 'System.Decimal' ")
					.Append("when DATA_TYPE = 'smallint' then 'System.Int16' ")
					.Append("when DATA_TYPE = 'bigint' then 'System.Int64' ")
					.Append("when DATA_TYPE = 'varbinary' then 'System.Byte[]' ")
					.Append("when DATA_TYPE = 'tinyint' then 'System.Boolean' ")
					.Append("when DATA_TYPE = 'text' then 'System.String' ")
					.Append("END, COLUMN_NAME, IS_NULLABLE from ")
					.AppendFormat("INFORMATION_SCHEMA.COLUMNS IC where TABLE_NAME = '{0}' ", databaseTableName.Replace("dbo.", ""))
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

	private void InsertIp2LocationData(DataTable dataTable)
	{
		try
		{
			string tSql = new StringBuilder().AppendFormat("Select Count(*) from {0};", _r2UtilitiesSettings.Ip2LocationTableName).ToString();
			int rows = ExecuteBasicCountQuery(tSql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.Ip2LocationConnection);
			tSql = new StringBuilder().AppendFormat("truncate table {0};", _r2UtilitiesSettings.Ip2LocationTableName).ToString();
			ExecuteBasicCountQuery(tSql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.Ip2LocationConnection);
			R2UtilitiesBase.Log.DebugFormat("Rows Truncated: {0}", rows);
			using (SqlConnection connection = new SqlConnection(_r2UtilitiesSettings.Ip2LocationConnection))
			{
				connection.Open();
				using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);
				bulkCopy.DestinationTableName = _r2UtilitiesSettings.Ip2LocationTableName;
				bulkCopy.WriteToServer(dataTable);
			}
			string sql = new StringBuilder().AppendFormat("Select Count(*) from {0};", _r2UtilitiesSettings.Ip2LocationTableName).ToString();
			rows = ExecuteBasicCountQuery(sql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.Ip2LocationConnection);
			R2UtilitiesBase.Log.DebugFormat("Rows Inserted: {0}", rows);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	private int UpdateWebDatabase()
	{
		string sql = new StringBuilder().AppendFormat("Select Count(*) from tIp2Location;").ToString();
		int rows = ExecuteBasicCountQuery(sql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
		sql = new StringBuilder().AppendFormat("truncate table tIp2Location;").ToString();
		ExecuteBasicCountQuery(sql, new List<ISqlCommandParameter>(), logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
		R2UtilitiesBase.Log.DebugFormat("tIp2Location Rows Truncated: {0}", rows);
		sql = new StringBuilder().Append("insert into tIp2Location (iIpTo,iIpFrom,vchCountryCode,vchCountryName)").Append("           select ip_from, ip_to, country_code, country_name").AppendFormat("     from   {0}..{1}", _r2UtilitiesSettings.Ip2LocationDatabaseName, _r2UtilitiesSettings.Ip2LocationTableName)
			.Append("           order by  ip_from")
			.ToString();
		rows = ExecuteInsertStatementReturnRowCount(sql, null, logSql: true, _r2UtilitiesSettings.R2DatabaseConnection);
		R2UtilitiesBase.Log.DebugFormat("tIp2Location Rows Inserted: {0}", rows);
		return rows;
	}

	private void CopyFileToLocations(string originalFileLocation)
	{
		try
		{
			string fileLocationsString = _r2UtilitiesSettings.Ip2LocationFileDestinations;
			List<string> destinationFileLocations = (from x in fileLocationsString.Split(';')
				where !string.IsNullOrWhiteSpace(x)
				select x).ToList();
			if (!destinationFileLocations.Any())
			{
				return;
			}
			FileInfo fileInfo = new FileInfo(originalFileLocation);
			foreach (string destinationFileLocation in destinationFileLocations)
			{
				string newFile = Path.Combine(destinationFileLocation, fileInfo.Name);
				File.Copy(fileInfo.FullName, newFile, overwrite: true);
			}
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}
}
