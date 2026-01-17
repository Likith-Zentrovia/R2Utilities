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
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.EmailTasks.WeeklyEmails;

public class NewEditionTask : EmailTaskBase
{
	private readonly IEmailSettings _emailSettings;

	private readonly EmailTaskService _emailTaskService;

	private readonly NewEditionResourceEmailBuildService _newEditionResourceEmailBuildService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public NewEditionTask(EmailTaskService emailTaskService, IR2UtilitiesSettings r2UtilitiesSettings, IEmailSettings emailSettings, NewEditionResourceEmailBuildService newEditionResourceEmailBuildService)
		: base("NewEditionTask", "-SendNewEditionEmails", "42", TaskGroup.CustomerEmails, "Sends new edition emails to customers", enabled: true)
	{
		_newEditionResourceEmailBuildService = newEditionResourceEmailBuildService;
		_emailTaskService = emailTaskService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_emailSettings = emailSettings;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "New Edition Email Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "NewEditionTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		_newEditionResourceEmailBuildService.SetTemplates(isPdaEdition: false);
		int institutionCount = 0;
		int successCount = 0;
		int failCount = 0;
		int userCount = 0;
		StringBuilder failureEmailAddress = new StringBuilder();
		List<int> institutionIdsOfCartsUpdated = new List<int>();
		try
		{
			List<User> users = _emailTaskService.GetUsersForNewEditionEmail();
			R2UtilitiesBase.Log.InfoFormat("Processing {0} users", users.Count());
			Dictionary<int, List<User>> institutionIdAndUsers = new Dictionary<int, List<User>>();
			foreach (User user in users)
			{
				if (institutionIdAndUsers.ContainsKey(user.InstitutionId.GetValueOrDefault()))
				{
					institutionIdAndUsers[user.InstitutionId.GetValueOrDefault()].Add(user);
					continue;
				}
				institutionIdAndUsers.Add(user.InstitutionId.GetValueOrDefault(), new List<User> { user });
			}
			foreach (KeyValuePair<int, List<User>> institutionIdAndUser in institutionIdAndUsers)
			{
				institutionCount++;
				int institutionUserCount = institutionIdAndUser.Value.Count;
				R2UtilitiesBase.Log.InfoFormat("Processing {0} of {1} institutions - Id: {2}, users: {3}", institutionCount, institutionIdAndUsers.Count(), institutionIdAndUser.Key, institutionUserCount);
				List<IResource> resources = _emailTaskService.GetNewEditionResourceEmailResources(institutionIdAndUser.Key);
				if (resources == null || !resources.Any())
				{
					R2UtilitiesBase.Log.InfoFormat("No New Edition Resources Found for Institution {0}", institutionIdAndUser.Key);
					continue;
				}
				int institutionUserCounter = 0;
				foreach (User user2 in institutionIdAndUser.Value)
				{
					userCount++;
					institutionUserCounter++;
					R2UtilitiesBase.Log.InfoFormat("Processing {0} of {1} users - Id: {2}, username: {3}, email: {4}", institutionUserCounter, institutionUserCount, user2.Id, user2.UserName, user2.Email);
					EmailMessage emailMessage = _newEditionResourceEmailBuildService.BuildNewEditionResourceEmail(resources, user2);
					if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
					{
						if (!institutionIdsOfCartsUpdated.Contains(user2.InstitutionId.GetValueOrDefault()) && user2.IsInstitutionAdmin())
						{
							institutionIdsOfCartsUpdated.Add(user2.InstitutionId.GetValueOrDefault());
							_emailTaskService.AddNewResourcesToCart(user2.InstitutionId.GetValueOrDefault(), resources);
						}
						successCount++;
					}
					else
					{
						failCount++;
						failureEmailAddress.AppendFormat("FailToSend: [InstitutionId:{0} | UserId:{1} | UserEmail: {2}] <br/>", user2.Institution.Id, user2.Id, user2.Email);
					}
				}
			}
			R2UtilitiesBase.Log.DebugFormat("Is in Test Mode? : {0}", !_emailSettings.SendToCustomers);
			if (_emailSettings.SendToCustomers)
			{
				_emailTaskService.UpdateNewEditionResourceEmailResources();
			}
			step.CompletedSuccessfully = failCount == 0;
			step.Results = $"{userCount} users processed,  {successCount} new editition report emails sent, {failCount} emails failed to send. Failed Emails information: {failureEmailAddress}";
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
