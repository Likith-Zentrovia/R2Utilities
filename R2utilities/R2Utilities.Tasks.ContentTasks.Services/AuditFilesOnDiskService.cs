using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using log4net;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Compression;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class AuditFilesOnDiskService
{
	private const string RootNodeTocFront = "tocfront";

	private const string RootNodeTocChapter = "tocchap";

	private const string RootNodeTocBack = "tocback";

	private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);

	private readonly IContentSettings _contentSettings;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly TransformQueueDataService _transformQueueDataService;

	public List<ResourceCore> ResourcesWithoutTocXml { get; }

	public List<ResourceCore> ValidatedResources { get; }

	public List<ResourceCore> ErrorResources { get; }

	public List<ResourceCore> ResourcesWithExtraContent { get; }

	public List<ResourceCore> ResourcesMissingContent { get; }

	public List<string> FilesToDelete { get; }

	public List<string> MissingFiles { get; }

	public int BookXmlFilesConfirmed { get; private set; }

	public List<AuditFilesOnDiskResult> AuditFilesOnDiskResults { get; }

	public AuditFilesOnDiskService(IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_contentSettings = contentSettings;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_transformQueueDataService = new TransformQueueDataService();
		ResourcesWithoutTocXml = new List<ResourceCore>();
		ValidatedResources = new List<ResourceCore>();
		ErrorResources = new List<ResourceCore>();
		ResourcesWithExtraContent = new List<ResourceCore>();
		ResourcesMissingContent = new List<ResourceCore>();
		FilesToDelete = new List<string>();
		MissingFiles = new List<string>();
		BookXmlFilesConfirmed = 0;
		AuditFilesOnDiskResults = new List<AuditFilesOnDiskResult>();
	}

	public AuditFilesOnDiskResult AuditFilesOnDisk(ResourceCore resourceCore)
	{
		AuditFilesOnDiskResult result = new AuditFilesOnDiskResult
		{
			ResourceCore = resourceCore
		};
		AuditFilesOnDiskResults.Add(result);
		try
		{
			string resourceXmlDirectory = Path.Combine(_contentSettings.ContentLocation, resourceCore.Isbn);
			Log.DebugFormat("resourceXmlDirectory: {0}", resourceXmlDirectory);
			string tocXml = Path.Combine(resourceXmlDirectory, "toc." + resourceCore.Isbn + ".xml");
			Log.DebugFormat("tocXml: {0}", tocXml);
			FileInfo fileInfo = new FileInfo(tocXml);
			if (!fileInfo.Exists)
			{
				ResourcesWithoutTocXml.Add(resourceCore);
				result.ResourcesWithoutTocXml = true;
				return result;
			}
			List<string> xmlFiles = new List<string>();
			DateTime tocModifiedDate = fileInfo.LastWriteTime;
			XmlDocument xmlDoc = new XmlDocument
			{
				PreserveWhitespace = false,
				XmlResolver = null
			};
			xmlDoc.Load(tocXml);
			XmlNodeList tocFrontNodes = xmlDoc.GetElementsByTagName("tocfront");
			Log.DebugFormat("tocFrontNodes.Count: {0}", tocFrontNodes.Count);
			foreach (XmlNode tocFrontNode in tocFrontNodes)
			{
				if (tocFrontNode.Attributes != null)
				{
					XmlAttribute attribute = tocFrontNode.Attributes["linkend"];
					if (!string.IsNullOrWhiteSpace(attribute.Value))
					{
						string xmlFilename = BuildXmlFilename(resourceCore.Isbn, attribute.Value, "tocfront");
						xmlFiles.Add(xmlFilename);
					}
				}
			}
			XmlNodeList tocChapNodes = xmlDoc.GetElementsByTagName("tocchap");
			Log.DebugFormat("tocChapNodes.Count: {0}", tocChapNodes.Count);
			foreach (XmlNode tocChapNode in tocChapNodes)
			{
				foreach (XmlNode childNode in tocChapNode.ChildNodes)
				{
					if (childNode.Name.Equals("toclevel1", StringComparison.OrdinalIgnoreCase))
					{
						XmlNode tocentry = childNode.FirstChild;
						if (tocentry.Attributes != null)
						{
							XmlAttribute attribute2 = tocentry.Attributes["linkend"];
							string xmlFilename2 = BuildXmlFilename(resourceCore.Isbn, attribute2.Value, "tocchap");
							xmlFiles.Add(xmlFilename2);
						}
					}
				}
			}
			XmlNodeList tocBackNodes = xmlDoc.GetElementsByTagName("tocback");
			Log.DebugFormat("tocBackNodes.Count: {0}", tocBackNodes.Count);
			foreach (XmlNode tocBackNode in tocBackNodes)
			{
				if (tocBackNode.Attributes != null && tocBackNode.Attributes["linkend"] != null)
				{
					XmlAttribute attribute3 = tocBackNode.Attributes["linkend"];
					string xmlFilename3 = BuildXmlFilename(resourceCore.Isbn, attribute3.Value, "tocback");
					xmlFiles.Add(xmlFilename3);
				}
				foreach (XmlNode childNode2 in tocBackNode.ChildNodes)
				{
					if (childNode2.Name.Equals("tocentry", StringComparison.OrdinalIgnoreCase) && childNode2.Attributes != null)
					{
						XmlAttribute attribute4 = childNode2.Attributes["linkend"];
						string xmlFilename4 = BuildXmlFilename(resourceCore.Isbn, attribute4.Value, "tocback");
						xmlFiles.Add(xmlFilename4);
					}
				}
			}
			VerifyFiles(result, resourceXmlDirectory, xmlFiles, tocModifiedDate);
			if (result.ContainsExtraXmlFile)
			{
				ResourcesWithExtraContent.Add(resourceCore);
			}
			else if (result.MissingXmlFile)
			{
				ResourcesMissingContent.Add(resourceCore);
			}
			else
			{
				ValidatedResources.Add(resourceCore);
			}
		}
		catch (Exception ex)
		{
			ErrorResources.Add(resourceCore);
			Log.Error(ex.Message, ex);
			result.ExceptionWhileProcessing = true;
			result.ExceptionMessage = ex.Message;
			result.ExceptionStackTrace = ex.StackTrace;
		}
		return result;
	}

	private void VerifyFiles(AuditFilesOnDiskResult result, string contentDirectoryPath, List<string> filesFromTocXml, DateTime tocModifiedDate)
	{
		string[] xmlFilesOnDisk = Directory.GetFiles(contentDirectoryPath);
		result.FilesReferencedInTocXmlCount = filesFromTocXml.Count;
		result.FilesOnDiskCount = xmlFilesOnDisk.Length;
		HashSet<string> tocXmlHashSet = new HashSet<string>(filesFromTocXml);
		string[] array = xmlFilesOnDisk;
		foreach (string xmlFilePath in array)
		{
			if (!xmlFilePath.Contains("\\toc.") && !xmlFilePath.Contains("\\book.") && !tocXmlHashSet.Contains(Path.GetFileName(xmlFilePath)))
			{
				Log.WarnFormat("File not in toc.xml: {0}", xmlFilePath);
				result.FilesNotInTocXmlCount++;
				DateTime xmlFileDateModified = new FileInfo(xmlFilePath).LastWriteTime;
				if (IsDateWindowExceeded(xmlFileDateModified, tocModifiedDate))
				{
					Log.WarnFormat("\tFile dates out of sync - Toc DateModified: {0}, Xml DateModified: {1}", tocModifiedDate, xmlFileDateModified);
					result.FilesDateDiffersFromTocCount++;
					FilesToDelete.Add(xmlFilePath);
					result.FilesToDeleteCount++;
				}
				else
				{
					Log.Info("\tFile date in sync with Toc");
				}
			}
		}
		HashSet<string> xmlFileNamesOnDiskHashSet = new HashSet<string>(xmlFilesOnDisk.Select(Path.GetFileName));
		foreach (string xmlFileName in filesFromTocXml)
		{
			if (!xmlFileNamesOnDiskHashSet.Contains(xmlFileName))
			{
				Log.WarnFormat("File not on disk: {0}", xmlFileName);
				result.FilesNotOnDiskCount++;
				MissingFiles.Add(xmlFileName);
			}
			else
			{
				result.FilesConfirmedInTocXmlCount++;
			}
		}
		BookXmlFilesConfirmed += result.FilesConfirmedInTocXmlCount;
		Log.InfoFormat("FilesNotInTocXmlCount: {0}, FilesDateDiffersFromTocCount: {1}, FilesToDeleteCount: {2}, FilesNotOnDiskCount: {3}, FilesConfirmedInTocXmlCount: {4}", result.FilesNotInTocXmlCount, result.FilesDateDiffersFromTocCount, result.FilesToDeleteCount, result.FilesNotOnDiskCount, result.FilesConfirmedInTocXmlCount);
	}

	private bool IsDateWindowExceeded(DateTime xmlFileDateModified, DateTime tocDateModified)
	{
		DateTime startTime;
		DateTime endTime;
		if (xmlFileDateModified < tocDateModified)
		{
			startTime = xmlFileDateModified;
			endTime = tocDateModified;
		}
		else
		{
			startTime = tocDateModified;
			endTime = xmlFileDateModified;
		}
		TimeSpan duration = endTime.Subtract(startTime);
		TimeSpan xmlDateModifiedWindow = new TimeSpan(0, 0, 0, _r2UtilitiesSettings.AuditXmlDateModifiedWindowInSeconds);
		return duration > xmlDateModifiedWindow;
	}

	public int BackupResourcesWithExtraContent()
	{
		int resourcesBackedUp = 0;
		foreach (ResourceCore resourceCore in ResourcesWithExtraContent)
		{
			try
			{
				string resourceXmlDirectory = Path.Combine(_contentSettings.ContentLocation, resourceCore.Isbn);
				string zipFileName = Path.Combine(_r2UtilitiesSettings.AuditFilesOnDiskBackupDirectory, resourceCore.Isbn + ".zip");
				ZipHelper.CompressDirectory(resourceXmlDirectory, zipFileName);
				_transformQueueDataService.Insert(resourceCore.Id, resourceCore.Isbn, "A");
				resourcesBackedUp++;
			}
			catch (Exception ex)
			{
				Log.WarnFormat("Error backing up / adding to transform queue - {0}", resourceCore.ToDebugString());
				Log.Error(ex.Message, ex);
			}
		}
		return resourcesBackedUp;
	}

	public int DeleteResourceFilesNotInBookXml()
	{
		int filesDeleted = 0;
		foreach (string fileToDelete in FilesToDelete)
		{
			try
			{
				Log.DebugFormat("deleting file: {0}", fileToDelete);
				File.Delete(fileToDelete);
				filesDeleted++;
			}
			catch (Exception ex)
			{
				Log.WarnFormat("Error deleting file: {0}", fileToDelete);
				Log.Error(ex.Message, ex);
			}
		}
		return filesDeleted;
	}

	public int ResourceBackups(string isbns)
	{
		string[] limitByIsbns = ((isbns == null) ? new string[0] : isbns.Split(','));
		List<FileInfo> zipFilesToRestore = new List<FileInfo>();
		string[] backupZipFiles = Directory.GetFiles(_r2UtilitiesSettings.AuditFilesOnDiskBackupDirectory, "*.zip");
		string[] array = backupZipFiles;
		foreach (string backupZipFile in array)
		{
			Log.DebugFormat("backupZipFile: {0}", backupZipFile);
			FileInfo fileInfo = new FileInfo(backupZipFile);
			string isbn = GetIsbnFromZipFileInfo(fileInfo);
			if (limitByIsbns.Length != 0)
			{
				if (limitByIsbns.Contains(isbn))
				{
					zipFilesToRestore.Add(fileInfo);
				}
			}
			else
			{
				zipFilesToRestore.Add(fileInfo);
			}
		}
		int resourcesRestored = 0;
		foreach (FileInfo zipFileInfo in zipFilesToRestore)
		{
			Log.InfoFormat("Restoring backup {0} of {1} - {2}", resourcesRestored + 1, zipFilesToRestore.Count, zipFileInfo.Name);
			string isbn2 = GetIsbnFromZipFileInfo(zipFileInfo);
			string resourceXmlDirectory = Path.Combine(_contentSettings.ContentLocation, isbn2);
			ZipHelper.ExtractAll(zipFileInfo.FullName, resourceXmlDirectory, overwrite: true);
			resourcesRestored++;
		}
		return resourcesRestored;
	}

	private string GetIsbnFromZipFileInfo(FileInfo zipFileInfo)
	{
		return zipFileInfo.Name.Replace(".zip", "");
	}

	public string BuildXmlFilename(string isbn, string linkend, string rootNodeName)
	{
		if (string.IsNullOrWhiteSpace(linkend) || linkend.Length < 2)
		{
			Log.ErrorFormat("linkend is null, empty, whitespace, less than 2 characters - linkend: '{0}'", linkend);
			return "book." + isbn + ".xml";
		}
		switch (linkend.Substring(0, 2))
		{
		case "pr":
			if (rootNodeName.Equals("tocfront"))
			{
				return "preface." + isbn + "." + linkend + ".xml";
			}
			if (rootNodeName.Equals("tocchap"))
			{
				return "sect1." + isbn + "." + linkend + ".xml";
			}
			Log.ErrorFormat("'pr' PREFIX NOT SUPPORTED for root node name: '{2}' - isbn: {0}, linked: {1}", isbn, linkend, rootNodeName);
			return "_missing." + isbn + "." + linkend + ".xml";
		case "dd":
			return "dedication." + isbn + "." + linkend + ".xml";
		case "ap":
			if (linkend.Length <= 7)
			{
				return "appendix." + isbn + "." + linkend + ".xml";
			}
			if (linkend.Length > 7)
			{
				return "sect1." + isbn + "." + linkend + ".xml";
			}
			Log.ErrorFormat("'ap' PREFIX NOT SUPPORTED for root node name: '{2}' - isbn: {0}, linked: {1}", isbn, linkend, rootNodeName);
			return "_missing." + isbn + "." + linkend + ".xml";
		case "ch":
		case "pt":
		case "s0":
		case "ci":
		case "p2":
		case "s2":
			return "sect1." + isbn + "." + linkend + ".xml";
		default:
			if (linkend.StartsWith("bibs", StringComparison.InvariantCultureIgnoreCase) || linkend.StartsWith("PTE", StringComparison.InvariantCultureIgnoreCase))
			{
				return "sect1." + isbn + "." + linkend + ".xml";
			}
			if (linkend.StartsWith("ded", StringComparison.InvariantCultureIgnoreCase))
			{
				return "dedication." + isbn + "." + linkend + ".xml";
			}
			if (linkend.StartsWith("glossary", StringComparison.InvariantCultureIgnoreCase) || linkend.StartsWith("gl", StringComparison.InvariantCultureIgnoreCase) || linkend.StartsWith("in", StringComparison.InvariantCultureIgnoreCase) || linkend.StartsWith("biblio", StringComparison.InvariantCultureIgnoreCase) || linkend.StartsWith("bibli", StringComparison.InvariantCultureIgnoreCase) || linkend.StartsWith("bib", StringComparison.InvariantCultureIgnoreCase))
			{
				Log.InfoFormat("Resolve to book.xml - isbn: {0}, linked: {1}", isbn, linkend);
				return "book." + isbn + ".xml";
			}
			Log.ErrorFormat("PREFIX NOT SUPPORTED for root node name: '{2}' - isbn: {0}, linked: {1}", isbn, linkend, rootNodeName);
			return "_missing." + isbn + "." + linkend + ".xml";
		}
	}
}
