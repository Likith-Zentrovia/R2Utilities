using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.Tasks.ContentTasks.BookInfo;
using R2V2.Core.Resource.BookSearch;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class FixHtmlMetaTagsTask : TaskBase
{
	private readonly IContentSettings _contentSettings;

	private readonly FixHtmlQueueDataService _fixHtmlQueueFactory;

	protected new string TaskName = "FixHtmlMetaTagsTask";

	public FixHtmlMetaTagsTask(IContentSettings contentSettings)
		: base("FixHtmlMetaTagsTask", "-FixHtmlMetaTagsTask", "x22", TaskGroup.Deprecated, "Task for updating the HTML meta tags in the HTML content index by dtSearch", enabled: false)
	{
		_fixHtmlQueueFactory = new FixHtmlQueueDataService();
		_contentSettings = contentSettings;
	}

	public override void Run()
	{
		try
		{
			Stopwatch totalRunTime = new Stopwatch();
			totalRunTime.Start();
			int fixedResourceCount = 0;
			int errorResourceCount = 0;
			int totalFixedFileCount = 0;
			TaskResultStep step = new TaskResultStep
			{
				Name = "FixMetaTags",
				StartTime = DateTime.Now
			};
			base.TaskResult.AddStep(step);
			UpdateTaskResult();
			ResourceCoreDataService resourceCoreDataService = new ResourceCoreDataService();
			FixHtmlQueue fixHtmlQueue = _fixHtmlQueueFactory.GetNext();
			while (fixHtmlQueue != null && fixHtmlQueue.ResourceId > 0)
			{
				fixHtmlQueue.DateStarted = DateTime.Now;
				ResourceCore resource = resourceCoreDataService.GetResourceByIsbn(fixHtmlQueue.Isbn, excludeForthcoming: true);
				fixHtmlQueue.DateFinished = DateTime.Now;
				if (FixHtmlMetaData(resource, out var fixedFileCount, out var errerMessage))
				{
					fixHtmlQueue.Status = "F";
					fixHtmlQueue.StatusMessage = $"{fixedFileCount} Html Files Fixed";
					fixedResourceCount++;
				}
				else
				{
					fixHtmlQueue.Status = "E";
					fixHtmlQueue.StatusMessage = $"ERROR - {fixedFileCount} Html Files Fixed - {errerMessage}";
					errorResourceCount++;
				}
				totalFixedFileCount += fixedFileCount;
				_fixHtmlQueueFactory.Update(fixHtmlQueue);
				R2UtilitiesBase.Log.InfoFormat("*** Total Run Time: {0:c}, total resource processed: {1}, fixed: {2}, errors: {3}, total files fixed: {4}", totalRunTime.Elapsed, errorResourceCount + fixedResourceCount, fixedResourceCount, errorResourceCount, totalFixedFileCount);
				fixHtmlQueue = _fixHtmlQueueFactory.GetNext();
			}
			totalRunTime.Stop();
			R2UtilitiesBase.Log.InfoFormat("### Total Run Time: {0:c}, total resource processed: {1}, fixed: {2}, errors: {3}, total files fixed: {4}", totalRunTime.Elapsed, errorResourceCount + fixedResourceCount, fixedResourceCount, errorResourceCount, totalFixedFileCount);
			step.CompletedSuccessfully = true;
			step.Results = $"tasked finished in {totalRunTime.Elapsed:c}";
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	public bool FixHtmlMetaData(ResourceCore resource, out int fixedFileCount, out string errorMessage)
	{
		fixedFileCount = 0;
		try
		{
			string isbn = resource.Isbn.Trim();
			StringBuilder infoMsg = new StringBuilder().AppendLine(">>>>>>>>>>").AppendFormat("\t\tISBN: {0}, Id: {1}, Status: {2}", isbn, resource.Id, resource.StatusId).AppendLine()
				.AppendLine("<<<<<<<<<<");
			R2UtilitiesBase.Log.Info(infoMsg);
			string htmlDirectoryPath = _contentSettings.NewContentLocation + "\\" + isbn;
			R2UtilitiesBase.Log.Info(htmlDirectoryPath);
			DirectoryInfo htmlDirectoryInfo = new DirectoryInfo(htmlDirectoryPath);
			if (!htmlDirectoryInfo.Exists)
			{
				R2UtilitiesBase.Log.WarnFormat("directory does not exist, {0}", htmlDirectoryInfo.FullName);
				errorMessage = "directory does not exist, " + htmlDirectoryInfo.FullName;
				return false;
			}
			string xmlDirectoryPath = _contentSettings.ContentLocation + "\\" + isbn;
			R2UtilitiesBase.Log.Info(xmlDirectoryPath);
			DirectoryInfo xmlDirectoryInfo = new DirectoryInfo(xmlDirectoryPath);
			if (!xmlDirectoryInfo.Exists)
			{
				R2UtilitiesBase.Log.WarnFormat("directory does not exist, {0}", xmlDirectoryInfo.FullName);
				errorMessage = "directory does not exist, " + xmlDirectoryInfo.FullName;
				return false;
			}
			BookSearchInfo bookSearchInfo = new BookSearchInfo(resource, xmlDirectoryInfo);
			BookSearchResource bookSearchResource = bookSearchInfo.ToBookSearchResource(_contentSettings.NewContentLocation, isbn);
			bookSearchResource.SaveR2BookSearchXml();
			FileInfo[] fileInfos = htmlDirectoryInfo.GetFiles();
			if (fileInfos.Length != 0)
			{
				FileInfo[] array = fileInfos;
				foreach (FileInfo fileInfo in array)
				{
					if (fileInfo.Extension == ".html")
					{
						string html = GetFileText(fileInfo.FullName);
						string xmlFilePath = fileInfo.FullName.Replace("html", "xml");
						string filePrefix = fileInfo.Name.Split('.')[0];
						ReplaceMetaTags(html, bookSearchInfo, xmlFilePath, fileInfo.FullName, filePrefix);
						fixedFileCount++;
					}
				}
				errorMessage = null;
				return true;
			}
			R2UtilitiesBase.Log.WarnFormat("NO files in directory '{0}'", htmlDirectoryPath);
			errorMessage = "NO files in directory '" + htmlDirectoryPath + "'";
			return false;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			errorMessage = ex.Message;
			return false;
		}
	}

	private string GetFileText(string filepath)
	{
		StringBuilder text = new StringBuilder();
		if (File.Exists(filepath))
		{
			using StreamReader sr = File.OpenText(filepath);
			string input;
			while ((input = sr.ReadLine()) != null)
			{
				text.AppendLine(input);
			}
		}
		return text.ToString();
	}

	private void ReplaceMetaTags(string html, BookSearchInfo bookSearchInfo, string xmlFullFilePath, string htmlFullFilePath, string filePrefix)
	{
		R2UtilitiesBase.Log.Debug(htmlFullFilePath);
		DocSearchInfo docSearchInfo = new DocSearchInfo(bookSearchInfo, xmlFullFilePath, filePrefix);
		int x = html.IndexOf("<!-- r2v2 meta tags - start -->", 0, StringComparison.Ordinal);
		if (x == -1)
		{
			x = html.IndexOf("<meta name=", 0, StringComparison.Ordinal);
		}
		else
		{
			R2UtilitiesBase.Log.Debug("debug");
		}
		int y = html.IndexOf("</head>", x, StringComparison.Ordinal);
		string newMetaTags;
		if (y < x)
		{
			y = html.IndexOf("<body ", x, StringComparison.Ordinal);
			newMetaTags = docSearchInfo.MetaTags + "</head>" + Environment.NewLine;
		}
		else
		{
			newMetaTags = docSearchInfo.MetaTags;
		}
		string oldMetaTags = html.Substring(x, y - x);
		html = html.Replace(oldMetaTags, newMetaTags);
		using StreamWriter outfile = new StreamWriter(htmlFullFilePath);
		outfile.Write(html);
	}
}
