using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class MakeTocRequestsTask : TaskBase, ITask
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private string _isbns;

	private int _maxBatchSize;

	private int _maxResourceId;

	private int _minResourceId;

	private bool _orderBatchDescending;

	private ResourceCoreDataService _resourceCoreDataService;

	private string _tocurl;

	public MakeTocRequestsTask(IR2UtilitiesSettings r2UtilitiesSettings)
		: base("MakeTocRequestsTask", "-MakeTocRequestsTask", "80", TaskGroup.ContentLoading, "Makes TOC requests for the resources specified via the URL specified", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_maxBatchSize = GetArgumentInt32("maxBatchSize", _r2UtilitiesSettings.HtmlIndexerBatchSize);
		_minResourceId = GetArgumentInt32("minResourceId", 0);
		_maxResourceId = GetArgumentInt32("maxResourceId", 100000);
		_orderBatchDescending = GetArgumentBoolean("orderBatchDescending", _r2UtilitiesSettings.OrderBatchDescending);
		_isbns = GetArgument("isbn");
		_tocurl = GetArgument("tocurl");
		R2UtilitiesBase.Log.InfoFormat("-maxBatchSize: {0}, -minResourceId: {1}, -maxResourceId: {2}, -orderBatchDescending: {3}", _maxBatchSize, _minResourceId, _maxResourceId, _orderBatchDescending);
		R2UtilitiesBase.Log.InfoFormat("-tocurl: {0}", _tocurl);
		R2UtilitiesBase.Log.InfoFormat("-isbns: {0}", _isbns);
		SetSummaryEmailSetting(includeOkTaskSteps: true, showStepTotals: true, 100);
	}

	public override void Run()
	{
		StringBuilder taskInfo = new StringBuilder();
		TaskResultStep summaryStep = new TaskResultStep
		{
			Name = "MakeTocRequestsSummary",
			StartTime = DateTime.Now,
			Results = string.Empty
		};
		base.TaskResult.AddStep(summaryStep);
		UpdateTaskResult();
		_resourceCoreDataService = new ResourceCoreDataService();
		StringBuilder summaryStepResults = new StringBuilder();
		try
		{
			taskInfo.AppendFormat("Making TOC requests for {0} resources.", _maxBatchSize);
			R2UtilitiesBase.Log.InfoFormat("Making TOC requests for {0} resources.", _maxBatchSize);
			IList<ResourceCore> resourceCores = _resourceCoreDataService.GetActiveAndArchivedResources(orderByDescending: true, _minResourceId, _maxResourceId, _maxBatchSize, _isbns?.Split(','));
			taskInfo.AppendFormat("{0} resources to process", resourceCores.Count);
			R2UtilitiesBase.Log.InfoFormat("{0} resources to process", resourceCores.Count);
			int resourceCount = 0;
			summaryStepResults.AppendFormat("maxBatchSize: {0}, _minResourceId: {1}, _maxResourceId: {2} - ISNBs:", _maxBatchSize, _minResourceId, _maxResourceId);
			summaryStepResults.AppendLine();
			foreach (ResourceCore resourceCore in resourceCores)
			{
				resourceCount++;
				R2UtilitiesBase.Log.InfoFormat("Request TOC for ISBN: {0}, resource {1} of {2}", resourceCore.Isbn, resourceCount, resourceCores.Count);
				summaryStepResults.AppendLine(MakeTocRequest(resourceCore.Isbn, resourceCore.Id));
			}
			if (resourceCount == 0)
			{
				TaskResultStep step = new TaskResultStep
				{
					Name = "zero TOCs to requested.",
					StartTime = DateTime.Now,
					CompletedSuccessfully = true,
					Results = "No resources",
					EndTime = DateTime.Now
				};
				base.TaskResult.AddStep(step);
				UpdateTaskResult();
				summaryStepResults.Insert(0, "WARNING - NO TOCS REQUESTED!!!! ");
			}
			else
			{
				summaryStepResults.Insert(0, $"TOC request count: {resourceCount}, ");
			}
			summaryStep.CompletedSuccessfully = true;
		}
		catch (Exception ex)
		{
			summaryStepResults.Insert(0, "EXCEPTION: " + ex.Message + "\r\n");
			summaryStep.CompletedSuccessfully = false;
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			base.TaskResult.Information = taskInfo.ToString();
			summaryStep.EndTime = DateTime.Now;
			summaryStep.Results = summaryStepResults.ToString();
			UpdateTaskResult();
		}
	}

	private string MakeTocRequest(string isbn, int resourceId)
	{
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		string results = null;
		string url = _tocurl + isbn;
		R2UtilitiesBase.Log.DebugFormat("url: {0}", url);
		long contentLength = 0L;
		try
		{
			HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
			webRequest.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; .NET CLR 1.1.4322; .NET CLR 2.0.50727) - R2 Toc Crawler";
			webRequest.Timeout = 30000;
			webRequest.Proxy = null;
			using WebResponse webResponse = webRequest.GetResponse();
			contentLength = webResponse.ContentLength;
			using Stream responseStream = webResponse.GetResponseStream();
			if (responseStream != null)
			{
				using StreamReader stream = new StreamReader(responseStream);
				results = stream.ReadToEnd();
			}
		}
		catch (WebException ex)
		{
			R2UtilitiesBase.Log.WarnFormat("URL: {0}, WebException: {1}", url, ex.Message);
		}
		catch (Exception ex2)
		{
			R2UtilitiesBase.Log.ErrorFormat("URL: {0}, Exception: {1}", url, ex2.Message);
		}
		stopwatch.Stop();
		string status = $"time: {stopwatch.ElapsedMilliseconds:#,###} ms, url: {url}, content length: {contentLength:#,###}, resourceId: {resourceId}";
		R2UtilitiesBase.Log.Info(status);
		return status;
	}
}
