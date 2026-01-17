using System;
using System.Globalization;
using System.Text;
using R2Utilities.DataAccess.WebActivity;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Email.EmailBuilders;

public class InternalUtilitiesEmailBuildService : EmailBuildBaseService
{
	public InternalUtilitiesEmailBuildService(ILog<EmailBuildBaseService> log, IEmailSettings emailSettings, IContentSettings contentSettings)
		: base(log, emailSettings, contentSettings)
	{
	}

	public void InitEmailTemplates()
	{
		SetTemplates("WebActivityReport_Body.html", "WebActivityReport_Item.html", includeUnsubscribe: false, "WebActivityReport_Item_Result.html");
	}

	public R2V2.Infrastructure.Email.EmailMessage BuildWebActivityEmail(string bodyHtml, string[] emails, DateTime reportDate)
	{
		string messageHtml = GetWebActivityEmailHtml(bodyHtml);
		if (string.IsNullOrWhiteSpace(messageHtml))
		{
			return null;
		}
		return BuildEmailMessage(emails, string.Format("{0} {1:g}", "R2 Library Web Activity Report", reportDate), messageHtml);
	}

	private string GetWebActivityEmailHtml(string bodyHtml)
	{
		return BuildMainHtml("Web Activity Report", bodyHtml, null);
	}

	public string BuildWebActivitySubItemHtml(int hitCount, string resultDetails)
	{
		return base.SubItemTemplate.Replace("{Report_Result_Count}", $"{hitCount:#,###}").Replace("{Report_Result_Details}", resultDetails);
	}

	public string BuildWebActivityItemHtml(string reportDescription, string subItemHtml, string cssProperties)
	{
		return base.ItemTemplate.Replace("{Report_Description}", reportDescription).Replace("{Report_Results}", subItemHtml).Replace("{CSS_Properties}", cssProperties ?? "");
	}

	public string BuildBody(WebActivityReport webActivityReport, StringBuilder itemBuilder, DateTime startDate)
	{
		return base.BodyTemplate.Replace("{WebActivity_StartDate}", startDate.ToString(CultureInfo.InvariantCulture)).Replace("{WebActivity_EndDate}", DateTime.Now.ToString(CultureInfo.InvariantCulture)).Replace("{WebActivity_Page_Requests}", $"{webActivityReport.PageRequests:#,##0}")
			.Replace("{WebActivity_Average_Request_Times}", $"{webActivityReport.AveragePageRequestTime:#,##0}")
			.Replace("{WebActivity_Median_Request_Times}", $"{webActivityReport.MedianPageRequestTime:#,##0}")
			.Replace("{WebActivity_Search_Requests}", $"{webActivityReport.SearchCount:#,##0}")
			.Replace("{WebActivity_Search_Average}", $"{webActivityReport.SearchTimeAverage:#,##0}")
			.Replace("{WebActivity_Search_Max}", $"{webActivityReport.SearchTimeMax:#,##0}")
			.Replace("{WebActivity_All_Content_Requests}", $"{webActivityReport.AllContentRequests:#,##0}")
			.Replace("{WebActivity_TOC_Requests}", $"{webActivityReport.TocRequests:#,##0}")
			.Replace("{WebActivity_Content_Requests}", $"{webActivityReport.ContentRequests:#,##0}")
			.Replace("{WebActivity_Concurrency_Turnaways}", $"{webActivityReport.TurnawayConcurrency:#,##0}")
			.Replace("{WebActivity_Access_Turnaways}", $"{webActivityReport.TurnawayAccess:#,##0}")
			.Replace("{WebActivity_Print_Requests}", $"{webActivityReport.PrintRequests:#,##0}")
			.Replace("{WebActivity_Email_Requests}", $"{webActivityReport.EmailRequests:#,##0}")
			.Replace("{WebActivity_Sessions}", $"{webActivityReport.SessionCount:#,##0}")
			.Replace("{WebActivity_Page_Requests_2_5}", $"{webActivityReport.NumberOfRequestTimes.TwoToFive:#,##0}")
			.Replace("{WebActivity_Page_Requests_5_10}", $"{webActivityReport.NumberOfRequestTimes.FiveToTen:#,##0}")
			.Replace("{WebActivity_Page_Requests_Over_10}", $"{webActivityReport.NumberOfRequestTimes.MoreThanTen:#,##0}")
			.Replace("{WebActivity_Page_Requests_2_5_Percentage}", $"{webActivityReport.NumberOfRequestTimes.TwoToFivePercentage():P}")
			.Replace("{WebActivity_Page_Requests_5_10_Percentage}", $"{webActivityReport.NumberOfRequestTimes.FiveToTenPercentage():P}")
			.Replace("{WebActivity_Page_Requests_Over_10_Percentage}", $"{webActivityReport.NumberOfRequestTimes.MoreThanTenPercentage():P}")
			.Replace("{Result_Items}", itemBuilder.ToString());
	}
}
