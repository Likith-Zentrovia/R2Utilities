using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Compression;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class TitleXmlService : R2UtilitiesBase
{
	private readonly IContentSettings _contentSettings;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ResourceCoreDataService _resourceCoreDataService;

	public TitleXmlService(IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings, ResourceCoreDataService resourceCoreDataService)
	{
		_contentSettings = contentSettings;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_resourceCoreDataService = resourceCoreDataService;
	}

	public TaskResultStep UpdateTitleXml(ResourceTitleChange resourceTitleChange, TaskResult taskResult, bool isTestMode)
	{
		R2UtilitiesBase.Log.Info(">+++> STEP - Update title xml for ISBN: " + resourceTitleChange.Isbn);
		TaskResultStep step = new TaskResultStep
		{
			Name = $"Update title xml for ISBN: {resourceTitleChange.Isbn}, Id: {resourceTitleChange.ResourceId}",
			StartTime = DateTime.Now
		};
		taskResult.AddStep(step);
		string workingFilePath = GetWorkingFilePath(resourceTitleChange.Isbn, isTestMode);
		StringBuilder errorMessage = new StringBuilder();
		List<string> warningMessages = new List<string>();
		int documentsUpdated = 0;
		bool revertChanges = false;
		ResourceBackup resourceBackup = null;
		try
		{
			resourceBackup = GetResourceBackup(warningMessages, resourceTitleChange, isTestMode);
			if (resourceBackup == null)
			{
				return step;
			}
			string titleFromXml = GetTitleFromXml(resourceBackup, resourceTitleChange);
			string subTitleFromXml = GetSubTitleFromXml(resourceBackup, resourceTitleChange, isRevert: false);
			if (string.IsNullOrWhiteSpace(titleFromXml))
			{
				errorMessage.Append("ERROR - No Title was parsed from the bookXml. The book.xml might not exist");
				R2UtilitiesBase.Log.InfoFormat(errorMessage.ToString());
				return step;
			}
			FileInfo[] files = resourceBackup.Xml.Files;
			foreach (FileInfo file in files)
			{
				if (ProcessFile(file, resourceTitleChange, titleFromXml, subTitleFromXml, workingFilePath, isTestMode) == 0 && !file.Name.Contains("preface") && !file.Name.Contains("appendix"))
				{
					errorMessage.Append("ERROR - failed to update title nodes in XML for : " + file.Name);
					R2UtilitiesBase.Log.InfoFormat(errorMessage.ToString());
					revertChanges = true;
					return step;
				}
				documentsUpdated++;
			}
			if (!isTestMode)
			{
				CopyNewFilesToContentLocation(workingFilePath, resourceBackup.Xml.ResourceDirectory.FullName);
				if (!_resourceCoreDataService.UpdateResourceTitle(resourceTitleChange, _r2UtilitiesSettings.R2UtilitiesDatabaseName))
				{
					errorMessage.Append("ERROR - failed to update title in database : " + resourceBackup.Xml.ResourceDirectory.Name);
					R2UtilitiesBase.Log.InfoFormat(errorMessage.ToString());
					revertChanges = true;
					return step;
				}
			}
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			errorMessage.Append("Exception - " + ex.Message);
		}
		finally
		{
			StringBuilder results = new StringBuilder();
			if (isTestMode)
			{
				results.Append("  TEST MODE!! ");
			}
			int documentsFound = 0;
			if (resourceBackup != null)
			{
				documentsFound = resourceBackup.Xml.Files.Length;
			}
			results.Append($"{documentsFound} total documents, {documentsUpdated} updated documents").AppendLine();
			if (errorMessage.Length != 0)
			{
				results.AppendLine(errorMessage.ToString());
			}
			foreach (string warningMessage in warningMessages)
			{
				results.AppendLine(warningMessage);
			}
			step.Results = results.ToString();
			step.HasWarnings = warningMessages.Count > 0;
			step.CompletedSuccessfully = errorMessage.Length == 0 && warningMessages.Count == 0;
			step.EndTime = DateTime.Now;
			if (!isTestMode && revertChanges)
			{
				RestoreResourceContentDirectory(resourceBackup);
			}
			if (!isTestMode && errorMessage.Length == 0)
			{
				Directory.Delete(workingFilePath, recursive: true);
			}
		}
		return step;
	}

	public string GetTitleFromXml(ResourceBackup resourceBackup, ResourceTitleChange resourceTitleChange)
	{
		FileInfo bookXmlFile = resourceBackup.Xml.Files.FirstOrDefault((FileInfo x) => x.Name.Contains("book") && !x.Name.Contains("comment"));
		if (bookXmlFile != null)
		{
			string text = File.ReadAllText(bookXmlFile.FullName);
			string title = null;
			Match match = Regex.Match(text, "(?i)<book[^>]*>\\s*<title>.*?<\\/title>");
			if (match.Success)
			{
				int indexOfTitleStart = match.Value.IndexOf("<title>", StringComparison.Ordinal) + 7;
				title = match.Value.Substring(indexOfTitleStart, match.Value.Length - (indexOfTitleStart + 8));
			}
			if (!string.IsNullOrWhiteSpace(title) && (title.Length > 255 || title.Contains("<") || title.Contains(">")))
			{
				R2UtilitiesBase.Log.ErrorFormat(">>> GetTitleFromXml - Title's Length > 255 or contains < > {0}", title);
				return null;
			}
			return title;
		}
		return null;
	}

	public string GetSubTitleFromXml(ResourceBackup resourceBackup, ResourceTitleChange resourceTitleChange, bool isRevert)
	{
		if (!isRevert && resourceTitleChange.UpdateType != ResourceTitleUpdateType.RittenhouseEqualR2TitleAndSub)
		{
			return null;
		}
		FileInfo bookXmlFile = resourceBackup.Xml.Files.FirstOrDefault((FileInfo x) => x.Name.Contains("book") && !x.Name.Contains("comment"));
		if (bookXmlFile != null)
		{
			string text = File.ReadAllText(bookXmlFile.FullName);
			string subTitle = null;
			Match match = Regex.Match(text, "(?i)<subtitle>.*?<\\/subtitle>");
			if (match.Success)
			{
				subTitle = match.Value.Replace("<subtitle>", "").Replace("</subtitle>", "").Replace("<emphasis>", "")
					.Replace("</emphasis>", "");
			}
			if (!string.IsNullOrWhiteSpace(subTitle) && (subTitle.Length > 255 || subTitle.Contains("<") || subTitle.Contains(">")))
			{
				R2UtilitiesBase.Log.ErrorFormat(">>> GetSubTitleFromXml - SubTitle's Length > 255 or contains < > {0}", subTitle);
				return null;
			}
			return subTitle;
		}
		return null;
	}

	public int RestoreXmlFiles(List<ResourceTitleChange> rittenhouseResourceTitles, bool isTestMode)
	{
		int resourcesRestored = 0;
		foreach (ResourceTitleChange rittenhouseResourceTitle in rittenhouseResourceTitles)
		{
			try
			{
				R2UtilitiesBase.Log.Info("Working on Title: " + rittenhouseResourceTitle.Isbn);
				ResourceBackup resourceBackup = new ResourceBackup(_contentSettings.ContentLocation, _r2UtilitiesSettings.UpdateTitleTaskXmlBackupLocation, rittenhouseResourceTitle.Isbn);
				RestoreResourceContentDirectory(resourceBackup);
				if (resourceBackup.BackupZipFile.Exists)
				{
					resourcesRestored++;
					if (string.IsNullOrWhiteSpace(rittenhouseResourceTitle.AlternateTitle) && !isTestMode)
					{
						string originalTitle = GetTitleFromXml(resourceBackup, rittenhouseResourceTitle);
						string originalSubTitle = GetSubTitleFromXml(resourceBackup, rittenhouseResourceTitle, isRevert: true);
						rittenhouseResourceTitle.AlternateTitle = originalTitle;
						rittenhouseResourceTitle.AlternateSubTitle = originalSubTitle;
						rittenhouseResourceTitle.IsRevert = true;
						_resourceCoreDataService.UpdateResourceTitle(rittenhouseResourceTitle, _r2UtilitiesSettings.R2UtilitiesDatabaseName);
					}
				}
				else
				{
					R2UtilitiesBase.Log.Info($"No backup File found for: {resourceBackup.BackupZipFile}");
				}
			}
			catch (Exception ex)
			{
				R2UtilitiesBase.Log.Error(ex.Message, ex);
			}
		}
		return resourcesRestored;
	}

	public void RestoreResourceContentDirectory(ResourceBackup backup)
	{
		if (!backup.BackupZipFile.Exists)
		{
			return;
		}
		string tempPath = Path.Combine(backup.BackupZipFile.DirectoryName, "temp");
		if (Directory.Exists(tempPath))
		{
			Directory.Delete(tempPath, recursive: true);
		}
		ZipHelper.ExtractAll(backup.BackupZipFile.FullName, tempPath);
		string[] test = Directory.GetFiles(Path.Combine(tempPath, "xml"));
		string[] array = test;
		foreach (string filePath in array)
		{
			FileInfo fileInfo = new FileInfo(filePath);
			if (!Directory.Exists(backup.Xml.ResourceDirectory.FullName))
			{
				Directory.CreateDirectory(backup.Xml.ResourceDirectory.FullName);
			}
			File.Copy(fileInfo.FullName, Path.Combine(backup.Xml.ResourceDirectory.FullName, fileInfo.Name), overwrite: true);
		}
		Directory.Delete(tempPath, recursive: true);
	}

	private void CopyNewFilesToContentLocation(string workingFilePath, string originalFileName)
	{
		string[] newFiles = Directory.GetFiles(workingFilePath);
		string[] array = newFiles;
		foreach (string newFile in array)
		{
			FileInfo test = new FileInfo(newFile);
			File.Copy(newFile, Path.Combine(originalFileName, test.Name), overwrite: true);
		}
	}

	private string GetWorkingFilePath(string isbn, bool isTestMode)
	{
		string workingFilePath = Path.Combine(_r2UtilitiesSettings.UpdateTitleTaskWorkingFolder, isbn);
		if (!isTestMode)
		{
			if (Directory.Exists(workingFilePath))
			{
				Directory.Delete(workingFilePath, recursive: true);
			}
			Directory.CreateDirectory(workingFilePath);
		}
		return workingFilePath;
	}

	private ResourceBackup GetResourceBackup(List<string> warnMessages, ResourceTitleChange resourceTitleChange, bool isTestMode)
	{
		ResourceBackup resourceBackup = new ResourceBackup(_contentSettings.ContentLocation, _r2UtilitiesSettings.UpdateTitleTaskXmlBackupLocation, resourceTitleChange.Isbn);
		if (!resourceBackup.Xml.ResourceDirectory.Exists)
		{
			warnMessages.Add("ERROR - directory does not exist: " + resourceBackup.Xml.ResourceDirectory.Name);
			R2UtilitiesBase.Log.InfoFormat(warnMessages.First());
			return null;
		}
		if (!isTestMode && !BackupDirectory(resourceBackup))
		{
			warnMessages.Add("ERROR - failed to backup directory: " + resourceBackup.Xml.ResourceDirectory.Name);
			R2UtilitiesBase.Log.InfoFormat(warnMessages.First());
			return null;
		}
		return resourceBackup;
	}

	private string ValidateAndReplaceText(int bracketCount, string foundValue, string replacementValue, string titleInXml)
	{
		int foundGreatCount = foundValue.Count((char x) => x == '>');
		int foundLessCount = foundValue.Count((char x) => x == '<');
		if (foundGreatCount != bracketCount || foundLessCount != bracketCount)
		{
			return null;
		}
		return foundValue.Replace(titleInXml, replacementValue);
	}

	private string UpdateTextInFile(string text, string pattern, string replacementValue, string valueToFind, int bracketCount, out int replacementCount)
	{
		int replaceCount = 0;
		text = Regex.Replace(text, pattern, delegate(Match m)
		{
			replaceCount++;
			string text2 = ValidateAndReplaceText(bracketCount, m.Value, replacementValue, valueToFind);
			if (string.IsNullOrWhiteSpace(text2))
			{
				R2UtilitiesBase.Log.Error("Failed to Validate bookMainTitle Found Text: " + m.Value);
				text = null;
			}
			return text2;
		});
		replacementCount = replaceCount;
		return text;
	}

	private int ProcessFile(FileInfo file, ResourceTitleChange resourceTitleChange, string titleToFind, string subTitleToFind, string workingLocation, bool isTestMode)
	{
		string text = File.ReadAllText(file.FullName);
		string newTitle = WebUtility.HtmlEncode(resourceTitleChange.GetNewTitle().Trim());
		int bytesTextOrig = GetByteCount(text);
		int bytesTitleOrig = GetByteCount(titleToFind);
		int bytesSubtitleOrig = GetByteCount(subTitleToFind);
		int bytesTitleNew = GetByteCount(newTitle);
		int bytesSubtitleNew = GetByteCount("");
		R2UtilitiesBase.Log.InfoFormat("File: {0}", file.Name);
		int changeCount = 0;
		string bookMainTitlePattern = "(?i)<book[^>]*>\\s*<title>" + Regex.Escape(titleToFind) + "<\\/title>";
		string bookTitlePattern = "(?i)<bookinfo>\\s*<title>" + Regex.Escape(titleToFind) + "<\\/title>";
		string bookContentPattern = "(?i)<booktitle>" + Regex.Escape(titleToFind) + "<\\/booktitle>";
		string bookTocPattern = "(?i)<\\/tocinfo>\\s*<title>" + Regex.Escape(titleToFind) + "<\\/title>";
		string subTitlePattern = "(?i)<subtitle>" + Regex.Escape(titleToFind) + "<\\/subtitle>";
		text = UpdateTextInFile(text, bookMainTitlePattern, newTitle, titleToFind, 3, out var bookMainTitleCount);
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}
		changeCount += bookMainTitleCount;
		text = UpdateTextInFile(text, bookTitlePattern, newTitle, titleToFind, 3, out var bookTitleCount);
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}
		changeCount += bookTitleCount;
		text = UpdateTextInFile(text, bookContentPattern, newTitle, titleToFind, 2, out var bookContentCount);
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}
		changeCount += bookContentCount;
		text = UpdateTextInFile(text, bookTocPattern, newTitle, titleToFind, 3, out var bookTocCount);
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}
		changeCount += bookTocCount;
		text = UpdateTextInFile(text, subTitlePattern, newTitle, titleToFind, 2, out var subTitleCount);
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}
		changeCount += subTitleCount;
		R2UtilitiesBase.Log.Info($"Total change Count: {changeCount} bookMainTitle Count: {bookMainTitleCount} | bookTitle Count: {bookTitleCount} | bookContent Count: {bookContentCount} | bookToc Count: {bookTocCount} | subTitle Count: {subTitleCount}");
		int bytesTextNew = GetByteCount(text);
		int matchCountBook = bookMainTitleCount + bookTitleCount + bookContentCount + bookTocCount;
		int matchCountSubtitle = subTitleCount;
		int bytesPredicted = PredictByteCount(bytesTextOrig, bytesTitleNew, bytesTitleOrig, bytesSubtitleNew, bytesSubtitleOrig, matchCountBook, matchCountSubtitle);
		if (bytesPredicted != bytesTextNew)
		{
			R2UtilitiesBase.Log.ErrorFormat($"Byte Count Validation Failed - ISBN:{resourceTitleChange.Isbn}, Predicted Byte Count:{bytesPredicted}, Actual Byte Count:{bytesTextNew}, FileName: {file.Name}");
			return 0;
		}
		if (!isTestMode)
		{
			if (!Directory.Exists(workingLocation))
			{
				Directory.CreateDirectory(workingLocation);
			}
			string newFileLocation = Path.Combine(workingLocation, file.Name);
			File.WriteAllText(newFileLocation, text, Encoding.UTF8);
		}
		return changeCount;
	}

	private bool BackupDirectory(ResourceBackup resourceBackup)
	{
		try
		{
			FileInfo zipFileInfo = new FileInfo(resourceBackup.BackupZipFile.FullName);
			if (zipFileInfo.Exists)
			{
				zipFileInfo.Delete();
			}
			CompressResourceContentDirectory(resourceBackup.Xml, resourceBackup.BackupZipFile.FullName);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
		}
		return true;
	}

	private static int GetByteCount(string s)
	{
		Encoding encoding = Encoding.UTF8;
		return (s != null) ? encoding.GetByteCount(s) : 0;
	}

	private static int PredictByteCount(int bytesTextOrig, int bytesTitleNew, int bytesTitleOrig, int bytesSubtitleNew, int bytesSubtitleOrig, int matchCountBook, int matchCountSubtitle)
	{
		int bytesTitleDelta = bytesTitleNew - bytesTitleOrig;
		int bytesSubtitleDelta = bytesSubtitleNew - bytesSubtitleOrig;
		int bytesDelta = matchCountBook * bytesTitleDelta + matchCountSubtitle * bytesSubtitleDelta;
		return bytesTextOrig + bytesDelta;
	}
}
