using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Resource;
using R2V2.Extensions;
using R2V2.Infrastructure.Compression;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class RestoreResourceContentTask : TaskBase
{
	private readonly IContentSettings _contentSettings;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly IQueryable<Resource> _resources;

	private bool _fileCopy;

	private string _isbns;

	private int _maxResourcesToProcess = 25000;

	private int _maxResourcesToRestore = 25000;

	private int _resourcesProcessed;

	private int _resourcesRestored;

	private int _restoreExceptionCount;

	public RestoreResourceContentTask(IQueryable<Resource> resources, IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("RestoreResourceContentTask", "-RestoreResourceContentTask", "27", TaskGroup.DiagnosticsMaintenance, "Copy compressed files locally and restore", enabled: true)
	{
		_resources = resources;
		_contentSettings = contentSettings;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		base.TaskResult.Information = base.TaskDescription;
		TaskResultStep step = new TaskResultStep
		{
			Name = "RestoreContent",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		try
		{
			_isbns = GetArgument("isbns");
			int.TryParse(GetArgument("maxResourcesToProcess") ?? "0", out _maxResourcesToProcess);
			int.TryParse(GetArgument("maxResourcesToRestore") ?? "0", out _maxResourcesToRestore);
			bool.TryParse(GetArgument("fileCopy") ?? "false", out _fileCopy);
			UpdateTaskResult();
			int downloadedFileCount = 0;
			RestoreResources();
			if (_restoreExceptionCount > 0)
			{
				step.Results = $"Error - {_restoreExceptionCount} exceptions, {downloadedFileCount} resources downloaded, {_resourcesRestored} resources restored, {_resourcesProcessed} resources processed";
				step.CompletedSuccessfully = false;
			}
			else
			{
				step.Results = $"OK - {downloadedFileCount} resources downloaded, {_resourcesRestored} resources restored, {_resourcesProcessed} resources processed";
				step.CompletedSuccessfully = true;
			}
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

	private void RestoreResources()
	{
		IOrderedQueryable<Resource> query;
		if (!string.IsNullOrWhiteSpace(_isbns))
		{
			string[] isbns = _isbns.Split(',');
			query = ((_maxResourcesToProcess > 0) ? (from r in _resources.Where((Resource r) => isbns.Contains(r.Isbn)).Take(_maxResourcesToProcess)
				orderby r.Id descending
				select r) : (from r in _resources
				where isbns.Contains(r.Isbn)
				orderby r.Id descending
				select r));
		}
		else
		{
			query = ((_maxResourcesToProcess > 0) ? (from r in _resources.Take(_maxResourcesToProcess)
				orderby r.Id descending
				select r) : _resources.OrderByDescending((Resource r) => r.Id));
		}
		List<Resource> resources = query.ToList();
		IndexQueueDataService indexQueueDataService = new IndexQueueDataService();
		TransformQueueDataService transformQueueDataService = new TransformQueueDataService();
		TaskResultStep step = new TaskResultStep
		{
			Name = "Restoring resources",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		StringBuilder stepResults = new StringBuilder();
		foreach (Resource resource in resources)
		{
			_resourcesProcessed++;
			R2UtilitiesBase.Log.InfoFormat("Processing ISBN: {0}, {1} of {2} - {3} resources restored", resource.Isbn, _resourcesProcessed, resources.Count(), _resourcesRestored);
			ResourceToRestore resourceToRestore = new ResourceToRestore(resource, _contentSettings, _r2UtilitiesSettings);
			R2UtilitiesBase.Log.Debug(resourceToRestore.ToDebugString());
			bool saveResourceRestoreResult = false;
			ResourceRestoreResult resourceRestoreResult = new ResourceRestoreResult
			{
				Isbn = resourceToRestore.Isbn,
				BackupFileDateTime = resourceToRestore.BackupZipFile.LastWriteTime,
				BackupFileFullPath = resourceToRestore.BackupZipFile.FullName,
				RestoreXmlDirectory = resourceToRestore.Xml.ResourceDirectory.FullName,
				RestoreStartTime = DateTime.Now
			};
			try
			{
				if (RestoreToWorkingDirectory(resourceToRestore))
				{
					resourceToRestore.Xml.ResourceDirectory.Empty();
					resourceToRestore.Html.ResourceDirectory.Empty();
					resourceToRestore.Images.ResourceDirectory.Empty();
					resourceToRestore.Media.ResourceDirectory.Empty();
					resourceRestoreResult.XmlFileCount = MoveFiles(resourceToRestore.XmlWorkingDirectory, resourceToRestore.Xml.ResourceDirectory);
					resourceRestoreResult.HtmlFileCount = MoveFiles(resourceToRestore.HtmlWorkingDirectory, resourceToRestore.Html.ResourceDirectory);
					resourceRestoreResult.ImageFileCount = MoveFiles(resourceToRestore.ImagesWorkingDirectory, resourceToRestore.Images.ResourceDirectory);
					resourceRestoreResult.MediaFileCount = MoveFiles(resourceToRestore.MediaWorkingDirectory, resourceToRestore.Media.ResourceDirectory);
					FileInfo[] bookCoverImages = resourceToRestore.BookCoverDirectory.GetFiles(resourceToRestore.Isbn + ".*");
					FileInfo[] array = bookCoverImages;
					foreach (FileInfo bookCoverImage in array)
					{
						bookCoverImage.Delete();
					}
					bookCoverImages = resourceToRestore.BookCoverImageWorkingDirectory.GetFiles(resourceToRestore.Isbn + ".*");
					FileInfo[] array2 = bookCoverImages;
					foreach (FileInfo bookCoverImage2 in array2)
					{
						string bookCoverImageFullPath = Path.Combine(resourceToRestore.BookCoverDirectory.FullName, bookCoverImage2.Name);
						bookCoverImage2.MoveTo(bookCoverImageFullPath);
						R2UtilitiesBase.Log.InfoFormat("++ Book cover image file moved to {0}", bookCoverImageFullPath);
					}
					if (bookCoverImages.Length == 0)
					{
						R2UtilitiesBase.Log.ErrorFormat("Resource missing cover image - {0}", resourceToRestore.BookCoverImageWorkingDirectory.FullName);
					}
					resourceToRestore.WorkingDirectory.Refresh();
					resourceToRestore.WorkingDirectory.Empty();
					resourceToRestore.WorkingDirectory.Delete();
					if (resourceRestoreResult.HtmlFileCount > 0 && resourceRestoreResult.HtmlFileCount > resourceRestoreResult.XmlFileCount - 5)
					{
						indexQueueDataService.AddResourceToQueue(resource.Id, resource.Isbn);
					}
					else if (resourceRestoreResult.XmlFileCount > 0)
					{
						transformQueueDataService.Insert(resource.Id, resource.Isbn, "A");
					}
					stepResults.AppendFormat("ISBN: {0} - {1} XML files, {2} HTML files, {3} Image files, {4} Media files - Resource Id: {5}", resource.Isbn, resourceRestoreResult.XmlFileCount, resourceRestoreResult.HtmlFileCount, resourceRestoreResult.ImageFileCount, resourceRestoreResult.MediaFileCount, resource.Id).AppendLine();
					saveResourceRestoreResult = true;
					resourceRestoreResult.WasRestoreSuccessful = true;
					_resourcesRestored++;
				}
			}
			catch (Exception ex)
			{
				R2UtilitiesBase.Log.Error(ex.Message, ex);
				_restoreExceptionCount++;
				stepResults.AppendFormat("ISBN: {0} - Exception: {1}", resource.Isbn, ex.Message).AppendLine();
				saveResourceRestoreResult = true;
				resourceRestoreResult.WasRestoreSuccessful = false;
				resourceRestoreResult.ErrorMessage = ex.Message;
			}
			finally
			{
				resourceRestoreResult.RestoreEndTime = DateTime.Now;
				if (saveResourceRestoreResult)
				{
					string resourceRestoreResultFilePath = GetRestoreResultFilePath(resourceToRestore.Isbn);
					File.WriteAllText(resourceRestoreResultFilePath, resourceRestoreResult.ToJsonString());
					if (!resourceRestoreResult.WasRestoreSuccessful)
					{
						resourceRestoreResultFilePath = GetRestoreResultFilePath(resourceToRestore.Isbn, errorDirectory: true);
						File.WriteAllText(resourceRestoreResultFilePath, resourceRestoreResult.ToJsonString());
					}
				}
			}
			if (_maxResourcesToRestore > 0 && _resourcesRestored >= _maxResourcesToRestore)
			{
				R2UtilitiesBase.Log.Info("MAX RESOURCES RESTORED!");
				break;
			}
		}
		step.Results = stepResults.ToString();
		step.CompletedSuccessfully = _restoreExceptionCount == 0;
		step.EndTime = DateTime.Now;
		UpdateTaskResult();
	}

	private bool RestoreToWorkingDirectory(ResourceToRestore resourceToRestore)
	{
		if (!resourceToRestore.BackupZipFile.Exists)
		{
			R2UtilitiesBase.Log.InfoFormat("-- Backup file does not exist: {0}", resourceToRestore.Isbn);
			return false;
		}
		string previousRestoreResultFilePath = GetRestoreResultFilePath(resourceToRestore.Isbn);
		if (File.Exists(previousRestoreResultFilePath))
		{
			string json = File.ReadAllText(previousRestoreResultFilePath);
			ResourceRestoreResult previousRestoreResult = ResourceRestoreResult.ParseJson(json);
			if (previousRestoreResult.BackupFileDateTime >= resourceToRestore.BackupZipFile.LastWriteTime)
			{
				R2UtilitiesBase.Log.InfoFormat("-- BackupFileDateTime: {0} >= BackupZipFile.LastWriteTime: {1}", previousRestoreResult.BackupFileDateTime, resourceToRestore.BackupZipFile.LastWriteTime);
				R2UtilitiesBase.Log.InfoFormat("-- Resources does not need to be restored: {0}", resourceToRestore.Isbn);
				return false;
			}
			R2UtilitiesBase.Log.InfoFormat("++ BackupFileDateTime: {0} < BackupZipFile.LastWriteTime: {1}", previousRestoreResult.BackupFileDateTime, resourceToRestore.BackupZipFile.LastWriteTime);
		}
		R2UtilitiesBase.Log.InfoFormat("++ Restore ISBN: {0}", resourceToRestore.Isbn);
		resourceToRestore.WorkingDirectory.Empty();
		ZipHelper.ExtractAll(resourceToRestore.BackupZipFile.FullName, resourceToRestore.WorkingDirectory.FullName);
		return true;
	}

	private int MoveFiles(DirectoryInfo sourceDirectoryInfo, DirectoryInfo destinationDirectoryInfo)
	{
		if (!sourceDirectoryInfo.Exists)
		{
			R2UtilitiesBase.Log.InfoFormat("-- Directory does not exist {0}", sourceDirectoryInfo.FullName);
			return 0;
		}
		int fileCount = 0;
		if (!destinationDirectoryInfo.Exists)
		{
			destinationDirectoryInfo.Create();
		}
		FileInfo[] sourceFiles = sourceDirectoryInfo.GetFiles();
		FileInfo[] array = sourceFiles;
		foreach (FileInfo sourceFileInfo in array)
		{
			FileInfo destinationFileInfo = new FileInfo(Path.Combine(destinationDirectoryInfo.FullName, sourceFileInfo.Name));
			if (destinationFileInfo.Exists)
			{
				destinationFileInfo.Delete();
			}
			sourceFileInfo.MoveTo(destinationFileInfo.FullName);
			fileCount++;
		}
		R2UtilitiesBase.Log.InfoFormat("++ {0} files moved to {1}", fileCount, destinationDirectoryInfo.FullName);
		return fileCount;
	}

	private string GetRestoreResultFilePath(string isbn, bool errorDirectory = false)
	{
		if (errorDirectory)
		{
			return Path.Combine(_r2UtilitiesSettings.ContentBackupDirectory, "_backupResults", "_errors", isbn + "_restore-results.json");
		}
		return Path.Combine(_r2UtilitiesSettings.ContentBackupDirectory, "_backupResults", isbn + "_restore-results.json");
	}
}
