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

public class DoodyUpdateResourceTask : EmailTaskBase
{
	private readonly DctUpdateResourceEmailBuildService _dctUpdateResourceEmailBuildService;

	private readonly EmailTaskService _emailTaskService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public DoodyUpdateResourceTask(EmailTaskService emailTaskService, DctUpdateResourceEmailBuildService dctUpdateResourceEmailBuildService, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("DoodyUpdateResourceTask", "-DoodyUpdateResourceEmailTask", "52", TaskGroup.CustomerEmails, "Sends Doody update email to ???", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_dctUpdateResourceEmailBuildService = dctUpdateResourceEmailBuildService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		_dctUpdateResourceEmailBuildService.SetTemplates();
		base.TaskResult.Information = "Doody Update Email Resource Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "DoodyUpdateResourceTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int medicalEmailCount = 0;
		int nursingEmailCount = 0;
		int alliedHealthEmailCount = 0;
		int failCount = 0;
		try
		{
			List<IResource> medicineResources = _emailTaskService.GetDctUpdateResourcesForEmail(1);
			List<IResource> nursingResources = _emailTaskService.GetDctUpdateResourcesForEmail(2);
			List<IResource> alliedHealthResources = _emailTaskService.GetDctUpdateResourcesForEmail(3);
			if (medicineResources.Any())
			{
				List<User> users = _emailTaskService.GetUsersForDctUpdateEmails(1);
				foreach (User user in users)
				{
					EmailMessage emailMessage = _dctUpdateResourceEmailBuildService.BuildDctUpdateEmail(medicineResources, user, "Medicine");
					if (emailMessage != null)
					{
						if (EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
						{
							medicalEmailCount++;
							break;
						}
						failCount++;
					}
				}
			}
			if (nursingResources.Any())
			{
				List<User> users2 = _emailTaskService.GetUsersForDctUpdateEmails(2);
				foreach (User user2 in users2)
				{
					EmailMessage emailMessage2 = _dctUpdateResourceEmailBuildService.BuildDctUpdateEmail(nursingResources, user2, "Nursing");
					if (emailMessage2 != null)
					{
						if (EmailDeliveryService.SendCustomerTaskEmail(emailMessage2, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
						{
							nursingEmailCount++;
							break;
						}
						failCount++;
					}
				}
			}
			if (alliedHealthResources.Any())
			{
				List<User> users3 = _emailTaskService.GetUsersForDctUpdateEmails(3);
				foreach (User user3 in users3)
				{
					EmailMessage emailMessage3 = _dctUpdateResourceEmailBuildService.BuildDctUpdateEmail(alliedHealthResources, user3, "Allied Health");
					if (emailMessage3 != null)
					{
						if (EmailDeliveryService.SendCustomerTaskEmail(emailMessage3, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
						{
							alliedHealthEmailCount++;
							break;
						}
						failCount++;
					}
				}
			}
			string results = new StringBuilder().AppendFormat("DCT Medicine Emails Sent: {0} <br />", medicalEmailCount).AppendLine().AppendFormat("DCT Nursing Emails Sent: {0} <br />", nursingEmailCount)
				.AppendLine()
				.AppendFormat("DCT Allied Health Emails Sent: {0} <br />", alliedHealthEmailCount)
				.AppendLine()
				.AppendFormat("FAILED Emails: {0} <br />", failCount)
				.AppendLine()
				.ToString();
			step.CompletedSuccessfully = failCount == 0;
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
