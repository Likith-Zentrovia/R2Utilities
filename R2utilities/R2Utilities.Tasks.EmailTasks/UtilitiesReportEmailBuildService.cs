using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Email.EmailBuilders;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.EmailTasks;

public class UtilitiesReportEmailBuildService : InternalUtilitiesEmailBuildService
{
	private readonly string _errorLabel = "<span style=\"color:red;font-weight:bold\">ERROR</span>";

	private readonly ILog<EmailBuildBaseService> _log;

	private int _totalErrors;

	private int _totalProcesses;

	private int _totalSteps;

	public UtilitiesReportEmailBuildService(ILog<EmailBuildBaseService> log, IEmailSettings emailSettings, IContentSettings contentSettings)
		: base(log, emailSettings, contentSettings)
	{
		_log = log;
	}

	public new void InitEmailTemplates()
	{
		SetTemplates("UtilityReport_Body.html", "UtilityReport_Item.html", includeUnsubscribe: false, "UtilityReport_Item_Step.html");
	}

	public EmailMessage BuildUtilitiesReportEmail(IEnumerable<TaskResult> taskResults, DateTime startDate, DateTime endDate, string[] emails)
	{
		string messageHtml = GetUtilityReportEmailHtml(taskResults, startDate, endDate);
		return string.IsNullOrWhiteSpace(messageHtml) ? null : BuildEmailMessage(emails, "R2 Library Utility Report", messageHtml);
	}

	private string GetUtilityReportEmailHtml(IEnumerable<TaskResult> taskResults, DateTime startDate, DateTime endDate)
	{
		string itemHtml = BuildUtilityItemsHtml(taskResults);
		string bodyHtml = BuildBodyHtml().Replace("{Utility_StartDate}", startDate.ToString()).Replace("{Utility_EndDate}", endDate.ToString()).Replace("{Utility_Processes_Count}", _totalProcesses.ToString())
			.Replace("{Utility_Step_Count}", _totalSteps.ToString())
			.Replace("{Utility_Error_Count}", _totalErrors.ToString())
			.Replace("{Utility_Error_Count_Style}", GetErrorCountStyle(_totalErrors))
			.Replace("{Task_Items}", itemHtml);
		return BuildMainHtml("R2Utilities Processes", bodyHtml, null);
	}

	private string GetErrorCountStyle(int errorCount)
	{
		return (errorCount > 0) ? "background-color:#ff0000; color: white" : "background-color:#00FF00; color: white";
	}

	private string BuildUtilityItemsHtml(IEnumerable<TaskResult> taskResults)
	{
		StringBuilder itemBuilder = new StringBuilder();
		foreach (TaskResult taskResult in taskResults)
		{
			_totalProcesses++;
			StringBuilder stepBuilder = new StringBuilder();
			if (taskResult.Steps != null)
			{
				foreach (TaskResultStep taskResultStep in taskResult.Steps)
				{
					_totalSteps++;
					if (!taskResultStep.CompletedSuccessfully)
					{
						_totalErrors++;
					}
					if (base.SubItemTemplate == null)
					{
						_log.ErrorFormat("ERROR BUILDING EMAIL MESSAGE ==> SubItemTemplate is null - {0}", taskResultStep.ToString());
						stepBuilder.Append("ERROR BUILDING EMAIL MESSAGE ==> SubItemTemplate is null");
					}
					else if (!taskResultStep.CompletedSuccessfully)
					{
						stepBuilder.Append(base.SubItemTemplate.Replace("{Step_Detail}", $"{taskResultStep.Id} - {taskResultStep.Name}").Replace("{Step_Status}", _errorLabel));
					}
				}
			}
			if (base.ItemTemplate != null)
			{
				string successCount = ((taskResult.Steps == null || !taskResult.Steps.Any()) ? "success (0/1)" : $"success ({taskResult.Steps.Count((TaskResultStep x) => x.CompletedSuccessfully)}/{taskResult.Steps.Count})");
				itemBuilder.Append(base.ItemTemplate.Replace("{Task_Id}", taskResult.Id.ToString()).Replace("{Task_Detail}", $"{taskResult.Name} started at {taskResult.StartTime} {successCount}").Replace("{Task_Status}", taskResult.CompletedSuccessfully ? "OK" : _errorLabel)
					.Replace("{Task_Steps}", stepBuilder.ToString()));
			}
			else
			{
				_log.ErrorFormat("ERROR BUILDING EMAIL MESSAGE ==> ItemTemplate is null - {0}", taskResult.ToString());
				stepBuilder.Append("ERROR BUILDING EMAIL MESSAGE ==> ItemTemplate is null");
			}
		}
		return itemBuilder.ToString();
	}
}
