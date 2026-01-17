using System;
using System.Collections.Generic;
using System.Linq;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Authentication;
using R2V2.Core.Email;
using R2V2.Core.Institution;
using R2V2.Core.Reports;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.ReportTasks;

public class SavedReportsTask : EmailTaskBase
{
	private readonly SavedReportsEmailBuildService _emailBuildService;

	private readonly EmailTaskService _emailTaskService;

	private readonly IQueryable<Institution> _institutions;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly IReportService _reportService;

	private readonly ReportServiceBase _reportServiceBase;

	private readonly IQueryable<Resource> _resources;

	private readonly IQueryable<User> _users;

	private ReportFrequency _frequency = ReportFrequency.Weekly;

	private IEnumerable<IResource> Resources;

	public SavedReportsTask(IQueryable<Institution> institutions, IReportService reportService, IR2UtilitiesSettings r2UtilitiesSettings, SavedReportsEmailBuildService emailBuildService, IQueryable<Resource> resources, IQueryable<User> users, EmailTaskService emailTaskService, ReportServiceBase reportServiceBase)
		: base("SavedReportsTask", "-SavedReportsTask", "39", TaskGroup.CustomerEmails, "Sends saved reports emails to customers (IAs), -frequency=Weekly|BiWeekly|Monthly", enabled: true)
	{
		_institutions = institutions;
		_reportService = reportService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_emailBuildService = emailBuildService;
		_resources = resources;
		_users = users;
		_emailTaskService = emailTaskService;
		_reportServiceBase = reportServiceBase;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		_frequency = (ReportFrequency)Enum.Parse(typeof(ReportFrequency), GetArgument("frequency"));
		TaskResultStep step = new TaskResultStep
		{
			Name = $"SavedReportsTask - {_frequency}",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		base.TaskResult.Information = $"Frequency = {_frequency}";
		try
		{
			List<SavedReport> reportsToRun = _reportService.GetSavedReports(_frequency);
			int appUserReportCount = 0;
			int resourceUserReportCount = 0;
			int invalidReportTypeCount = 0;
			int exceptionCount = 0;
			int emailCount = 0;
			int emailCountTotal = reportsToRun.Count();
			foreach (SavedReport savedReport in reportsToRun)
			{
				emailCount++;
				R2UtilitiesBase.Log.InfoFormat("Processing {0} of {1}, Type: {2}, Email: {3}, UserId: {4}", emailCount, emailCountTotal, savedReport.Type, savedReport.Email, savedReport.UserId);
				if (savedReport.LastUpdate.HasValue)
				{
					if (savedReport.Frequency == 30)
					{
						if (savedReport.LastUpdate.GetValueOrDefault().AddMonths(1).AddDays(-4.0)
							.Date > DateTime.Now.Date)
						{
							R2UtilitiesBase.Log.InfoFormat(" -> Ignored, LastUpdate: {0}", savedReport.LastUpdate);
							continue;
						}
					}
					else if (savedReport.LastUpdate.GetValueOrDefault().AddDays(savedReport.Frequency).AddDays(-2.0)
						.Date > DateTime.Now.Date)
					{
						R2UtilitiesBase.Log.InfoFormat(" -> Ignored, LastUpdate: {0}", savedReport.LastUpdate);
						continue;
					}
				}
				try
				{
					switch (savedReport.Type)
					{
					case 1:
						RunSavedApplicationUsage(savedReport);
						appUserReportCount++;
						R2UtilitiesBase.Log.Info(" -> Application Usage Report Sent");
						break;
					case 2:
						RunSavedResourceUsage(savedReport);
						resourceUserReportCount++;
						R2UtilitiesBase.Log.Info(" -> Resource Usage Report Sent");
						break;
					default:
						R2UtilitiesBase.Log.ErrorFormat(" -> Don't know how to handle this Report : {0}", savedReport);
						invalidReportTypeCount++;
						break;
					}
				}
				catch (Exception exception)
				{
					R2UtilitiesBase.Log.Error($" -> Error processing SavedReport : {savedReport}", exception);
					exceptionCount++;
				}
			}
			step.CompletedSuccessfully = invalidReportTypeCount == 0 && exceptionCount == 0;
			step.Results = $"{appUserReportCount} application usage report emails sent, {resourceUserReportCount} resource usage report emails sent, {invalidReportTypeCount} invalid report types, {exceptionCount} exceptions.";
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

	private void RunSavedApplicationUsage(SavedReport savedReport)
	{
		_reportServiceBase.InitBase(savedReport.InstitutionId, savedReport.Id);
		Institution institution = _institutions.FirstOrDefault((Institution x) => x.Id == _reportServiceBase.ReportRequest.InstitutionId);
		ApplicationReportCounts applicationReportCounts = _reportService.GetApplicationReportCounts(_reportServiceBase.ReportRequest);
		User user = _users.FirstOrDefault((User x) => x.Id == savedReport.UserId);
		EmailMessage emailMessage = _emailBuildService.BuildApplicationUsageReportEmail(applicationReportCounts, savedReport, _reportServiceBase.ReportRequest, institution, user);
		if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName) && !_r2UtilitiesSettings.EmailTestMode)
		{
			_reportService.SaveSavedReport(_reportServiceBase.ReportRequest.DateRangeEnd, savedReport.Id);
		}
	}

	private void RunSavedResourceUsage(SavedReport savedReport)
	{
		_reportServiceBase.InitBase(savedReport.InstitutionId, savedReport.Id);
		Institution institution = _institutions.FirstOrDefault((Institution x) => x.Id == _reportServiceBase.ReportRequest.InstitutionId);
		if (Resources == null)
		{
			Resources = _resources.ToList();
		}
		List<ResourceReportItem> items = _reportService.GetResourceReportItems(_reportServiceBase.ReportRequest, Resources.ToList());
		if (items.Count != 0)
		{
			_emailBuildService.InitEmailTemplates();
			EmailMessage emailMessage = _emailBuildService.BuildResourceUsageReportEmail(items, savedReport, _reportServiceBase.ReportRequest, institution);
			if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName) && !_r2UtilitiesSettings.EmailTestMode)
			{
				_emailTaskService.UpdateSavedReportLastUpdate(_reportServiceBase.ReportRequest.DateRangeEnd, savedReport.Id);
			}
		}
	}
}
