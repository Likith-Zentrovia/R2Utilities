using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Authentication;
using R2V2.Core.CollectionManagement;
using R2V2.Core.CollectionManagement.PatronDrivenAcquisition;
using R2V2.Core.Email;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.EmailTasks.WeeklyEmails;

public class PdaHistoryTask : EmailTaskBase
{
	private readonly EmailTaskService _emailTaskService;

	private readonly StringBuilder _failureEmailAddress = new StringBuilder();

	private readonly PdaEmailBuildService _pdaEmailBuildService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private int _failCount;

	private int _successCount;

	public PdaHistoryTask(EmailTaskService emailTaskService, PdaEmailBuildService pdaEmailBuildService, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("PdaHistoryTask", "-PdaHistoryTask", "48", TaskGroup.CustomerEmails, "Sends PDA History Excel Reports.", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_pdaEmailBuildService = pdaEmailBuildService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "PDA History Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "PdaHistoryTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			_pdaEmailBuildService.InitEmailTemplatesForPdaHistory();
			_successCount = 0;
			_failCount = 0;
			List<UserIdAndInstitutionId> userIdsAndInstitutionIds = _emailTaskService.GetPdaHistoryUserIds();
			IEnumerable<int> institutionIds = userIdsAndInstitutionIds.Select((UserIdAndInstitutionId x) => x.InstitutionId).Distinct();
			List<PdaHistoryReport> pdaHistoryReports = new List<PdaHistoryReport>();
			foreach (int institutionId in institutionIds)
			{
				List<PdaHistoryResource> pdaResources = _emailTaskService.GetPdaHistoryResources(institutionId).ToList();
				PdaHistoryReport report = _emailTaskService.GetPdaHistoryReport(institutionId, pdaResources);
				if (report != null)
				{
					pdaHistoryReports.Add(report);
				}
			}
			foreach (UserIdAndInstitutionId userIdAndInstitutionId in userIdsAndInstitutionIds)
			{
				PdaHistoryReport report2 = pdaHistoryReports.FirstOrDefault((PdaHistoryReport x) => x.InstitutionId == userIdAndInstitutionId.InstitutionId);
				if (report2 != null)
				{
					ProcessUser(userIdAndInstitutionId.UserId, report2);
				}
			}
			step.CompletedSuccessfully = true;
			string results = new StringBuilder().AppendFormat("<div>{0} PDA history reports sent</div>", _successCount).AppendLine().AppendFormat("<div>{0} PDA history reports failed</div>", _failCount)
				.AppendLine()
				.AppendFormat("<div>Emails that failed: {0}</div>", _failureEmailAddress)
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

	public void ProcessUser(int userId, PdaHistoryReport pdaHistoryReport)
	{
		User user = _emailTaskService.GetUser(userId);
		List<User> territoryusers = new List<User>();
		if (user.Institution != null && user.Institution.Territory != null)
		{
			territoryusers = _emailTaskService.GetTerritoryOwners(user.Institution.Territory.Id);
		}
		string[] userArray = (territoryusers.Any() ? territoryusers.Select((User x) => x.Email).ToArray() : null);
		EmailMessage emailMessage = _pdaEmailBuildService.BuildPdaHistoryEmail(pdaHistoryReport, user, userArray);
		if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
		{
			_successCount++;
			return;
		}
		_failCount++;
		_failureEmailAddress.AppendFormat("FailToSend: [InstitutionId:{0} | UserId:{1} | UserEmail: {2}] <br/>", (user.Institution != null) ? user.Institution.Id : 0, user.Id, user.Email);
	}
}
