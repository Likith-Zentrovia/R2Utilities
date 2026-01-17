using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess.LogEvents;

public class LogEvent : FactoryBase, IDataEntity
{
	public int LogEventId { get; set; }

	public DateTime Timestamp { get; set; }

	public DateTime Last_Occurance { get; set; }

	public string Thread { get; set; }

	public string Hostname { get; set; }

	public int LevelInt { get; set; }

	public string Level { get; set; }

	public string Logger { get; set; }

	public string Version { get; set; }

	public string Message { get; set; }

	public string Exception { get; set; }

	public string RequestId { get; set; }

	public string SessionId { get; set; }

	public string Url { get; set; }

	public string IpAddress { get; set; }

	public string UserAgent { get; set; }

	public string StackTrace { get; set; }

	public int ServerNumber { get; set; }

	public string Referrer { get; set; }

	public string ExceptionHash { get; set; }

	public int ReportCount { get; set; }

	public List<string> FieldsPopulated { get; set; }

	public void Populate(SqlDataReader reader)
	{
		FieldsPopulated = new List<string>();
		PopulateField(reader, LogEventId, "LogEventId");
		PopulateField(reader, Timestamp, "Timestamp");
		PopulateField(reader, Last_Occurance, "Last_Occurance");
		PopulateField(reader, Thread, "Thread");
		PopulateField(reader, Hostname, "Hostname");
		PopulateField(reader, LevelInt, "LevelInt");
		PopulateField(reader, Level, "Level");
		PopulateField(reader, Logger, "Logger");
		PopulateField(reader, Version, "Version");
		PopulateField(reader, Message, "Message");
		PopulateField(reader, Exception, "Exception");
		PopulateField(reader, RequestId, "RequestId");
		PopulateField(reader, SessionId, "SessionId");
		PopulateField(reader, Url, "Url");
		PopulateField(reader, IpAddress, "IpAddress");
		PopulateField(reader, UserAgent, "UserAgent");
		PopulateField(reader, StackTrace, "StackTrace");
		PopulateField(reader, ServerNumber, "ServerNumber");
		PopulateField(reader, Referrer, "Referrer");
		PopulateField(reader, ExceptionHash, "ExceptionHash");
		PopulateField(reader, ReportCount, "ReportCount");
	}

	private void PopulateField(SqlDataReader reader, object field, string fieldName)
	{
		Type fieldType = field?.GetType() ?? typeof(string);
		if (string.IsNullOrWhiteSpace(fieldName) || !reader.HasColumn(fieldName))
		{
			return;
		}
		PropertyInfo propertyInfo = GetType().GetProperty(fieldName);
		if (!(propertyInfo == null))
		{
			if (fieldType == typeof(int))
			{
				propertyInfo.SetValue(this, GetInt32Value(reader, fieldName, 0));
			}
			if (fieldType == typeof(string))
			{
				propertyInfo.SetValue(this, GetStringValue(reader, fieldName));
			}
			if (fieldType == typeof(bool))
			{
				propertyInfo.SetValue(this, GetBoolValue(reader, fieldName, defaultValue: false));
			}
			if (fieldType == typeof(DateTime))
			{
				propertyInfo.SetValue(this, GetDateValue(reader, fieldName));
			}
			FieldsPopulated.Add(fieldName);
		}
	}
}
