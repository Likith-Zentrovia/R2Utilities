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

public class NewResourceTask : EmailTaskBase
{
	private readonly EmailTaskService _emailTaskService;

	private readonly NewResourceEmailBuildService _newResourceEmailBuildService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public NewResourceTask(EmailTaskService emailTaskService, IR2UtilitiesSettings r2UtilitiesSettings, NewResourceEmailBuildService newResourceEmailBuildService)
		: base("NewResourceTask", "-SendNewResourceEmails", "40", TaskGroup.CustomerEmails, "Send new resource email to customers", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_newResourceEmailBuildService = newResourceEmailBuildService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "New Resource Email Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "NewResourceTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int userCount = 0;
		int successEmails = 0;
		int failureEmails = 0;
		StringBuilder failureEmailAddress = new StringBuilder();
		try
		{
			List<User> users = _emailTaskService.GetUsersForNewResourceEmail();
			List<IResource> newResources = _emailTaskService.GetNewResourceEmailResources();
			if (newResources != null && newResources.Any())
			{
				_newResourceEmailBuildService.SetNewResourceItemHtml(newResources, "REPLACE");
				foreach (User user in users)
				{
					userCount++;
					R2UtilitiesBase.Log.InfoFormat("Processing {0} of {1} users - Id: {2}, username: {3}, email: {4}", userCount, users.Count(), user.Id, user.UserName, user.Email);
					EmailMessage emailMessage = _newResourceEmailBuildService.BuildNewResourceEmail(user);
					if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
					{
						successEmails++;
					}
					else
					{
						failureEmails++;
					}
				}
				if (!_r2UtilitiesSettings.EmailTestMode)
				{
					_emailTaskService.UpdateNewResourceEmailResources();
				}
			}
			step.CompletedSuccessfully = failureEmails == 0;
			step.Results = $"{userCount} users processed,  {successEmails} new resource emails sent, {failureEmails} emails failed to send. Failed Emails information: {failureEmailAddress}";
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
