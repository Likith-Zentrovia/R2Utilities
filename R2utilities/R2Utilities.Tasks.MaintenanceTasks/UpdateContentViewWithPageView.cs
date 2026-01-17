using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class UpdateContentViewWithPageView : TaskBase
{
	private readonly ReportDataService _reportDataService;

	public UpdateContentViewWithPageView(ReportDataService reportDataService)
		: base("UpdateContentViewWithPageView", "-UpdateContentViewWithPageView", "10", TaskGroup.ContentLoading, "Updates the Content View Table in the R2Reports Database with Search Details", enabled: true)
	{
		_reportDataService = reportDataService;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will update the Content View Table in the R2Reports Database with Search Details.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "UpdateContentViewWithPageView",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			DateTime startDate = DateTime.Parse("9/01/2012 12:00:01 AM");
			DateTime endDate = DateTime.Parse("7/10/2013 23:59:59 PM");
			List<ReportPageView> pageViews = _reportDataService.GetPageViews(startDate, endDate);
			List<ReportContentView> contentViews = _reportDataService.GetContentViews(startDate, endDate);
			Dictionary<ReportContentView, string> foundSearchResourceHits = new Dictionary<ReportContentView, string>();
			ReportPageView lastPageViewWithSearch = null;
			int hitCount = 0;
			int counter = 0;
			if (contentViews.Any() && pageViews.Any())
			{
				foreach (ReportPageView reportPageView in pageViews)
				{
					counter++;
					R2UtilitiesBase.Log.DebugFormat("Processing {0} of {1}   ||    Total Found: {2}", counter, pageViews.Count, hitCount);
					Console.WriteLine("Processing {0} of {1}   ||    Total Found: {2}", counter, pageViews.Count, hitCount);
					if (reportPageView.Url.Contains("/Search?q="))
					{
						lastPageViewWithSearch = reportPageView;
					}
					else if (reportPageView.Url.Contains("/Resource/") && lastPageViewWithSearch != null && lastPageViewWithSearch.SessionId == reportPageView.SessionId)
					{
						ReportPageView view = reportPageView;
						ReportContentView contentViewMatch = contentViews.FirstOrDefault((ReportContentView x) => x.InstitutionId == view.InstitutionId && x.IpAddressInteger == view.IpAddressInteger && x.ContentViewTimestamp == view.PageViewTimeStamp);
						if (contentViewMatch != null)
						{
							hitCount++;
							string searchTerm = Regex.Split(lastPageViewWithSearch.Url, "q=").Skip(1).FirstOrDefault();
							foundSearchResourceHits.Add(contentViewMatch, HttpUtility.UrlDecode(searchTerm));
							R2UtilitiesBase.Log.DebugFormat("Found search hit. ContentId : {0} || Search Term: {1}", contentViewMatch.ContentId, searchTerm);
							Console.WriteLine("Found search hit. ContentId : {0} || Search Term: {1}", contentViewMatch.ContentId, searchTerm);
						}
						lastPageViewWithSearch = null;
					}
				}
				if (foundSearchResourceHits.Any())
				{
					_reportDataService.SaveContentViews(foundSearchResourceHits);
				}
			}
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"{foundSearchResourceHits.Count} searches have been matched with content Retrievals.");
			step.Results = sb.ToString();
			step.CompletedSuccessfully = true;
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
