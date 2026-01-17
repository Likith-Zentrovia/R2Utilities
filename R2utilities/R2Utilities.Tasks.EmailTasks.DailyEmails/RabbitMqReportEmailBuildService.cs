using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Utilities.DataAccess;
using R2Utilities.Email.EmailBuilders;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class RabbitMqReportEmailBuildService : InternalUtilitiesEmailBuildService
{
	public RabbitMqReportEmailBuildService(ILog<EmailBuildBaseService> log, IEmailSettings emailSettings, IContentSettings contentSettings)
		: base(log, emailSettings, contentSettings)
	{
	}

	public new void InitEmailTemplates()
	{
		SetTemplates("RabbitMqReport_Body.html", "RabbitMqReport_Host.html", includeUnsubscribe: false, "RabbitMqReport_Host_Queue.html");
	}

	public EmailMessage BuildRabbitMqReportEmail(Dictionary<string, List<RabbitMqQueueDetails>> detailsDictionary, string[] emails)
	{
		string messageHtml = GetRabbitMqReportEmailHtml(detailsDictionary);
		return string.IsNullOrWhiteSpace(messageHtml) ? null : BuildEmailMessage(emails, "R2 Library RabbitMQ Queues", messageHtml);
	}

	private string GetRabbitMqReportEmailHtml(Dictionary<string, List<RabbitMqQueueDetails>> detailsDictionary)
	{
		StringBuilder itemBuilder = new StringBuilder();
		List<RabbitMqQueueDetails> queuesWithMessagesList = new List<RabbitMqQueueDetails>();
		foreach (KeyValuePair<string, List<RabbitMqQueueDetails>> detailsItem in detailsDictionary)
		{
			string itemHtml = GetRabbitMqHostDetails(detailsItem.Key, detailsItem.Value);
			itemBuilder.Append(itemHtml);
			IEnumerable<RabbitMqQueueDetails> queuesWithMessages = detailsItem.Value.Where((RabbitMqQueueDetails x) => x.messages > 0);
			queuesWithMessagesList.AddRange(queuesWithMessages);
		}
		string bodyHtml = BuildBodyHtml().Replace("{Date_Run}", DateTime.Now.ToString("g")).Replace("{Instances_Count}", detailsDictionary.Keys.Count.ToString()).Replace("{Queues_Count}", detailsDictionary.Sum((KeyValuePair<string, List<RabbitMqQueueDetails>> x) => x.Value.Count).ToString())
			.Replace("{Queues_With_Messages_Count}", queuesWithMessagesList.Count.ToString())
			.Replace("{Queues_Error_Count_Style}", GetErrorCountStyle(queuesWithMessagesList.Count))
			.Replace("{Host_Items}", itemBuilder.ToString());
		return BuildMainHtml("RabbitMq Queues", bodyHtml, null);
	}

	private string GetRabbitMqHostDetails(string hostName, List<RabbitMqQueueDetails> details)
	{
		StringBuilder queueBuilder = new StringBuilder();
		details = details.OrderByDescending((RabbitMqQueueDetails x) => x.messages).ToList();
		foreach (RabbitMqQueueDetails test in details)
		{
			queueBuilder.Append(base.SubItemTemplate.Replace("{Queue_Name}", test.name).Replace("{Queue_Last_Run}", GetIdleInEst(test.idle_since)).Replace("{Queue_Messages}", test.messages.ToString())
				.Replace("{Queue_Number_Sent}", test.message_stats?.publish.ToString("N0") ?? "")
				.Replace("{Queues_Error_Count_Style}", (test.messages > 0) ? GetErrorCountStyle(test.messages) : ""));
		}
		return base.ItemTemplate.Replace("{Host_Name}", hostName).Replace("{Queue_Items}", queueBuilder.ToString());
	}

	private string GetIdleInEst(string utcTime)
	{
		if (!string.IsNullOrWhiteSpace(utcTime))
		{
			try
			{
				DateTime parsedTime = DateTime.SpecifyKind(DateTime.Parse(utcTime), DateTimeKind.Utc);
				if (parsedTime != DateTime.MinValue)
				{
					TimeZoneInfo est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
					return TimeZoneInfo.ConvertTime(parsedTime, est).ToString("g");
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private string GetErrorCountStyle(int errorCount)
	{
		return (errorCount > 0) ? "background-color:#ff0000; color: white" : "background-color:#00FF00; color: white";
	}
}
