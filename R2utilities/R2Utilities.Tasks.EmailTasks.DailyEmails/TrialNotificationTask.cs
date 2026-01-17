using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Email.EmailBuilders;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Authentication;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.UnitOfWork;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class TrialNotificationTask : EmailTaskBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly TrialNotificationEmailBuildService _trialNotificationEmailBuildService;

	private readonly IUnitOfWork _unitOfWork;

	private readonly IQueryable<User> _users;

	public TrialNotificationTask(IR2UtilitiesSettings r2UtilitiesSettings, IQueryable<User> users, IUnitOfWork unitOfWork, TrialNotificationEmailBuildService trialNotificationEmailBuildService)
		: base("TrialNotificationTask", "-TrialNotificationTask", "45", TaskGroup.CustomerEmails, "Sends the trial notification emails to customers (IAs)", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_users = users;
		_unitOfWork = unitOfWork;
		_trialNotificationEmailBuildService = trialNotificationEmailBuildService;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		TaskResultStep step = new TaskResultStep
		{
			Name = "TrialNotificationTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			StringBuilder sbResults = new StringBuilder().Append(ProcessTrialNotification(TrialNotice.First)).Append(ProcessTrialNotification(TrialNotice.Second)).Append(ProcessTrialNotification(TrialNotice.Final))
				.Append(ProcessTrialNotification(TrialNotice.Extension));
			step.CompletedSuccessfully = true;
			step.Results = sbResults.ToString();
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

	public List<User> GetUsersForNotice(TrialNotice noticeDay)
	{
		return noticeDay switch
		{
			TrialNotice.First => _users.Where((User u) => u.Institution.Trial.EndDate.HasValue && u.Institution.Trial.EndDate.Value.Date == DateTime.Now.AddDays((int)noticeDay).Date && (!u.Institution.Trial.EmailWarningDate.HasValue || u.Institution.Trial.EmailWarningDate.Value.Date != DateTime.Now.Date) && (int)u.Role.Code == 1 && u.Institution.AccountStatusId == 2 && u.RecordStatus && (u.ExpirationDate == null || u.ExpirationDate > DateTime.Now)).ToList(), 
			TrialNotice.Second => _users.Where((User u) => u.Institution.Trial.EndDate.HasValue && u.Institution.Trial.EndDate.Value.Date == DateTime.Now.AddDays((int)noticeDay).Date && (!u.Institution.Trial.Email3DayWarningDate.HasValue || u.Institution.Trial.Email3DayWarningDate.Value.Date != DateTime.Now.Date) && (int)u.Role.Code == 1 && u.Institution.AccountStatusId == 2 && u.RecordStatus && (u.ExpirationDate == null || u.ExpirationDate > DateTime.Now)).ToList(), 
			TrialNotice.Final => _users.Where((User u) => u.Institution.Trial.EndDate.HasValue && u.Institution.Trial.EndDate.Value.Date == DateTime.Now.AddDays((int)noticeDay).Date && (!u.Institution.Trial.EmailFinalDate.HasValue || u.Institution.Trial.EmailFinalDate.Value.Date != DateTime.Now.Date) && (int)u.Role.Code == 1 && u.Institution.AccountStatusId == 2 && u.RecordStatus && (u.ExpirationDate == null || u.ExpirationDate > DateTime.Now)).ToList(), 
			TrialNotice.Extension => _users.Where((User u) => u.Institution.Trial.EndDate.HasValue && u.Institution.Trial.EndDate.Value.Date == DateTime.Now.AddMonths((int)noticeDay).Date && (int)u.Role.Code == 1 && u.Institution.AccountStatusId == 2 && u.RecordStatus && (u.ExpirationDate == null || u.ExpirationDate > DateTime.Now)).ToList(), 
			_ => null, 
		};
	}

	public StringBuilder ProcessTrialNotification(TrialNotice noticeDay)
	{
		List<User> users = GetUsersForNotice(noticeDay);
		StringBuilder sbUsers = new StringBuilder();
		int usersProcessed = 0;
		_trialNotificationEmailBuildService.InitEmailTemplates(noticeDay);
		int lastUpdatedInstitutionId = 0;
		foreach (User user in users)
		{
			usersProcessed++;
			EmailMessage emailMessage = _trialNotificationEmailBuildService.BuildTrialNotificationEmail(user);
			if (emailMessage != null && EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName) && !_r2UtilitiesSettings.EmailTestMode && lastUpdatedInstitutionId != user.Institution.Id)
			{
				UpdateInstitution(user.Institution.Id, noticeDay);
				lastUpdatedInstitutionId = user.Institution.Id;
			}
			sbUsers.AppendFormat("<div>Account #: {0}</div>", user.Institution.AccountNumber).AppendFormat("<div>Name : {0}, {1}</div>", user.LastName, user.FirstName).AppendFormat("<div>Email: {0}</div>", user.Email)
				.AppendFormat("<div>Last Session: {0}</div>", user.LastSession)
				.AppendFormat("<div>Territory: {0}</div>", (user.Institution.Territory != null) ? user.Institution.Territory.Name : "Not Specified")
				.Append("<div>&nbsp;</div>");
		}
		return new StringBuilder().AppendFormat("<div>{0}-{1}</div>", usersProcessed, noticeDay.ToTitle()).Append(sbUsers);
	}

	public bool UpdateInstitution(int institutionId, TrialNotice noticeDay)
	{
		string fieldName = "";
		switch (noticeDay)
		{
		case TrialNotice.First:
			fieldName = "dtTrialEndEmailWarn";
			break;
		case TrialNotice.Second:
			fieldName = "dtTrialEndEmail3DayWarn";
			break;
		case TrialNotice.Final:
			fieldName = "dtTrialEndEmailFinal";
			break;
		case TrialNotice.Extension:
			return false;
		}
		string updateSql = $"Update tInstitution set {fieldName} = GetDate() Where iInstitutionId = {institutionId}";
		_unitOfWork.Session.CreateSQLQuery(updateSql).List();
		return true;
	}
}
