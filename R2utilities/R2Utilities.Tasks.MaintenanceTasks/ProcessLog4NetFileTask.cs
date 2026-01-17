using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2Utility;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class ProcessLog4NetFileTask : TaskBase, ITask
{
	private const string InsertStatement_RabbitMqSendIssue = "insert into _temp_RabbitMqSendIssue([timestamp], sendTime, sessionId, requestId, logLevel, [server]) values(@Timestamp, @SendTime, @SessionId, @RequestId, @LogLevel, @Server)";

	private const string InsertStatement_RequestStartEnd = "insert into _temp_RequestStartEnd([timestamp], url, [start], exceptionHash, sessionId, requestId, logLevel, [server]) values(@Timestamp, @Url, @Start, @ExceptionHash, @SessionId, @RequestId, @LogLevel, @Server)";

	private string _job;

	private string _path;

	private string _server;

	protected new string TaskName = "ProcessLog4NetFileTask";

	public ProcessLog4NetFileTask()
		: base("ProcessLog4NetFileTask", "-ProcessLog4NetFileTask", "29", TaskGroup.DiagnosticsMaintenance, "Task for processing log4net files (uses args to specify functionality)", enabled: true)
	{
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_path = GetArgument("path");
		_server = GetArgument("server");
		_job = GetArgument("job");
		R2UtilitiesBase.Log.InfoFormat("-job: {0}, -server: {1}, -path: {2}", _job, _server, _path);
	}

	public override void Run()
	{
		StringBuilder taskInfo = new StringBuilder();
		TaskResultStep summaryStep = new TaskResultStep
		{
			Name = "ProcessLog4NetFile",
			StartTime = DateTime.Now,
			Results = string.Empty
		};
		base.TaskResult.AddStep(summaryStep);
		UpdateTaskResult();
		try
		{
			DirectoryInfo dirInfo = new DirectoryInfo(_path);
			Dictionary<string, LogEvent> startOnlyEvents = new Dictionary<string, LogEvent>();
			Dictionary<string, LogEvent> endOnlyEvents = new Dictionary<string, LogEvent>();
			List<LogEvent> nullRequestIdEvents = new List<LogEvent>();
			if (dirInfo.Exists)
			{
				FileInfo[] fileInfos = dirInfo.GetFiles();
				int totalFileCount = 0;
				int totalFileLineCount = 0;
				int totalMessageSendTimes = 0;
				FileInfo[] array = fileInfos;
				foreach (FileInfo fileInfo in array)
				{
					if (fileInfo.Extension == ".7z")
					{
						continue;
					}
					totalFileCount++;
					R2UtilitiesBase.Log.InfoFormat("Processing file {0} of {1} - {2}", totalFileCount, fileInfos.Length, fileInfo.Name);
					int fileLineCounter = 0;
					StreamReader file = new StreamReader(fileInfo.FullName);
					string line;
					while ((line = file.ReadLine()) != null)
					{
						fileLineCounter++;
						totalFileLineCount++;
						if (_job.Contains("requestQ"))
						{
							LogEvent logEvent = GetMessageSendTime(line);
							if (logEvent != null)
							{
								totalMessageSendTimes++;
								R2UtilitiesBase.Log.DebugFormat("messageSendTime: {0}, fileLineCounter: {1}, totalFileCount: {2}, totalMessageSendTimes: {3}, totalFileCount: {4}, totalFileLineCount: {5}", logEvent.SendTime, fileLineCounter, totalFileCount, totalMessageSendTimes, totalFileCount, totalFileLineCount);
								WriteToDbRabbitMqSendIssue(logEvent);
							}
						}
						if (!_job.Contains("start-stop"))
						{
							continue;
						}
						try
						{
							LogEvent logEvent2 = GetRequestLoggerModule(line);
							if (logEvent2 == null)
							{
								continue;
							}
							if (logEvent2.Start)
							{
								if (endOnlyEvents.ContainsKey(logEvent2.RequestId))
								{
									endOnlyEvents.Remove(logEvent2.RequestId);
									continue;
								}
								if (!startOnlyEvents.ContainsKey(logEvent2.RequestId))
								{
									startOnlyEvents.Add(logEvent2.RequestId, logEvent2);
									continue;
								}
								R2UtilitiesBase.Log.WarnFormat("startOnlyEvents already contains request id: {0}", logEvent2.RequestId);
							}
							else if (startOnlyEvents.ContainsKey(logEvent2.RequestId))
							{
								startOnlyEvents.Remove(logEvent2.RequestId);
							}
							else if (logEvent2.RequestId == "(null)")
							{
								nullRequestIdEvents.Add(logEvent2);
							}
							else
							{
								endOnlyEvents.Add(logEvent2.RequestId, logEvent2);
							}
						}
						catch (Exception ex)
						{
							R2UtilitiesBase.Log.ErrorFormat("ERROR --> {0}", line);
							R2UtilitiesBase.Log.Error(ex.Message, ex);
							throw;
						}
					}
				}
			}
			R2UtilitiesBase.Log.InfoFormat("----------------------------------------");
			R2UtilitiesBase.Log.InfoFormat("Start Only Count: {0}", startOnlyEvents.Count);
			foreach (string requestId in startOnlyEvents.Keys)
			{
				LogEvent logEvent3 = startOnlyEvents[requestId];
				R2UtilitiesBase.Log.InfoFormat("Start Only - Request Id: {0}, URL: {1}", requestId, logEvent3.Url);
				WriteToDbRequestStartEnd(logEvent3);
			}
			R2UtilitiesBase.Log.InfoFormat("End Only Count: {0}", endOnlyEvents.Count);
			foreach (string requestId2 in endOnlyEvents.Keys)
			{
				LogEvent logEvent4 = endOnlyEvents[requestId2];
				R2UtilitiesBase.Log.InfoFormat("End Only - Request Id: {0}, URL: {1} - [{2}] {3}", requestId2, logEvent4.Url, logEvent4.Level, logEvent4.ExceptionHash);
				WriteToDbRequestStartEnd(logEvent4);
			}
			R2UtilitiesBase.Log.InfoFormat("NULL Request Id Count: {0}", nullRequestIdEvents.Count);
			foreach (LogEvent logEvent5 in nullRequestIdEvents)
			{
				R2UtilitiesBase.Log.InfoFormat("End Only - Request Id: (null), URL: {0} - [{1}] {2}", logEvent5.Url, logEvent5.Level, logEvent5.ExceptionHash);
				WriteToDbRequestStartEnd(logEvent5);
			}
			summaryStep.CompletedSuccessfully = true;
		}
		catch (Exception ex2)
		{
			summaryStep.Results = "EXCEPTION: " + ex2.Message + "\r\n\t" + summaryStep.Results;
			summaryStep.CompletedSuccessfully = false;
			R2UtilitiesBase.Log.Error(ex2.Message, ex2);
			throw;
		}
		finally
		{
			base.TaskResult.Information = taskInfo.ToString();
			summaryStep.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private LogEvent GetMessageSendTime(string line)
	{
		int x = line.IndexOf("Message sent to Q.Prod.RequestData in", StringComparison.Ordinal);
		if (x > -1)
		{
			int y = line.IndexOf(" ms - {", x, StringComparison.Ordinal);
			string timeField = line.Substring(x + 37, y - x - 37);
			int sendTime = int.Parse(timeField);
			return new LogEvent(line.Substring(0, x), sendTime, _server);
		}
		return null;
	}

	private LogEvent GetRequestLoggerModule(string line)
	{
		string logLevel = GetLogLevel(line);
		if (string.IsNullOrEmpty(logLevel))
		{
			return null;
		}
		if (logLevel.Equals("INFO"))
		{
			int x = line.IndexOf("RequestLoggerModule -  >>>>>>", StringComparison.Ordinal);
			if (x > -1)
			{
				int y = line.IndexOf(", IP:", x, StringComparison.Ordinal);
				string page = line.Substring(x + 29, y - x - 29);
				return new LogEvent(line.Substring(0, x), page, _server, start: true);
			}
			return null;
		}
		if (logLevel.Equals("PT-OK") || logLevel.Equals("PT-WARN"))
		{
			int x2 = line.IndexOf(" <<<<<< ", StringComparison.Ordinal);
			if (x2 > -1)
			{
				int z = line.IndexOf(", ", x2, StringComparison.Ordinal);
				int y2 = line.IndexOf(", IP:", z, StringComparison.Ordinal);
				string page2 = line.Substring(z + 2, y2 - z - 2);
				return new LogEvent(line.Substring(0, x2), page2, _server, start: false);
			}
		}
		if (logLevel.Equals("PT-ALERT"))
		{
			int x3 = line.IndexOf(".RequestLoggerModule - ", StringComparison.Ordinal);
			if (x3 > -1)
			{
				int z2 = line.IndexOf("= The requested page took more than 10 seconds to render.", x3, StringComparison.Ordinal);
				string exceptionHash = line.Substring(x3 + 23, z2 - x3 - 22);
				return new LogEvent(line.Substring(0, x3), exceptionHash, _server);
			}
		}
		return null;
	}

	private string GetLogLevel(string line)
	{
		int x = line.IndexOf("] ", StringComparison.Ordinal);
		if (x == -1 || x > 40)
		{
			return null;
		}
		int y = line.IndexOf(" [", x, StringComparison.Ordinal);
		if (y == -1 || y > x + 20)
		{
			return null;
		}
		return line.Substring(x + 2, y - x - 2).Trim();
	}

	private int WriteToDbRabbitMqSendIssue(LogEvent logEvent)
	{
		ISqlCommandParameter[] sqlParameters = new ISqlCommandParameter[6]
		{
			new DateTimeParameter("Timestamp", logEvent.Timestamp),
			new Int32Parameter("SendTime", logEvent.SendTime),
			new StringParameter("SessionId", logEvent.SessionId),
			new StringParameter("RequestId", logEvent.RequestId),
			new StringParameter("Loglevel", logEvent.Level),
			new StringParameter("Server", logEvent.Server)
		};
		return ExecuteInsertStatementReturnRowCount("insert into _temp_RabbitMqSendIssue([timestamp], sendTime, sessionId, requestId, logLevel, [server]) values(@Timestamp, @SendTime, @SessionId, @RequestId, @LogLevel, @Server)", sqlParameters, logSql: true);
	}

	private int WriteToDbRequestStartEnd(LogEvent logEvent)
	{
		ISqlCommandParameter[] sqlParameters = new ISqlCommandParameter[8]
		{
			new DateTimeParameter("Timestamp", logEvent.Timestamp),
			new StringParameter("Url", logEvent.Url),
			new BooleanParameter("Start", logEvent.Start),
			new StringParameter("ExceptionHash", logEvent.ExceptionHash),
			new StringParameter("SessionId", logEvent.SessionId),
			new StringParameter("RequestId", logEvent.RequestId),
			new StringParameter("Loglevel", logEvent.Level),
			new StringParameter("Server", logEvent.Server)
		};
		return ExecuteInsertStatementReturnRowCount("insert into _temp_RequestStartEnd([timestamp], url, [start], exceptionHash, sessionId, requestId, logLevel, [server]) values(@Timestamp, @Url, @Start, @ExceptionHash, @SessionId, @RequestId, @LogLevel, @Server)", sqlParameters, logSql: true);
	}
}
