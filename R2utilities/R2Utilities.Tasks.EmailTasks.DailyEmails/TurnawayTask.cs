using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Authentication;
using R2V2.Core.Email;
using R2V2.Core.Reports;
using R2V2.Core.Resource.Discipline;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class TurnawayTask : EmailTaskBase
{
	private readonly EmailTaskService _emailTaskService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly TurnawayEmailBuildService _turnawayEmailBuildService;

	public TurnawayTask(EmailTaskService emailTaskService, TurnawayEmailBuildService turnawayEmailBuildService, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("TurnawayTask", "-SendTurnawayEmails", "44", TaskGroup.CustomerEmails, "Sends turnaway emails to customers (IAs)", enabled: true)
	{
		_emailTaskService = emailTaskService;
		_turnawayEmailBuildService = turnawayEmailBuildService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		EmailDeliveryService.DebugMaxEmailsSent = _r2UtilitiesSettings.EmailTestNumberOfEmails;
		base.TaskResult.Information = "Turnaway Resource Emails Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "TurnawayTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			int successEmails = 0;
			int failureEmails = 0;
			int failureToBuild = 0;
			StringBuilder failureEmailAddress = new StringBuilder();
			List<TurnawayResource> turnaways2 = _emailTaskService.GetInstitutionTurnaways(_r2UtilitiesSettings.R2ReportsDatabaseName, _r2UtilitiesSettings.R2DatabaseName);
			List<User> turnawayUsers = _emailTaskService.GetUsersForTurnawayEmail();
			var turnawayGroupings = (from t in turnaways2
				group t by t.InstitutionId into g
				select new
				{
					InstitutionId = g.Key,
					Turnaways = g.ToList()
				}).ToList();
			List<User> turnawayGroupingUsers = turnawayUsers.Where((User x) => turnawayGroupings.Select(y => y.InstitutionId).Contains(x.InstitutionId.GetValueOrDefault())).ToList();
			R2UtilitiesBase.Log.Info($"Processing {turnawayGroupings.Count} Institutions with Turnaways");
			R2UtilitiesBase.Log.Info($"Processing {turnawayGroupingUsers.Count} users for those Turnaways.");
			int userCount = 0;
			foreach (var grouping in turnawayGroupings)
			{
				List<User> institutionUsers = turnawayGroupingUsers.Where((User x) => x.InstitutionId == grouping.InstitutionId).ToList();
				List<TurnawayResource> turnaways3 = grouping.Turnaways.OrderBy(delegate(TurnawayResource x)
				{
					ISpecialty specialty = x.Resource.Specialties.OrderBy((ISpecialty y) => y.Name).FirstOrDefault();
					return (specialty == null) ? null : ((x.Resource.Specialties != null) ? specialty.Name : "zzz");
				}).ToList();
				if (institutionUsers.Any())
				{
					R2UtilitiesBase.Log.Info($"Processing Institution {grouping.InstitutionId}");
					foreach (TurnawayResource item in turnaways3)
					{
						R2UtilitiesBase.Log.Info($"[ResourceId: {item.ResourceId} | AccessTurnaway: {item.TurnawayDates.Count((TurnawayDate x) => x.IsAccessTurnaway)} | ConcurrencyTurnaway: {item.TurnawayDates.Count((TurnawayDate x) => !x.IsAccessTurnaway)}]");
					}
					foreach (User user in institutionUsers)
					{
						userCount++;
						EmailMessage emailMessage = GetTurnawayEmail(user, turnaways3);
						string userInfo = $"[InstitutionId:{user.Institution.Id} | UserId:{user.Id} | UserEmail: {user.Email}]";
						if (emailMessage != null)
						{
							if (EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName))
							{
								successEmails++;
								continue;
							}
							failureEmails++;
							failureEmailAddress.Append(userInfo + "<br/>");
						}
						else
						{
							R2UtilitiesBase.Log.Info($"{userCount} of {turnawayGroupingUsers.Count} ignored, turnaway email is null {userInfo}");
							failureToBuild++;
							failureEmailAddress.Append(userInfo + "<br/>");
						}
					}
				}
				else
				{
					R2UtilitiesBase.Log.Info($"No Users found for institution : {grouping.InstitutionId} || {grouping.Turnaways.Count} turnaways found;");
				}
			}
			step.CompletedSuccessfully = failureEmails == 0 && failureToBuild == 0;
			step.Results = $"\n{successEmails} Turnaway Resource emails sent,\n{failureToBuild} emails failed to build,\n{failureEmails} emails failed to send.\nFailed Emails information: {failureEmailAddress}";
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

	public EmailMessage GetTurnawayEmail(User user, List<TurnawayResource> turnawayResources)
	{
		try
		{
			StringBuilder itemBuilder = new StringBuilder();
			string lastSpecialtyName = null;
			foreach (TurnawayResource item in turnawayResources.Where((TurnawayResource x) => x.Resource != null))
			{
				ISpecialty specialty = ((item.Resource.Specialties != null) ? item.Resource.Specialties.OrderBy((ISpecialty x) => x.Name).FirstOrDefault() : null);
				if (specialty != null && lastSpecialtyName != specialty.Name)
				{
					itemBuilder.Append(_turnawayEmailBuildService.BuildSpecialtyHeader(item.Resource, specialty));
					lastSpecialtyName = specialty.Name;
				}
				itemBuilder.Append(_turnawayEmailBuildService.BuildItemHtml(item.Resource, GetTurnawayField(item), user.InstitutionId.GetValueOrDefault(), user.Institution.AccountNumber));
			}
			return _turnawayEmailBuildService.BuildTurnawayEmail(user, itemBuilder.ToString());
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			return null;
		}
	}

	public string GetTurnawayField(TurnawayResource turnawayResource)
	{
		StringBuilder sb = new StringBuilder();
		if (turnawayResource.TurnawayDates.Any())
		{
			List<TurnawayDate> concurrentTurnaways = turnawayResource.TurnawayDates.Where((TurnawayDate x) => !x.IsAccessTurnaway).ToList();
			List<TurnawayDate> accessTurnaways = turnawayResource.TurnawayDates.Where((TurnawayDate x) => x.IsAccessTurnaway).ToList();
			BuildTurnaway("Concurrent Licenses Exceeded Turnaways: ", concurrentTurnaways, sb);
			BuildTurnaway("         Non-Purchased Title Turnaways: ", accessTurnaways, sb);
		}
		return sb.ToString();
	}

	private void BuildTurnaway(string label, List<TurnawayDate> turnaways, StringBuilder sb)
	{
		int maxCount = 5;
		if (!turnaways.Any())
		{
			return;
		}
		sb.Append((turnaways.Count > maxCount) ? PopulateFieldOrNull(label, $"{turnaways.Count}") : PopulateField(label));
		sb.Append("<br/>");
		sb.Append("<br/>");
		IEnumerable<TurnawayDate> test = turnaways.OrderByDescending((TurnawayDate x) => x.TurnawayTimeStamp).Take(maxCount);
		foreach (TurnawayDate concurrentTurnaway in test)
		{
			sb.Append("<div style=\"text - align: left\">");
			sb.Append(PopulateFieldOrNull("Occurrence: ", concurrentTurnaway.TurnawayTimeStamp.ToString("MM/dd/yyyy hh:mm:ss tt")) + "<br/>");
			sb.Append(PopulateFieldOrNull("RequestId: ", concurrentTurnaway.RequestId) + "<br/>");
			sb.Append(PopulateFieldOrNull("SessionId: ", concurrentTurnaway.SessionId) + "<br/>");
			sb.Append(PopulateFieldOrNull("IpAddress: ", concurrentTurnaway.IpAddress) + "<br/>");
			sb.Append("</div>");
			sb.Append("<br/>");
		}
	}
}
