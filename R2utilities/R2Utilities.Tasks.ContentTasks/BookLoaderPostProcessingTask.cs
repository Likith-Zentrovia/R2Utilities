using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using R2Library.Data.ADO.R2;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.Services;
using R2Utilities.Tasks.ContentTasks.Xsl;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class BookLoaderPostProcessingTask : TaskBase
{
	private readonly IContentSettings _contentSettings;

	private readonly LicensingDataService _licensingDataService;

	private readonly ILog<BookLoaderPostProcessingTask> _log;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ResourceCoreDataService _resourceCoreDataService;

	private readonly ResourcePracticeAreaDataService _resourcePracticeAreaDataService;

	private readonly ResourceSpecialtyDataService _resourceSpecialtyDataService;

	private readonly TocXmlService _tocXmlService;

	private readonly TransformXmlService _transformXmlService;

	private bool _includeChapterNumbersInToc;

	private string _isbn;

	public BookLoaderPostProcessingTask(ILog<BookLoaderPostProcessingTask> log, IR2UtilitiesSettings r2UtilitiesSettings, IContentSettings contentSettings, TransformXmlService transformXmlService, LicensingDataService licensingDataService, TocXmlService tocXmlService)
		: base("BookLoaderPostProcessingTask", "-BookLoaderPostProcessingTask", "02", TaskGroup.ContentLoading, "Book loading task to be run after the Java based book loader", enabled: true)
	{
		_log = log;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_contentSettings = contentSettings;
		_transformXmlService = transformXmlService;
		_licensingDataService = licensingDataService;
		_tocXmlService = tocXmlService;
		_resourceCoreDataService = new ResourceCoreDataService();
		_resourceSpecialtyDataService = new ResourceSpecialtyDataService();
		_resourcePracticeAreaDataService = new ResourcePracticeAreaDataService();
	}

	public override void Run()
	{
		_isbn = GetArgument("isbn");
		_includeChapterNumbersInToc = GetArgumentBoolean("includeChapterNumbersInToc", defaultValue: false);
		_log.DebugFormat("processing ISBN: {0}, _includeChapterNumbersInToc: {1}", _isbn, _includeChapterNumbersInToc);
		base.TaskResult.Information = string.Format("Validation URL: <a href=\"{0}{1}\">{0}{1}</a>", _r2UtilitiesSettings.ResourceValidationBaseUrl, _isbn);
		TaskResultStep step = new TaskResultStep
		{
			Name = "Book Loader Post Processing for ISBN: " + _isbn,
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		base.EmailSubject = "ISBN: " + _isbn;
		try
		{
			ResourceCore resource = _resourceCoreDataService.GetResourceByIsbn(_isbn, excludeForthcoming: false);
			if (resource == null || resource.Id <= 0)
			{
				step.Results = "Resource not found by ISBN: " + _isbn;
				step.CompletedSuccessfully = false;
				_log.Error(step.Results);
				throw new Exception(step.Results);
			}
			StringBuilder results = new StringBuilder();
			results.AppendFormat("ISBN: {0}", resource.Isbn).AppendLine();
			results.AppendFormat("Id: {0}", resource.Id).AppendLine();
			results.AppendFormat("Title: {0}<br/>", resource.Title).AppendLine();
			step.Results = results.ToString();
			if (!UpdateResourceData(resource) || !CopyContent(resource.Isbn) || (_includeChapterNumbersInToc && !UpdateTocXml(resource)) || !TransformXmlContent(resource))
			{
				return;
			}
			_resourceCoreDataService.SetResourceStatus(resource.Id, ResourceStatus.Active, base.TaskName);
			IList<Institution> institutions = _licensingDataService.AddMissingAutoLicenses(useNewInstitutionResourceLicenseTable: true, _r2UtilitiesSettings.AutoLicensesNumberOfLicenses);
			foreach (Institution institution in institutions)
			{
				results.AppendFormat("Licenses added for {0} resources for institution '{1}', account number: {2}<br/>", institution.ResourceLicensesAdded, institution.Name, institution.AccountNumber).AppendLine();
			}
			step.Results = results.ToString();
			step.CompletedSuccessfully = true;
		}
		catch (Exception ex)
		{
			step.Results = ex.Message;
			step.CompletedSuccessfully = false;
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private bool UpdateResourceData(ResourceCore resource)
	{
		_log.InfoFormat(">+++> STEP 1 - Update Resource Data for ISBN: {0}", _isbn);
		TaskResultStep step = new TaskResultStep
		{
			Name = "Update Resource Data for ISBN: " + _isbn,
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		StringBuilder results = new StringBuilder();
		try
		{
			_log.DebugFormat("Title: {0}", resource.Title);
			string sortTitle = resource.Title;
			string alphaChar = "";
			if (!string.IsNullOrWhiteSpace(sortTitle) && sortTitle.Length >= 5)
			{
				if (sortTitle.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
				{
					sortTitle = sortTitle.Substring(2) + ", A";
				}
				else if (sortTitle.StartsWith("AN ", StringComparison.OrdinalIgnoreCase))
				{
					sortTitle = sortTitle.Substring(3) + ", " + sortTitle.Substring(0, 2);
				}
				else if (sortTitle.StartsWith("THE ", StringComparison.OrdinalIgnoreCase))
				{
					sortTitle = sortTitle.Substring(4) + ", " + sortTitle.Substring(0, 3);
				}
				alphaChar = sortTitle.Substring(0, 1);
			}
			R2UtilitiesBase.Log.DebugFormat("alphaChar: {0}, sortTitle: {1}", alphaChar, sortTitle);
			int resourceUpdateCount = _resourceCoreDataService.UpdateNewResourceFields(resource.Id, sortTitle, alphaChar, base.TaskName);
			results.AppendFormat("tResource update count: {0}", resourceUpdateCount);
			int specialtyInsertCount = UpdateResourceSpecialties(resource);
			results.AppendFormat(", tResourceSpecialty insert count: {0}", specialtyInsertCount);
			int practiceAreaInsertCount = UpdateResourcePracticeAreas(resource);
			results.AppendFormat(", tResourcePracticeArea insert count: {0}", practiceAreaInsertCount);
			results.Append(UpdateAtoIndexTerms(resource.Id));
			step.Results = results.ToString();
			step.CompletedSuccessfully = true;
		}
		catch (Exception ex)
		{
			step.Results = ex.Message;
			step.CompletedSuccessfully = false;
			_log.Error(ex.Message, ex);
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
		return step.CompletedSuccessfully;
	}

	private bool CopyContent(string isbn)
	{
		_log.InfoFormat(">+++> STEP 2 - Copying Resource Content for ISBN: {0}", _isbn);
		TaskResultStep step = new TaskResultStep
		{
			Name = "Copying Resource Content for ISBN: " + _isbn,
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		StringBuilder results = new StringBuilder();
		try
		{
			string xmlSourcePath = _r2UtilitiesSettings.BookLoaderSourceRootDirectory + "\\" + isbn + "\\xml\\";
			string xmlDestinationPath = _contentSettings.ContentLocation + "\\" + isbn + "\\";
			bool successful = CopyDirectory(xmlSourcePath, xmlDestinationPath, "XML", results);
			string imageSourcePath = _r2UtilitiesSettings.BookLoaderSourceRootDirectory + "\\" + isbn + "\\images\\";
			string imageDestinationPath = _r2UtilitiesSettings.BookLoaderImageDestinationDirectory + "\\" + isbn + "\\";
			step.CompletedSuccessfully = CopyDirectory(imageSourcePath, imageDestinationPath, "Images", results) && successful;
			step.Results = results.ToString();
		}
		catch (Exception ex)
		{
			step.Results = ex.Message;
			step.CompletedSuccessfully = false;
			_log.Error(ex.Message, ex);
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
		return step.CompletedSuccessfully;
	}

	private bool TransformXmlContent(ResourceCore resource)
	{
		_log.InfoFormat(">+++> STEP 3 - Transform Resource Content for ISBN: {0}", _isbn);
		TaskResultStep step = new TaskResultStep
		{
			Name = "Transform Resource Content for ISBN: " + _isbn,
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			ResourceTransformData resourceTransformData = _transformXmlService.TransformResource(resource);
			step.Results = resourceTransformData.ToDebugString();
			step.CompletedSuccessfully = resourceTransformData.Successful;
		}
		catch (Exception ex)
		{
			step.Results = ex.Message;
			step.CompletedSuccessfully = false;
			_log.Error(ex.Message, ex);
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
		return step.CompletedSuccessfully;
	}

	private bool CopyDirectory(string sourcePath, string destinationPath, string contentType, StringBuilder results)
	{
		int fileCopyCount = 0;
		long totalBytesCopied = 0L;
		DirectoryInfo sourceDirectory = new DirectoryInfo(sourcePath);
		R2UtilitiesBase.Log.DebugFormat("exists: {0}, sourcePath: {1}", sourceDirectory.Exists, sourcePath);
		DirectoryInfo destinationDirectory = new DirectoryInfo(destinationPath);
		R2UtilitiesBase.Log.DebugFormat("exists: {0}, destinationPath: {1}", destinationDirectory.Exists, destinationPath);
		if (destinationDirectory.Exists)
		{
			R2UtilitiesBase.Log.Debug("deleting destination directory ...");
			destinationDirectory.Delete(recursive: true);
			R2UtilitiesBase.Log.Debug("destination directory deleted");
		}
		destinationDirectory.Create();
		results.AppendFormat("<div>{0} Source: {1}</div>", contentType, sourceDirectory).AppendLine();
		results.AppendFormat("<div>{0} Destination: {1}</div>", contentType, destinationPath).AppendLine();
		FileInfo[] filesToCopy = sourceDirectory.GetFiles();
		FileInfo[] array = filesToCopy;
		foreach (FileInfo fileInfo in array)
		{
			string filename = destinationPath + fileInfo.Name;
			R2UtilitiesBase.Log.DebugFormat("Copy file to '{0}', {1} bytes", filename, fileInfo.Length);
			fileInfo.CopyTo(filename, overwrite: true);
			fileCopyCount++;
			totalBytesCopied += fileInfo.Length;
		}
		R2UtilitiesBase.Log.DebugFormat("{0} files copied, {1:0.000} MB", fileCopyCount, (decimal)totalBytesCopied / 1048576.00m);
		results.AppendFormat("<div>{0} {1} files copied (out of {2} files), {3:0.000} MB</div>", fileCopyCount, contentType, filesToCopy.Length, (decimal)totalBytesCopied / 1048576.00m).AppendLine();
		return fileCopyCount == filesToCopy.Length;
	}

	private int UpdateResourcePracticeAreas(ResourceCore resource)
	{
		IList<R2Utilities.DataAccess.ResourcePracticeArea> resourcePracticeAreas = _resourcePracticeAreaDataService.GetResourcePracticeArea(resource.Id);
		return (resourcePracticeAreas.Count == 0) ? _resourcePracticeAreaDataService.Insert(resource.Id, _r2UtilitiesSettings.DefaultPracticeAreaCode, base.TaskName) : 0;
	}

	private int UpdateResourceSpecialties(ResourceCore resource)
	{
		IList<R2Utilities.DataAccess.ResourceSpecialty> resourceSpecialties = _resourceSpecialtyDataService.GetResourceSpecialty(resource.Id);
		return (resourceSpecialties.Count == 0) ? _resourceSpecialtyDataService.Insert(resource.Id, _r2UtilitiesSettings.DefaultSpecialtyCode, base.TaskName) : 0;
	}

	private string UpdateAtoIndexTerms(int resourceId)
	{
		StringBuilder results = new StringBuilder();
		AtoZIndexDataService atoZIndexDataService = new AtoZIndexDataService();
		int deleteCount = atoZIndexDataService.DeleteAtoZIndexRecordsForResource(resourceId);
		results.AppendFormat(", tAtoZIndex records deleted: {0}", deleteCount);
		int drugsInsertCount = atoZIndexDataService.InsertDrugNameIntoAtoZIndexForResource(resourceId);
		int drugSynonymsInsertCount = atoZIndexDataService.InsertDrugNameSynonymsIntoAtoZIndexForResource(resourceId);
		int diseaseInsertCount = atoZIndexDataService.InsertDiseaseNamesIntoAtoZIndexForResource(resourceId);
		int diseaseSynonymsInsertCount = atoZIndexDataService.InsertDiseaseSynonymsIntoAtoZIndexForResource(resourceId);
		int keywordsInsertCount = atoZIndexDataService.InsertKeywordsIntoAtoZIndexForResource(resourceId);
		results.AppendFormat(", tAtoZIndex insert count: {0} [{1} drugs, {2} drug synonyms, {3} diseases, {4} disease synonyms, {5} keywords]", drugsInsertCount + drugSynonymsInsertCount + diseaseInsertCount + diseaseSynonymsInsertCount + keywordsInsertCount, drugsInsertCount, drugSynonymsInsertCount, diseaseInsertCount, diseaseSynonymsInsertCount, keywordsInsertCount);
		return results.ToString();
	}

	private bool UpdateTocXml(ResourceCore resource)
	{
		TaskResultStep step = _tocXmlService.UpdateTocXml(resource.Isbn, base.TaskResult, resource.Id);
		UpdateTaskResult();
		return step.CompletedSuccessfully;
	}
}
