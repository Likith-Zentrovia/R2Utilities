using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Authentication;
using R2V2.Core.Email;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.EmailTasks.WeeklyEmails;

public class ArchivedResourceTask : EmailTaskBase
{
	private readonly ArchivedResourceEmailBuildService _archivedResourceEmailBuildService;

	private readonly EmailTaskService _emailTaskService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private int _failCount;

	private int _successCount;

	public ArchivedResourceTask(EmailTaskService emailTaskService, IR2UtilitiesSettings r2UtilitiesSettings, ArchivedResourceEmailBuildService archivedResourceEmailBuildService)
		: base("ArchivedResourceTask", "-ArchivedResourceTask", "50", TaskGroup.CustomerEmails, "Archives resources and then send out an customer emails", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_archivedResourceEmailBuildService = archivedResourceEmailBuildService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "Archived Resource Email Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "ArchivedResourceTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			_successCount = 0;
			_failCount = 0;
			List<int> userIds = _emailTaskService.GetArchivedEmailUserIds();
			foreach (int userId in userIds)
			{
				User user = _emailTaskService.GetUser(userId);
				List<int> archivedResourceIds = _emailTaskService.GetArchivedEmailResourceIds(userId).ToList();
				List<IResource> archivedResources = _emailTaskService.GetResources(archivedResourceIds);
				if (archivedResources != null && archivedResources.Any())
				{
					EmailMessage emailMessage = _archivedResourceEmailBuildService.BuildArchivedResourceEmail(user, archivedResources);
					if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
					{
						_successCount++;
					}
					else
					{
						_failCount++;
					}
				}
			}
			if (!_r2UtilitiesSettings.EmailTestMode && _archivedResourceEmailBuildService.ProcessedArchivedResources != null)
			{
				_emailTaskService.UpdateArchivedResourceEmailResources(_archivedResourceEmailBuildService.ProcessedArchivedResources);
			}
			step.CompletedSuccessfully = true;
			string results = new StringBuilder().AppendFormat("<div>{0} Archived Resource Emails sent</div>", _successCount).AppendLine().AppendFormat("<div>{0} Archived Resource Emails failed</div>", _failCount)
				.AppendLine()
				.ToString();
			step.Results = results;
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
