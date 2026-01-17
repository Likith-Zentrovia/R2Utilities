using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Authentication;
using R2V2.Core.Cms;
using R2V2.Core.Email;
using R2V2.Core.Institution;
using R2V2.Core.Recommendations;
using R2V2.Core.Reports;
using R2V2.Core.Resource;
using R2V2.Core.Resource.Discipline;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;

namespace R2Utilities.Tasks.EmailTasks;

public class InstitutionDashboardEmailTask : EmailTaskBase
{
	private readonly CmsService _cmsService;

	private readonly DashboardEmailBuildService _dashboardEmailBuildService;

	private readonly DashboardService _dashboardService;

	private readonly EmailTaskService _emailTaskService;

	private readonly ILog<InstitutionDashboardEmailTask> _log;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public InstitutionDashboardEmailTask(EmailTaskService emailTaskService, DashboardService dashboardService, ILog<InstitutionDashboardEmailTask> log, IR2UtilitiesSettings r2UtilitiesSettings, DashboardEmailBuildService dashboardEmailBuildService, CmsService cmsService)
		: base("InstitutionDashboardEmailTask", "-InstitutionStatisticsEmailTask", "51", TaskGroup.CustomerEmails, "Sends institutional dashboard emails to customers (IAs)", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_dashboardService = dashboardService;
		_log = log;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_dashboardEmailBuildService = dashboardEmailBuildService;
		_cmsService = cmsService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "Institution Dashboard Email Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "InstitutionDashboardEmailTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int emailBuildCount = 0;
		int emailFailBuildCount = 0;
		int emailSendCount = 0;
		int emailFailedCount = 0;
		StringBuilder failToBuildEmails = new StringBuilder();
		StringBuilder failToSendEmails = new StringBuilder();
		try
		{
			TaskResultDataService taskResultDataService = new TaskResultDataService();
			TaskResult taskResult = taskResultDataService.GetPreviousTaskResult("InstitutionStatisticsTask");
			DateTime dashboardDateRunDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
			DateTime firstOfMonth = new DateTime(dashboardDateRunDate.Year, dashboardDateRunDate.Month, 1);
			List<IResource> resources = _emailTaskService.GetResources();
			List<IFeaturedTitle> featuredTitles = _emailTaskService.GetFeaturedTitles(dashboardDateRunDate, 4);
			List<SpecialResource> specials = _emailTaskService.GetSpecials(dashboardDateRunDate, 4);
			List<Specialty> specialies = _emailTaskService.GetSpecialties();
			List<string> notes = (_r2UtilitiesSettings.OverRideDashboardEmailQuickNotes ? null : _cmsService.GetDashboardQuickNotes());
			DateTime dashboardDate = new DateTime(dashboardDateRunDate.Year, dashboardDateRunDate.Month, 1).AddMonths(-1);
			List<Institution> institutions = _emailTaskService.GetDashboardInstitutions();
			foreach (Institution institution in institutions)
			{
				List<User> users = _emailTaskService.GetDashboardUsers(institution.Id);
				if (users.Count <= 0)
				{
					continue;
				}
				List<Recommendation> recommendations = _emailTaskService.GetRecommendations(institution.Id, 4);
				InstitutionEmailStatistics stats = _dashboardService.GetInstitutionEmailStatistics(institution.Id, dashboardDate);
				stats.PopulateResources(resources);
				stats.PopulateFeaturedTitles(resources, (featuredTitles.Count > 4) ? featuredTitles.Take(4).ToList() : featuredTitles, institution.Discount);
				stats.PopulateRecommendations(resources, (recommendations.Count > 4) ? recommendations.Take(4).ToList() : recommendations);
				stats.PopulateSpecialResources(resources, (specials.Count > 4) ? specials.Take(4).ToList() : specials);
				if (!_r2UtilitiesSettings.OverRideDashboardEmailQuickNotes)
				{
					stats.QuickNotes = notes;
				}
				stats.PopulateSpecialtyIds(specialies);
				string emailBodyBase = _dashboardEmailBuildService.GetDashboardBodyBase(stats, institution);
				int userCount = 0;
				foreach (User user in users)
				{
					EmailMessage emailMessage = _dashboardEmailBuildService.BuildDashboardEmail(stats, user, emailBodyBase);
					emailMessage.IsHtml = true;
					if (emailMessage.Body != null)
					{
						emailBuildCount++;
						if (EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
						{
							emailSendCount++;
						}
						else
						{
							emailFailedCount++;
							failToSendEmails.AppendFormat("[UserId: {0} | Email: {1}]", user.Id, user.Email).AppendLine();
							failToSendEmails.Append("\r\n");
						}
					}
					else
					{
						emailFailBuildCount++;
						failToBuildEmails.Append(stats.ToDebugString(user)).AppendLine();
					}
					userCount++;
				}
				_log.InfoFormat("Institution : {0} || {1} emails sent", institution.Id, userCount);
			}
			StringBuilder resultsBuilder = new StringBuilder();
			resultsBuilder.AppendFormat("{0} sent", emailSendCount).AppendLine();
			resultsBuilder.AppendFormat("{0} built", emailBuildCount).AppendLine();
			resultsBuilder.AppendFormat("{0} FAIL to send", emailFailedCount).AppendLine();
			resultsBuilder.AppendFormat("{0} FAIL to build", emailFailBuildCount).AppendLine();
			resultsBuilder.AppendFormat("FAIL to send Information: {0}", failToSendEmails).AppendLine();
			resultsBuilder.AppendFormat("FAIL to build Information: {0}", failToBuildEmails).AppendLine();
			step.CompletedSuccessfully = true;
			step.Results = resultsBuilder.ToString();
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
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
