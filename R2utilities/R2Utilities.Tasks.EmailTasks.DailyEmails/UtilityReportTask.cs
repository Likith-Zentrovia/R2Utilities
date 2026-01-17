using System;
using System.Collections.Generic;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class UtilityReportTask : EmailTaskBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly TaskResultDataService _taskResultDataService;

	private readonly UtilitiesReportEmailBuildService _utilitiesReportEmailBuildService;

	public UtilityReportTask(IR2UtilitiesSettings r2UtilitiesSettings, UtilitiesReportEmailBuildService utilitiesReportEmailBuildService)
		: base("UtilityReportTask", "-UtilityReportTask", "61", TaskGroup.InternalSystemEmails, "Sends the utilities report emails", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_utilitiesReportEmailBuildService = utilitiesReportEmailBuildService;
		_taskResultDataService = new TaskResultDataService();
	}

	public override void Run()
	{
		base.TaskResult.Information = "Utility Report Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "UtilityReportTask Run",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			if (base.StartDate == DateTime.MinValue || base.EndDate == DateTime.MinValue)
			{
				throw new Exception("Please set paramters: -start or -start and -end (Examples -start=1.0:0:0 or -start=12/15/2016 -end=12/18/2016)");
			}
			List<TaskResult> taskResults = _taskResultDataService.GetTaskResultsFromDate(base.StartDate, base.EndDate, base.TaskResult.Id);
			bool success = ProcessUtilityReport(taskResults, base.StartDate, base.EndDate);
			step.CompletedSuccessfully = success;
			step.Results = "Utility Report Task completed successfully";
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

	private bool ProcessUtilityReport(IEnumerable<TaskResult> taskResults, DateTime startDate, DateTime endDate)
	{
		base.TaskResult.Information = "Process Utility Report";
		TaskResultStep step = new TaskResultStep
		{
			Name = "ProcessUtilityReport (Build Email)",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			_utilitiesReportEmailBuildService.InitEmailTemplates();
			EmailMessage emailMessage = _utilitiesReportEmailBuildService.BuildUtilitiesReportEmail(taskResults, startDate, endDate, base.EmailSettings.TaskEmailConfig.ToAddresses.ToArray());
			bool success = false;
			if (emailMessage != null)
			{
				AddTaskCcToEmailMessage(emailMessage);
				AddTaskBccToEmailMessage(emailMessage);
				success = EmailDeliveryService.SendTaskReportEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName);
			}
			step.CompletedSuccessfully = success;
			step.Results = "Process Utility Report completed successfully";
			return success;
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
