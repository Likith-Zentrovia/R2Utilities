using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.BookInfo;
using R2Utilities.Tasks.ContentTasks.Xsl;
using R2V2.Core.Resource;
using R2V2.Core.Resource.BookSearch;
using R2V2.Core.Resource.Content;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class TransformXmlService
{
	private readonly ContentTransformer _contentTransformer;

	private readonly IndexQueueDataService _indexQueueDataService;

	private readonly ILog<TransformXmlService> _log;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ResourceCoreDataService _resourceCoreDataService;

	private readonly TransformedResourceDataService _transformedResourceFactory;

	private readonly StringBuilder _validationWarningMessages = new StringBuilder();

	public readonly string HtmlRootPath;

	public readonly string XmlRootPath;

	public string ValidationWarningMessages => _validationWarningMessages.ToString();

	public TransformXmlService(ILog<TransformXmlService> log, ContentTransformer contentTransformer, IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_log = log;
		_contentTransformer = contentTransformer;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_resourceCoreDataService = new ResourceCoreDataService();
		_transformedResourceFactory = new TransformedResourceDataService();
		HtmlRootPath = contentSettings.NewContentLocation + "/html";
		XmlRootPath = contentSettings.ContentLocation;
		_indexQueueDataService = new IndexQueueDataService();
	}

	public ResourceTransformData TransformResource(ResourceCore resource)
	{
		try
		{
			if (resource == null)
			{
				_log.Warn("RESOURCE IS NULL");
				return null;
			}
			bool isValidResource = resource.Id > 0;
			if (!isValidResource)
			{
				_log.Warn("INVALID RESOURCE");
			}
			_log.Debug(resource.ToDebugString());
			TransformedResource transformedResource = new TransformedResource
			{
				ResourceId = resource.Id,
				Isbn = resource.Isbn,
				Successfully = false,
				DateStarted = DateTime.Now,
				Results = "Processing ..."
			};
			if (isValidResource)
			{
				_transformedResourceFactory.Insert(transformedResource);
			}
			_log.Info(transformedResource.ToDebugString());
			ResourceTransformData rtd = new ResourceTransformData(resource, transformedResource.Id, HtmlRootPath);
			if (isValidResource)
			{
				TransformResource(rtd);
				rtd.ValidateNewHtmlFiles();
				if (rtd.ValidationFailureCount > 0)
				{
					_validationWarningMessages.AppendFormat("Id: {0}, ISBN: {1}, ValidationFailureCount: {2}", rtd.Resource.Id, rtd.Isbn, rtd.ValidationFailureCount).AppendLine();
				}
			}
			else
			{
				rtd.StatusMessage = "Invalid Resource - 0 Files Transformed!";
				rtd.HasWarning = true;
				rtd.Successful = true;
			}
			_log.DebugFormat("rtd.TransferCount: {0}", rtd.TransferCount);
			if (rtd.TransferCount > 0)
			{
				bool addedToQueue = _indexQueueDataService.AddResourceToQueue(resource.Id, resource.Isbn);
				_log.DebugFormat("Resource added to index queue: {0}", addedToQueue);
			}
			return rtd;
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			return null;
		}
	}

	private void TransformResource(ResourceTransformData rtd)
	{
		rtd.TransferCount = 0;
		_log.InfoFormat(">>>>>>>>>> PROCESSING - ResourceId: {0}, ISBN: {1}, Title:{2}", rtd.Resource.Id, rtd.Isbn, rtd.Resource.Title);
		_log.InfoFormat(">>>>>>>>>> STATUS - {0}", rtd.Resource.StatusId);
		try
		{
			IList<FileInfo> filesToTransform = GetFileToTransform(rtd.Isbn);
			_log.InfoFormat("{0} to transform", filesToTransform.Count);
			if (filesToTransform.Count > 0)
			{
				string contentDirectoryName = XmlRootPath + "\\" + rtd.Isbn;
				DirectoryInfo directoryInfo = new DirectoryInfo(contentDirectoryName);
				BookSearchInfo bookSearchInfo = new BookSearchInfo(rtd.Resource, directoryInfo);
				rtd.HtmlDirectoryInfo.Create();
				_log.InfoFormat("Processing book.xml, file: {0}", HtmlRootPath + "\\" + rtd.Isbn + "\\r2BookSearch." + rtd.Isbn + ".xml");
				BookSearchResource bookSearchResource = bookSearchInfo.ToBookSearchResource(HtmlRootPath, rtd.Isbn);
				bookSearchResource.SaveR2BookSearchXml();
				int authorInsertCount = SaveAuthors(bookSearchInfo, rtd.Resource.Id);
				_log.InfoFormat("Author Insert Count: {0}", authorInsertCount);
				Stopwatch transformStopwatch = new Stopwatch();
				transformStopwatch.Start();
				int totalTransformations = filesToTransform.Count + bookSearchInfo.Glossaries.Count();
				int transformCount = 0;
				foreach (FileInfo fileInfo in filesToTransform)
				{
					string[] fileParts = fileInfo.Name.Split('.');
					transformCount++;
					TransformFile(rtd, fileInfo, bookSearchInfo, fileParts[2], isGlossary: false, $"{transformCount} of {totalTransformations}");
				}
				foreach (string glossary in bookSearchInfo.Glossaries)
				{
					transformCount++;
					TransformFile(rtd, bookSearchInfo.BookXmlFileInfo, bookSearchInfo, glossary, isGlossary: true, $"{transformCount} of {totalTransformations}");
				}
				transformStopwatch.Stop();
				_log.InfoFormat("{0} of {1} files transformed successfully in {2:c}, {3} errors, {4} validation failures", rtd.TransferCount, filesToTransform.Count, transformStopwatch.Elapsed, rtd.ErrorCount, rtd.ValidationFailureCount);
				_log.InfoFormat("Avg Transform Time: {0} ms", transformStopwatch.ElapsedMilliseconds / filesToTransform.Count);
				rtd.StatusMessage = $"{rtd.TransferCount} of {filesToTransform.Count} files transformed successfully in {transformStopwatch.Elapsed:c}, {rtd.ErrorCount} errors, {rtd.ValidationFailureCount} validation failures, Avg Transform Time: {transformStopwatch.ElapsedMilliseconds / filesToTransform.Count} ms";
				rtd.Successful = rtd.ErrorCount == 0;
			}
			else
			{
				rtd.Successful = false;
				rtd.StatusMessage = "0 files to transform!";
			}
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			rtd.StatusMessage = "EXCEPTION: " + ex.Message;
			rtd.Successful = false;
		}
		_log.InfoFormat("<<<<<<<<<< SUCCESSFUL - {0}, ISBN: {1}", rtd.Successful, rtd.Isbn);
		rtd.Complete();
	}

	public bool TransformFile(ResourceTransformData rtd, FileInfo fileInfo, BookSearchInfo bookSearchInfo, string section, bool isGlossary, string logData)
	{
		try
		{
			_contentTransformer.Section = section;
			_contentTransformer.Isbn = rtd.Isbn;
			ContentType contentType = GetContentType(section);
			if (_contentTransformer.Transform(contentType, ResourceAccess.Allowed, "", email: false) is HtmlTransformResult transformResult)
			{
				rtd.TransferCount++;
				string htmlFilePath = _contentTransformer.OutputFilename;
				long modifyHtmlTime = ModifyHtmlFile(transformResult.Result, bookSearchInfo, fileInfo, htmlFilePath);
				long renameTime = RenameHtmlFile(htmlFilePath, fileInfo.Name, isGlossary ? section : null);
				_log.InfoFormat("Transformed '{0}' in {1} ms, modified in {2} ms, renamed in {3} ms, {4}", fileInfo.Name, transformResult.TransformTime, modifyHtmlTime, renameTime, logData);
				return true;
			}
			_log.ErrorFormat("Error transforming file: {0}", fileInfo.Name);
			rtd.Successful = false;
			rtd.StatusMessage = "Error transforming resource";
			rtd.AddError("Error transforming resource", fileInfo.Name);
			return false;
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			rtd.AddError(ex.Message, fileInfo.Name);
			_log.WarnFormat("ERROR TRANSFORMING {0}, Exception: {1}", fileInfo.Name, ex.Message);
			return false;
		}
	}

	private IList<FileInfo> GetFileToTransform(string isbn)
	{
		List<FileInfo> filesToTransform = new List<FileInfo>();
		try
		{
			string contentDirectoryName = XmlRootPath + "\\" + isbn.Trim();
			DirectoryInfo directoryInfo = new DirectoryInfo(contentDirectoryName);
			if (!directoryInfo.Exists)
			{
				_log.WarnFormat("Content directory does not exist, {0}", contentDirectoryName);
				return filesToTransform;
			}
			FileInfo[] fileInfos = directoryInfo.GetFiles();
			if (fileInfos.Length != 0)
			{
				FileInfo[] array = fileInfos;
				foreach (FileInfo fileInfo in array)
				{
					if (fileInfo.Name.StartsWith("sect1.") || fileInfo.Name.StartsWith("dedication.") || fileInfo.Name.StartsWith("appendix.") || fileInfo.Name.StartsWith("preface."))
					{
						filesToTransform.Add(fileInfo);
					}
				}
			}
			return filesToTransform;
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	private long ModifyHtmlFile(string html, BookSearchInfo bookSearchInfo, FileInfo xmlFileInfo, string htmlFullFilePath)
	{
		Stopwatch modifyHtmlStopwatch = new Stopwatch();
		modifyHtmlStopwatch.Start();
		string filePrefix = xmlFileInfo.Name.Split('.')[0];
		DocSearchInfo docSearchInfo = new DocSearchInfo(bookSearchInfo, xmlFileInfo.FullName, filePrefix);
		string fullHtml = new StringBuilder().AppendLine("<html><head>").Append(docSearchInfo.MetaTags).AppendLine("</head>")
			.AppendLine("<body>")
			.AppendLine("<!-- r2v2 content from transform -->")
			.AppendLine(html)
			.AppendLine("</body>")
			.AppendLine("</html>")
			.ToString();
		using (StreamWriter outfile = new StreamWriter(htmlFullFilePath))
		{
			outfile.Write(fullHtml);
		}
		modifyHtmlStopwatch.Stop();
		return modifyHtmlStopwatch.ElapsedMilliseconds;
	}

	private static ContentType GetContentType(string section)
	{
		switch (section.Substring(0, 2).ToLower())
		{
		case "ap":
			return ContentType.Appendix;
		case "dd":
		case "de":
			return ContentType.Dedication;
		case "pr":
			return ContentType.Preface;
		case "gl":
			return ContentType.Glossary;
		case "bi":
			return ContentType.Bibliography;
		default:
			return ContentType.Book;
		}
	}

	private long RenameHtmlFile(string htmlFile, string xmlFileName, string section)
	{
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		FileInfo fileInfo = new FileInfo(htmlFile);
		if (!fileInfo.Exists)
		{
			throw new Exception("File not found! '" + htmlFile + "'");
		}
		string newFileName = fileInfo.DirectoryName + "\\" + ((!string.IsNullOrWhiteSpace(section)) ? xmlFileName.Replace(".xml", "." + section + ".html") : xmlFileName.Replace(".xml", ".html"));
		if (newFileName == null)
		{
			throw new Exception("XML File extension not found! '" + htmlFile + "'");
		}
		if (newFileName.Length == htmlFile.Length)
		{
			throw new Exception("replace failed! '" + htmlFile + "' = '" + newFileName + "'");
		}
		fileInfo.MoveTo(newFileName);
		return stopwatch.ElapsedMilliseconds;
	}

	private int SaveAuthors(BookSearchInfo bookSearchInfo, int resourceId)
	{
		int order = 0;
		_resourceCoreDataService.DeleteResourceAuthors(resourceId, _r2UtilitiesSettings.AuthorTableName);
		Author primary = bookSearchInfo.PrimaryAuthor;
		if (primary != null)
		{
			if (string.IsNullOrWhiteSpace(primary.LastName))
			{
				_log.DebugFormat("empty primary author");
			}
			else
			{
				order++;
				_log.DebugFormat("Primary author: {0}", bookSearchInfo.PrimaryAuthor.GetFullName());
				_resourceCoreDataService.InsertAuthor(resourceId, order, bookSearchInfo.PrimaryAuthor, _r2UtilitiesSettings.AuthorTableName);
				if (order == 1)
				{
					string sortAuthor = bookSearchInfo.PrimaryAuthor.LastName + ", " + bookSearchInfo.PrimaryAuthor.FirstName;
					_log.DebugFormat("sortAuthor: {0}", sortAuthor);
					_resourceCoreDataService.UpdateResourceSortAuthor(resourceId, sortAuthor, "TransformService");
				}
			}
		}
		foreach (Author author in bookSearchInfo.OtherAuthors)
		{
			if (string.IsNullOrWhiteSpace(author.LastName))
			{
				_log.DebugFormat("empty author");
				continue;
			}
			if (primary != null && author.LastName == primary.LastName && author.FirstName == primary.FirstName && author.MiddleInitial == primary.MiddleInitial && author.Lineage == primary.Lineage && author.Degrees == primary.Degrees)
			{
				_log.DebugFormat("author same as primary");
				continue;
			}
			order++;
			_log.DebugFormat("other author: {0}", bookSearchInfo.PrimaryAuthor.GetFullName());
			_resourceCoreDataService.InsertAuthor(resourceId, order, author, _r2UtilitiesSettings.AuthorTableName);
			if (order == 1)
			{
				string sortAuthor2 = author.LastName + ", " + author.FirstName;
				_log.DebugFormat("sortAuthor: {0}", sortAuthor2);
				_resourceCoreDataService.UpdateResourceSortAuthor(resourceId, sortAuthor2, "TransformService");
			}
		}
		return order;
	}
}
