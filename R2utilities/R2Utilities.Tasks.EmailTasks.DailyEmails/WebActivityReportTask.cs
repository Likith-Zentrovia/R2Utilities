using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess.WebActivity;
using R2Utilities.Email.EmailBuilders;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.EmailTasks.DailyEmails;

public class WebActivityReportTask : EmailTaskBase
{
	private readonly InternalUtilitiesEmailBuildService _emailBuildService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly WebActivityService _webActivityService;

	private DateTime _startDate;

	public WebActivityReportTask(IR2UtilitiesSettings r2UtilitiesSettings, WebActivityService webActivityService, InternalUtilitiesEmailBuildService emailBuildService)
		: base("WebActivityReportTask", "-WebActivityReportTask", "62", TaskGroup.InternalSystemEmails, "Sends the web activity report email", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_webActivityService = webActivityService;
		_emailBuildService = emailBuildService;
	}

	public override void Run()
	{
		_startDate = DateTime.Now.Date;
		base.TaskResult.Information = "Web Activity Report Task Run";
		TaskResultStep step = new TaskResultStep
		{
			Name = "WebActivityReportTask Run",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			WebActivityReport webActivityReport = GetWebActivityReport();
			bool success = ProcessWebActivityReport(webActivityReport);
			step.CompletedSuccessfully = success;
			step.Results = "Web Activity Report Task completed successfully";
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

	private bool ProcessWebActivityReport(WebActivityReport webActivityReport)
	{
		base.TaskResult.Information = "Process Web Activity Report";
		TaskResultStep step = new TaskResultStep
		{
			Name = "ProcessWebActivityReport (Build Email)",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		_emailBuildService.InitEmailTemplates();
		try
		{
			StringBuilder itemBuilder = new StringBuilder();
			if (webActivityReport.TopInstitutionPageRequests != null)
			{
				StringBuilder resultBuilder = new StringBuilder();
				foreach (TopInstitution topInstitution in webActivityReport.TopInstitutionPageRequests)
				{
					resultBuilder.Append(_emailBuildService.BuildWebActivitySubItemHtml(topInstitution.Count, GetResultDetails(topInstitution)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("<span style=\"color:0033FF;\" >Top Institution Page Requests</span>", resultBuilder.ToString(), "color:#0033FF;"));
			}
			if (webActivityReport.TopResources != null)
			{
				StringBuilder resultBuilder2 = new StringBuilder();
				foreach (TopResource topResource in webActivityReport.TopResources)
				{
					resultBuilder2.Append(_emailBuildService.BuildWebActivitySubItemHtml(topResource.Count, GetResultDetails(topResource)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("Top Resource Requests", resultBuilder2.ToString(), "color:#009900;"));
			}
			if (webActivityReport.TopIpRanges != null)
			{
				StringBuilder resultBuilder3 = new StringBuilder();
				foreach (TopIpAddress topIpRange in webActivityReport.TopIpRanges)
				{
					resultBuilder3.Append(_emailBuildService.BuildWebActivitySubItemHtml(topIpRange.Count, GetResultDetails(topIpRange)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("Top Ip Range Accesses", resultBuilder3.ToString(), null));
			}
			if (webActivityReport.TopInstitutionIpRanges != null)
			{
				StringBuilder resultBuilder4 = new StringBuilder();
				foreach (TopIpAddress topIpRange2 in webActivityReport.TopInstitutionIpRanges)
				{
					resultBuilder4.Append(_emailBuildService.BuildWebActivitySubItemHtml(topIpRange2.Count, GetResultDetails(topIpRange2)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("Top Institution Ip Range Accesses", resultBuilder4.ToString(), null));
			}
			if (webActivityReport.TopInstitutionResourceRequests != null)
			{
				StringBuilder resultBuilder5 = new StringBuilder();
				foreach (TopInstitutionResource topInstitutionsAndResources in webActivityReport.TopInstitutionResourceRequests)
				{
					resultBuilder5.Append(_emailBuildService.BuildWebActivitySubItemHtml(topInstitutionsAndResources.Count, GetResultDetails(topInstitutionsAndResources)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("Top Institution Resource Requests", resultBuilder5.ToString(), "color:#009900;"));
			}
			if (webActivityReport.TopInstitutionResourcePrintRequests != null)
			{
				StringBuilder resultBuilder6 = new StringBuilder();
				foreach (TopInstitutionResource topInstitutionsAndResources2 in webActivityReport.TopInstitutionResourcePrintRequests)
				{
					resultBuilder6.Append(_emailBuildService.BuildWebActivitySubItemHtml(topInstitutionsAndResources2.Count, GetResultDetails(topInstitutionsAndResources2)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("Top Institution Resource Print Requests", resultBuilder6.ToString(), "color:#330066;"));
			}
			if (webActivityReport.TopInstitutionResourceEmailRequests != null)
			{
				StringBuilder resultBuilder7 = new StringBuilder();
				foreach (TopInstitutionResource topInstitutionsAndResources3 in webActivityReport.TopInstitutionResourceEmailRequests)
				{
					resultBuilder7.Append(_emailBuildService.BuildWebActivitySubItemHtml(topInstitutionsAndResources3.Count, GetResultDetails(topInstitutionsAndResources3)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("Top Institution Resource Email Requests", resultBuilder7.ToString(), "color:#660000;"));
			}
			if (webActivityReport.TopInstitutionSessionRequests != null)
			{
				StringBuilder resultBuilder8 = new StringBuilder();
				foreach (TopInstitution topInstitutionsAndResources4 in webActivityReport.TopInstitutionSessionRequests)
				{
					resultBuilder8.Append(_emailBuildService.BuildWebActivitySubItemHtml(topInstitutionsAndResources4.Count, GetResultDetails(topInstitutionsAndResources4)));
				}
				itemBuilder.Append(_emailBuildService.BuildWebActivityItemHtml("Top Institution Sessions", resultBuilder8.ToString(), "color:#FF33CC;"));
			}
			string body = _emailBuildService.BuildBody(webActivityReport, itemBuilder, _startDate);
			EmailMessage emailMessage = _emailBuildService.BuildWebActivityEmail(body, base.EmailSettings.TaskEmailConfig.ToAddresses.ToArray(), DateTime.Now);
			bool success = false;
			if (emailMessage != null)
			{
				AddTaskCcToEmailMessage(emailMessage);
				AddTaskBccToEmailMessage(emailMessage);
				R2UtilitiesBase.Log.Debug(emailMessage.ToDebugString());
				success = EmailDeliveryService.SendTaskReportEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName);
			}
			step.CompletedSuccessfully = success;
			step.Results = "Process Web Activity Report completed successfully";
			return success;
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

	private WebActivityReport GetWebActivityReport()
	{
		base.TaskResult.Information = " Get Web Activity Report";
		TaskResultStep step = new TaskResultStep
		{
			Name = "GetWebActivityReport",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			WebActivityReport webActivityReport = _webActivityService.GetWebActivityReport(_startDate);
			step.CompletedSuccessfully = true;
			step.Results = "Get Web Activity Report completed successfully";
			return webActivityReport;
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

	private static string PopulateValue(string formatedString, string value)
	{
		return string.IsNullOrWhiteSpace(value) ? "" : string.Format(formatedString, value);
	}

	private static string PopulateValue(string formatedString, int value)
	{
		return (value == 0) ? "" : string.Format(formatedString, value);
	}

	private static string GetResultDetails(TopInstitution topInstitution)
	{
		return $"{topInstitution.AccountName}, [{topInstitution.AccountNumber}], ({topInstitution.InstitutionId})";
	}

	private static string GetResultDetails(TopResource topResource)
	{
		return $"{topResource.Isbn} - {topResource.Title}, ({topResource.ResourceId})";
	}

	private string GetResultDetails(TopIpAddress topIpRange)
	{
		if (string.IsNullOrWhiteSpace(topIpRange.AccountNumber))
		{
			string externalDescription = GetExternalIpInformation(topIpRange);
			if (!string.IsNullOrWhiteSpace(externalDescription))
			{
				return externalDescription;
			}
		}
		return string.Format("{0}.{1}.{2}.{3} --- {4}{5}{6}{7}", topIpRange.OctetA, topIpRange.OctetB, topIpRange.OctetC, topIpRange.OctetD, PopulateValue("{0}", topIpRange.AccountName), PopulateValue(", [{0}]", topIpRange.AccountNumber), PopulateValue(", ({0})", topIpRange.InstitutionId), PopulateValue(", [{0}]", topIpRange.CountryCode));
	}

	private string GetExternalIpInformation(TopIpAddress topIpRange)
	{
		try
		{
			string ipAddress = $"{topIpRange.OctetA}.{topIpRange.OctetB}.{topIpRange.OctetC}.{topIpRange.OctetD}";
			WebRequest request = WebRequest.Create("https://ipapi.co/" + ipAddress + "/json/");
			request.Credentials = CredentialCache.DefaultCredentials;
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();
			Stream dataStream = response.GetResponseStream();
			string responseFromServer = null;
			if (dataStream != null)
			{
				StreamReader reader = new StreamReader(dataStream);
				responseFromServer = reader.ReadToEnd();
				reader.Close();
				dataStream.Close();
			}
			response.Close();
			JObject jsonResponse = JObject.Parse(responseFromServer);
			JToken organization = jsonResponse["org"];
			JToken countryCode = jsonResponse["country"];
			R2UtilitiesBase.Log.Info($"GetExternalIpInformation Response Information: {jsonResponse}");
			return $"{topIpRange.OctetA}.{topIpRange.OctetB}.{topIpRange.OctetC}.{topIpRange.OctetD} --- {organization}[{countryCode}]";
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
		}
		return null;
	}

	private static string GetResultDetails(TopInstitutionResource topInstitutionsAndResources)
	{
		return $"{topInstitutionsAndResources.AccountName}, [{topInstitutionsAndResources.AccountNumber}], ({topInstitutionsAndResources.InstitutionId}) --- {topInstitutionsAndResources.Isbn} - {topInstitutionsAndResources.Title}, ({topInstitutionsAndResources.ResourceId})";
	}
}
