using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Authentication;
using R2V2.Core.Email;
using R2V2.Core.Recommendations;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class FacultyRecommendationsTask : EmailTaskBase
{
	private readonly RecommendationEmailBuildService _emailBuildService;

	private readonly EmailTaskService _emailTaskService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public FacultyRecommendationsTask(RecommendationEmailBuildService emailBuildService, IR2UtilitiesSettings r2UtilitiesSettings, EmailTaskService emailTaskService)
		: base("FacultyRecommendationsTask", "-FacultyRecommendationsTask", "49", TaskGroup.CustomerEmails, "Task sends expert reviewer/faculty recommendations", enabled: true)
	{
		_emailBuildService = emailBuildService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_emailTaskService = emailTaskService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "ExpertReviewer Recommendations Task Run";
		TaskResultStep step = new TaskResultStep
		{
			Name = "FacultyRecommendationsTask Run",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int successEmails = 0;
		int failureEmails = 0;
		StringBuilder failureEmailAddress = new StringBuilder();
		try
		{
			_emailBuildService.InitEmailTemplates();
			IEnumerable<int> institutionIds = _emailTaskService.GetInstitutionIdsForFacultyRecommentations();
			foreach (int institutionId in institutionIds)
			{
				int id = institutionId;
				List<Recommendation> recommendations = _emailTaskService.GetRecommendations(id);
				if (!recommendations.Any())
				{
					continue;
				}
				List<User> recommendationUsers = _emailTaskService.GetFacultyRecommendationUsers(institutionId);
				if (!recommendationUsers.Any())
				{
					continue;
				}
				User user = _emailTaskService.GetInstitutionAdministrator(institutionId);
				string[] emails = recommendationUsers.Select((User x) => x.Email).ToArray();
				EmailMessage emailMessage = _emailBuildService.BuildRecommendationEmail(recommendations, user, emails);
				if (emailMessage != null)
				{
					if (EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
					{
						successEmails++;
						if (!_r2UtilitiesSettings.EmailTestMode)
						{
							_emailTaskService.SetRecommendationsAlertSentDate(recommendations.Select((Recommendation x) => x.Id).ToArray());
						}
					}
					else
					{
						failureEmails++;
						failureEmailAddress.AppendFormat("[InstitutionId:{0} | UserId:{1} | UserEmail: {2} | Failed To Send] <br/>", user.Institution.Id, user.Id, user.Email);
					}
				}
				else
				{
					failureEmails++;
					failureEmailAddress.AppendFormat("[InstitutionId:{0} | UserId:{1} | UserEmail: {2} | Failed To Build] <br/>", user.Institution.Id, user.Id, user.Email);
				}
			}
			step.CompletedSuccessfully = failureEmails == 0;
			step.Results = string.Format("{0} recommendation emails sent, {1} recommendation emails failed to send/build. Failed Emails information: {2}", successEmails, failureEmails, (failureEmailAddress.Length == 0) ? "There were no failures" : failureEmailAddress.ToString());
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
