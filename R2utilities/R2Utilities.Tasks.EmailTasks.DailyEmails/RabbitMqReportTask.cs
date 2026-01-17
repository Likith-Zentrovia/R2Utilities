using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class RabbitMqReportTask : EmailTaskBase
{
	private readonly ILog<RabbitMqReportTask> _log;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly RabbitMqReportEmailBuildService _rabbitMqReportEmailBuildService;

	public RabbitMqReportTask(ILog<RabbitMqReportTask> log, RabbitMqReportEmailBuildService rabbitMqReportEmailBuildService, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("RabbitMqReportTask", "-RabbitMqReportTask", "63", TaskGroup.InternalSystemEmails, "Sends report email on RabbitMq queues and messages in queue", enabled: true)
	{
		_log = log;
		_rabbitMqReportEmailBuildService = rabbitMqReportEmailBuildService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		base.TaskResult.Information = "RabbitMQ Report Task";
		TaskResultStep step = new TaskResultStep
		{
			Name = "ShoppingCartTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			List<RabbitMqQueueDetails> rabbitMqQueueDetailsList = GetRabbitMqDetails();
			Dictionary<string, List<RabbitMqQueueDetails>> hostDictionary = (from x in rabbitMqQueueDetailsList
				group x by x.vhost).ToDictionary((IGrouping<string, RabbitMqQueueDetails> x) => x.Key, (IGrouping<string, RabbitMqQueueDetails> x) => x.ToList());
			_rabbitMqReportEmailBuildService.InitEmailTemplates();
			EmailMessage emailMessage = _rabbitMqReportEmailBuildService.BuildRabbitMqReportEmail(hostDictionary, base.EmailSettings.TaskEmailConfig.ToAddresses.ToArray());
			bool success = EmailDeliveryService.SendTaskReportEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName);
			step.CompletedSuccessfully = success;
			step.Results = "RabbitMQ Report Task completed successfully";
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

	public List<RabbitMqQueueDetails> GetRabbitMqDetails()
	{
		if (string.IsNullOrWhiteSpace(_r2UtilitiesSettings.RabbitMqReportUrl) || _r2UtilitiesSettings.RabbitMqReportUserNameAndPassword == null || !_r2UtilitiesSettings.RabbitMqReportUserNameAndPassword.Any() || string.IsNullOrWhiteSpace(_r2UtilitiesSettings.RabbitMqReportUserNameAndPassword[0]) || string.IsNullOrWhiteSpace(_r2UtilitiesSettings.RabbitMqReportUserNameAndPassword[1]))
		{
			return null;
		}
		List<RabbitMqQueueDetails> rabbitMqQueueDetailsList = new List<RabbitMqQueueDetails>();
		try
		{
			WebRequest request = WebRequest.Create(_r2UtilitiesSettings.RabbitMqReportUrl);
			request.Credentials = new NetworkCredential(_r2UtilitiesSettings.RabbitMqReportUserNameAndPassword[0], _r2UtilitiesSettings.RabbitMqReportUserNameAndPassword[1]);
			using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
			using Stream dataStream = response.GetResponseStream();
			using StreamReader reader = new StreamReader(dataStream);
			string responseFromServer = reader.ReadToEnd();
			if (!string.IsNullOrWhiteSpace(responseFromServer))
			{
				rabbitMqQueueDetailsList = JsonConvert.DeserializeObject<List<RabbitMqQueueDetails>>(responseFromServer);
			}
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
		}
		return rabbitMqQueueDetailsList;
	}
}
