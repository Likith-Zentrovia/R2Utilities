using System;
using System.Collections.Generic;
using System.Linq;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess.LogEvents;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.EmailTasks;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class LogEventsReportTask : EmailTaskBase, ITask
{
	private readonly ILog<LogEventsReportTask> _log;

	private readonly LogEventsReportEmailBuildService _logEventsReportEmailBuildService;

	private readonly LogEventsService _logEventsService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private string _file;

	public LogEventsReportTask(IR2UtilitiesSettings r2UtilitiesSettings, LogEventsService logEventsService, ILog<LogEventsReportTask> log, LogEventsReportEmailBuildService logEventsReportEmailBuildService)
		: base("LogEventsReportTask", "-LogEventsReportTask", "30", TaskGroup.DiagnosticsMaintenance, "Task will Send Email on LogEvents based on Period Provided", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_logEventsService = logEventsService;
		_log = log;
		_logEventsReportEmailBuildService = logEventsReportEmailBuildService;
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_file = GetArgument("file");
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will send an email with all LogEvents based on the configuration file.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "LogEventsReportTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			_logEventsReportEmailBuildService.InitEmailTemplates();
			_log.Info("Templates Set");
			int emailsSent = 0;
			List<LogEventsConfiguration> logEventsConfigurations = _logEventsService.GetLogEventsConfigruation(_file);
			_log.Info($"{logEventsConfigurations.Count} LogEventsConfigurations Found in file: {_file}");
			foreach (LogEventsConfiguration logEventsConfiguration in logEventsConfigurations)
			{
				_log.Info($"{logEventsConfiguration.ReportConfigurations.Count} ReportConfigurations Found for Table {logEventsConfiguration.TableName}");
				foreach (ReportConfiguration reportConfiguration in logEventsConfiguration.ReportConfigurations)
				{
					List<R2Utilities.DataAccess.LogEvents.LogEvent> logEvents = _logEventsService.GetLogEvents(reportConfiguration, logEventsConfiguration.TableName, base.StartDate, base.EndDate);
					reportConfiguration.TotalLogEvents = logEvents.Count;
					if (logEvents.Count > reportConfiguration.ReportedItems)
					{
						logEvents = logEvents.Take(reportConfiguration.ReportedItems).ToList();
					}
					reportConfiguration.LogEvents = logEvents;
					_log.Info($"{reportConfiguration.TotalLogEvents} LogEvents found for {reportConfiguration.Name}");
				}
				EmailMessage emailMessage = _logEventsReportEmailBuildService.BuildLogEventsReportReportEmail(logEventsConfiguration.TableName, logEventsConfiguration.ReportConfigurations, base.StartDate, base.EndDate, base.EmailSettings.TaskEmailConfig.ToAddresses.ToArray());
				_log.Info("Email Built");
				if (emailMessage != null)
				{
					AddTaskCcToEmailMessage(emailMessage);
					AddTaskBccToEmailMessage(emailMessage);
					bool success = EmailDeliveryService.SendTaskReportEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName);
					_log.Info("Email Sent " + (success ? "Successfully" : "Failed"));
					if (success)
					{
						emailsSent++;
					}
				}
			}
			step.Results = $"Log Event Emails sent: {emailsSent}";
			step.CompletedSuccessfully = emailsSent == logEventsConfigurations.Count;
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
}
