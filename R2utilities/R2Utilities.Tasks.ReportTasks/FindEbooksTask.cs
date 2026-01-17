using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;
using Newtonsoft.Json;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Email.EmailBuilders;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Export.FileTypes;
using R2V2.Core.R2Utilities;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks.ReportTasks;

public class FindEbooksTask : EmailTaskBase
{
	private readonly FindEBookEmailBuildService _emailBuildService;

	private readonly EmailTaskService _emailTaskService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private DateTime _filesAfterDate;

	public FindEbooksTask(IR2UtilitiesSettings r2UtilitiesSettings, EmailTaskService emailTaskService, FindEBookEmailBuildService emailBuildService)
		: base("FindEbooksTask", "-FindEbooksTask", "56", TaskGroup.CustomerEmails, "Sends an email with all new eBooks found on the file system", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_emailTaskService = emailTaskService;
		_emailBuildService = emailBuildService;
	}

	public override void Run()
	{
		TaskResultStep step = new TaskResultStep
		{
			Name = "FindEbooksTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			_filesAfterDate = DateTime.Now.AddDays(-_r2UtilitiesSettings.FindEbookDaysAgo);
			string[] fileExtensionArray = _r2UtilitiesSettings.FindEbookFileExtensions.Split(',');
			List<string> extensions = new List<string>();
			extensions.AddRange(fileExtensionArray);
			string[] excludedFolderArray = _r2UtilitiesSettings.FindEbookFileExcludedFolders.Split(',');
			List<string> excludedFolders = new List<string>();
			excludedFolders.AddRange(excludedFolderArray);
			EBookReport report = GetFilesWithExtensions3(extensions, excludedFolders);
			BuildAndSendEmail(report);
			step.CompletedSuccessfully = true;
			step.Results = "Done";
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

	private void BuildAndSendEmail(EBookReport report)
	{
		_emailBuildService.InitEmailTemplates();
		string[] emailArray = _r2UtilitiesSettings.FindEbookRecipients.Split(';');
		EmailMessage emailMessage = _emailBuildService.BuildEmail(report, emailArray);
		EBookFilesExcelExport2 excelExport2 = new EBookFilesExcelExport2();
		MemoryStream excelStream = excelExport2.CreateExcelWorkbook(report);
		ContentType contentType = new ContentType
		{
			Name = "R2_eBook_Report" + DateTime.Now.ToShortDateString() + ".xlsx"
		};
		Attachment attachment = new Attachment(excelStream, contentType)
		{
			ContentType = 
			{
				MediaType = excelExport2.MimeType
			}
		};
		emailMessage.ExcelAttachment = attachment;
		EmailDeliveryService.SendCustomerTaskEmail(emailMessage, _r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName);
	}

	private EBookReport GetFilesWithExtensions3(List<string> extensions, List<string> excludedFolders)
	{
		List<IResource> resources = _emailTaskService.GetResources();
		EBookReport report = new EBookReport
		{
			StartDate = _filesAfterDate,
			EndDate = DateTime.Now,
			PublisherFiles = new List<EBookPublisher>()
		};
		try
		{
			IEnumerable<string> files = from text in Directory.GetFiles(_r2UtilitiesSettings.FindEbookRootFileLocation, "*.*", SearchOption.AllDirectories)
				where extensions.Any((string ext) => text.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) && !excludedFolders.Any((string excluded) => text.StartsWith(Path.Combine(_r2UtilitiesSettings.FindEbookRootFileLocation, excluded), StringComparison.OrdinalIgnoreCase))
				select text;
			foreach (string file in files)
			{
				bool addPublisher = false;
				FileInfo fi = new FileInfo(file);
				if (!(fi.CreationTime > _filesAfterDate))
				{
					continue;
				}
				string path = fi.DirectoryName.Replace(_r2UtilitiesSettings.FindEbookRootFileLocation, "").Substring(1);
				string publisher = path.Split('\\').FirstOrDefault();
				EBookPublisher foundPublisher = report.PublisherFiles.Find((EBookPublisher x) => x.Publisher == publisher);
				string name = fi.Name.Split('.').FirstOrDefault();
				if (foundPublisher == null)
				{
					foundPublisher = new EBookPublisher
					{
						Publisher = publisher,
						Files = new List<EBookFile>()
					};
					addPublisher = true;
				}
				bool isIsbn = false;
				if (IsValidISBN(name))
				{
					isIsbn = true;
					if (name.Length == 10)
					{
						IResource resource = resources.Find((IResource x) => x.Isbn10 != null && x.Isbn10.Equals(name, StringComparison.InvariantCultureIgnoreCase));
						if (resource != null)
						{
							continue;
						}
					}
					if (name.Length == 13)
					{
						IResource resource2 = resources.Find((IResource x) => (x.Isbn13 != null && x.Isbn13.Equals(name, StringComparison.InvariantCultureIgnoreCase)) || (x.EIsbn != null && x.EIsbn.Equals(name, StringComparison.InvariantCultureIgnoreCase)));
						if (resource2 != null)
						{
							continue;
						}
					}
				}
				bool isNewFile = false;
				EBookFile eBookFile = foundPublisher.Files.Find((EBookFile x) => x.Name == name);
				if (eBookFile == null)
				{
					isNewFile = true;
					eBookFile = new EBookFile
					{
						FileName = fi.Name,
						Path = path,
						CreateTime = fi.CreationTime,
						Folder = publisher,
						Name = name,
						Extensions = new List<string> { fi.Extension },
						Paths = new List<string> { path },
						NameAsIsbn = isIsbn
					};
				}
				else
				{
					eBookFile.Extensions.Add(fi.Extension);
					eBookFile.Paths.Add(path);
				}
				if (isIsbn)
				{
					eBookFile.Details = GetEBookDetails(name);
					Thread.Sleep(350);
				}
				if (isNewFile)
				{
					foundPublisher.Files.Add(eBookFile);
				}
				if (addPublisher)
				{
					report.PublisherFiles.Add(foundPublisher);
				}
			}
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error("Error accessing directory: " + ex.Message);
		}
		report.PublisherCount = report.PublisherFiles.Count;
		report.TitleCount = report.PublisherFiles.Sum((EBookPublisher x) => x.Files.Count);
		report.PublisherFiles.ForEach(delegate(EBookPublisher x)
		{
			x.FileCount = x.Files.Count;
			x.Files = x.Files.OrderByDescending((EBookFile y) => y.CreateTime).ToList();
		});
		return report;
	}

	private EBookDetails GetEBookDetails(string isbn)
	{
		string url = _r2UtilitiesSettings.FindEbookUrl + isbn;
		string token = _r2UtilitiesSettings.FindEbookIsbnDbKey;
		using (HttpClient client = new HttpClient())
		{
			client.DefaultRequestHeaders.Add("Authorization", token);
			HttpResponseMessage response = client.GetAsync(url).Result;
			if (response.IsSuccessStatusCode)
			{
				string jsonResponse = response.Content.ReadAsStringAsync().Result;
				EBookDetailsRoot details = JsonConvert.DeserializeObject<EBookDetailsRoot>(jsonResponse);
				return details.Book;
			}
			R2UtilitiesBase.Log.Warn($"Error: {response.StatusCode}");
		}
		return null;
	}

	private static bool IsValidISBN(string isbn)
	{
		string cleanedISBN = isbn.Replace("-", "").Replace(" ", "");
		return IsValidISBN10(cleanedISBN) || IsValidISBN13(cleanedISBN);
	}

	private static bool IsValidISBN10(string isbn)
	{
		if (isbn.Length != 10)
		{
			return false;
		}
		int sum = 0;
		for (int i = 0; i < 9; i++)
		{
			if (!char.IsDigit(isbn[i]))
			{
				return false;
			}
			sum += (i + 1) * (isbn[i] - 48);
		}
		char lastChar = isbn[9];
		if (lastChar == 'X')
		{
			sum += 100;
		}
		else
		{
			if (!char.IsDigit(lastChar))
			{
				return false;
			}
			sum += 10 * (lastChar - 48);
		}
		return sum % 11 == 0;
	}

	private static bool IsValidISBN13(string isbn)
	{
		if (isbn.Length != 13 || !long.TryParse(isbn, out var _))
		{
			return false;
		}
		int sum = 0;
		for (int i = 0; i < 13; i++)
		{
			int digit = isbn[i] - 48;
			sum += ((i % 2 == 0) ? digit : (digit * 3));
		}
		return sum % 10 == 0;
	}
}
