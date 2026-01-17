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

public class PurchasedForthcomingTask : EmailTaskBase
{
	private readonly EmailTaskService _emailTaskService;

	private readonly ForthcomingResourceEmailBuildService _forthcomingResourceEmailBuildService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public PurchasedForthcomingTask(EmailTaskService emailTaskService, IR2UtilitiesSettings r2UtilitiesSettings, ForthcomingResourceEmailBuildService forthcomingResourceEmailBuildService)
		: base("PurchasedForthcomingTask", "-SendPurchasedForthcomingEmails", "41", TaskGroup.CustomerEmails, "Sends ?? email to customers (IAs)", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_forthcomingResourceEmailBuildService = forthcomingResourceEmailBuildService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "Purchased Forthcoming Email Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "PurchasedForthcomingTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int userCount = 0;
		int successCount = 0;
		int failCount = 0;
		StringBuilder failureEmailAddress = new StringBuilder();
		try
		{
			List<User> users = _emailTaskService.GetUsersForPurchasedForthcomingEmail();
			foreach (User user in users)
			{
				userCount++;
				R2UtilitiesBase.Log.InfoFormat("Processing {0} of {1} users - Id: {2}, username: {3}, email: {4}", userCount, users.Count(), user.Id, user.UserName, user.Email);
				List<IResource> resources = _emailTaskService.GetPurchasedResourceEmailResources(user.Institution.Id);
				if (resources == null || !resources.Any())
				{
					R2UtilitiesBase.Log.Info("No Resources Found");
					continue;
				}
				EmailMessage emailMessage = _forthcomingResourceEmailBuildService.BuildForthcomingResourceEmail(resources, user);
				if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
				{
					successCount++;
					continue;
				}
				failCount++;
				failureEmailAddress.AppendFormat("FailToSend: [InstitutionId:{0} | UserId:{1} | UserEmail: {2}] <br/>", user.Institution.Id, user.Id, user.Email);
			}
			if (!_r2UtilitiesSettings.EmailTestMode)
			{
				_emailTaskService.UpdatePurchasedResourceEmailResources();
			}
			step.CompletedSuccessfully = failCount == 0;
			step.Results = $"{userCount} users processed,  {successCount} purchased forthcoming emails sent, {failCount} emails failed to send. Failed Emails information: {failureEmailAddress}";
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
