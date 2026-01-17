using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using R2Utilities.DataAccess;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class ResourceContentStatus
{
	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public string Isbn { get; private set; }

	public ResourceStatus ResourceStatus { get; private set; }

	public bool IsSoftDeleted { get; private set; }

	public string XmlDirectoryPath { get; }

	public string HtmlDirectoryPath { get; }

	public ResourceContentStatusType Status { get; set; }

	public List<string> StatusMessages { get; set; }

	public int XmlFileCount { get; set; }

	public int HtmlFileCount { get; set; }

	public long XmlDirectorySizeInBytes { get; set; }

	public long HtmlDirectorySizeInBytes { get; set; }

	public ResourceContentStatus(ResourceCore resourceCore, IContentSettings contentSettings)
	{
		try
		{
			Status = ResourceContentStatusType.Unknown;
			StatusMessages = new List<string>();
			Isbn = resourceCore.Isbn;
			XmlDirectoryPath = contentSettings.ContentLocation + "\\" + resourceCore.Isbn + "\\";
			HtmlDirectoryPath = contentSettings.NewContentLocation + "\\Html\\" + resourceCore.Isbn + "\\";
			ResourceStatus = (ResourceStatus)resourceCore.StatusId;
			IsSoftDeleted = resourceCore.RecordStatus == 0;
			DirectoryInfo xmlDirectoryInfo = new DirectoryInfo(XmlDirectoryPath);
			if (!xmlDirectoryInfo.Exists)
			{
				Status = ResourceContentStatusType.XmlDirectoryDoesNotExist;
				StatusMessages.Add("XML Directory does not exist: " + XmlDirectoryPath);
				return;
			}
			FileInfo[] xmlFiles = xmlDirectoryInfo.GetFiles();
			XmlFileCount = xmlFiles.Length;
			if (xmlFiles.Length == 0)
			{
				Status = ResourceContentStatusType.XmlDirectoryIsEmpty;
				StatusMessages.Add("XML Directory is empty: " + XmlDirectoryPath);
				return;
			}
			DirectoryInfo htmlDirectoryInfo = new DirectoryInfo(HtmlDirectoryPath);
			if (!htmlDirectoryInfo.Exists)
			{
				Status = ResourceContentStatusType.HtmlDirectoryDoesNotExist;
				StatusMessages.Add("HTML Directory does not exist: " + HtmlDirectoryPath);
				return;
			}
			FileInfo[] htmlFiles = htmlDirectoryInfo.GetFiles();
			HtmlFileCount = htmlFiles.Length;
			if (htmlFiles.Length == 0)
			{
				Status = ResourceContentStatusType.HtmlDirectoryIsEmpty;
				StatusMessages.Add("HTML Directory is empty: " + HtmlDirectoryPath);
				return;
			}
			FileInfo[] array = xmlFiles;
			foreach (FileInfo xmlFile in array)
			{
				XmlDirectorySizeInBytes += xmlFile.Length;
				if (!xmlFile.Name.StartsWith("book.") && !xmlFile.Name.StartsWith("toc."))
				{
					string htmlFilename = xmlFile.Name.Replace(".xml", ".html");
					if (!File.Exists(HtmlDirectoryPath + htmlFilename))
					{
						Status = ResourceContentStatusType.MissingHtmlFiles;
						StatusMessages.Add("Missing HTML file: " + htmlFilename);
					}
				}
			}
			if (StatusMessages.Count > 0)
			{
				return;
			}
			FileInfo[] array2 = htmlFiles;
			foreach (FileInfo htmlFile in array2)
			{
				HtmlDirectorySizeInBytes += htmlFile.Length;
				if (!htmlFile.Name.StartsWith("r2BookSearch", StringComparison.OrdinalIgnoreCase) && !htmlFile.Name.Contains("glossary"))
				{
					string xmlFilename = htmlFile.Name.Replace(".html", ".xml");
					if (!File.Exists(XmlDirectoryPath + xmlFilename))
					{
						Status = ResourceContentStatusType.MissingXmlFiles;
						StatusMessages.Add("Missing XML file: " + xmlFilename);
					}
				}
			}
			if (StatusMessages.Count == 0)
			{
				Status = ResourceContentStatusType.XmlAndHtmlOk;
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex.Message, ex);
			Status = ResourceContentStatusType.Exception;
			StatusMessages.Add("EXCEPTION: " + ex.Message);
		}
	}

	public void ValidateResourceInIndex(DocIds docIds)
	{
		if (Status == ResourceContentStatusType.HtmlDirectoryDoesNotExist || Status == ResourceContentStatusType.HtmlDirectoryIsEmpty || Status == ResourceContentStatusType.XmlDirectoryDoesNotExist || Status == ResourceContentStatusType.XmlDirectoryIsEmpty)
		{
			return;
		}
		foreach (DocIdFilename pair in docIds.Filenames)
		{
			string filename = pair.Name;
			string xmlFilePath = XmlDirectoryPath + filename.Replace(".html", ".xml");
			string htmlFilePath = HtmlDirectoryPath + filename;
			if (!File.Exists(htmlFilePath))
			{
				Log.WarnFormat("HTML file is missing: {0}", htmlFilePath);
				StatusMessages.Add("HTML file is missing: " + htmlFilePath);
				Status = ResourceContentStatusType.IndexContainsMissingFiles;
			}
			if (!File.Exists(htmlFilePath))
			{
				Log.WarnFormat("XML file is missing: {0}", xmlFilePath);
				StatusMessages.Add("XML file is missing: " + xmlFilePath);
				Status = ResourceContentStatusType.IndexContainsMissingFiles;
			}
		}
		string[] htmlFiles = Directory.GetFiles(HtmlDirectoryPath);
		string[] array = htmlFiles;
		foreach (string htmlFile in array)
		{
			string[] filePathParts = htmlFile.Split('\\');
			string filename2 = filePathParts.Last();
			if (!docIds.Filenames.Exists((DocIdFilename x) => x.Name == filename2))
			{
				Log.WarnFormat("HTML file not in index: {0}", filename2);
				StatusMessages.Add("HTML file not in index: " + filename2);
				Status = ResourceContentStatusType.HtmlFilesNotInIndex;
			}
		}
	}
}
