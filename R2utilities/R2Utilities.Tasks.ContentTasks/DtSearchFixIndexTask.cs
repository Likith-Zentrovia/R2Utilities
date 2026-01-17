using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.Services;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class DtSearchFixIndexTask : TaskBase, ITask
{
	private readonly IContentSettings _contentSettings;

	private readonly DtSearchService _dtSearchService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ResourceCoreDataService _resourceCoreDataService;

	private readonly TransformQueueDataService _transformQueueDataService;

	private bool _addBadResourcesToTransformQueue;

	private bool _fixDocIdsInDb;

	private bool _generateDtSearchListIndexFile;

	private string _isbns;

	private bool _logResourceFileSql;

	private int _maxResourceId;

	private int _maxResources = 20000;

	private int _minResourceId;

	private bool _removeBadDatabaseDocIds;

	private bool _removeBadResourcesFromIndex;

	private ResourceFileDataService _resourceFileDataService;

	private string _resourceFileTableName;

	private int _totalFilesRemovedFromIndexCount;

	private long _totalHtmlDirectorySizeInBytes;

	private int _totalHtmlFileCount;

	private int _totalIndexFileCount;

	private int _totalResourcesRemovedFromIndexCount;

	private long _totalXmlDirectorySizeInBytes;

	private int _totalXmlFileCount;

	private bool _truncateAndReloadResourceFileTable;

	protected new string TaskName = "VerifyDocIdsTask";

	public DtSearchFixIndexTask(IR2UtilitiesSettings r2UtilitiesSettings, DtSearchService dtSearchService, IContentSettings contentSettings)
		: base("DtSearchFixIndexTask", "-DtSearchFixIndexTask", "22", TaskGroup.DiagnosticsMaintenance, "Task to fix DtSearch index related issues", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_dtSearchService = dtSearchService;
		_contentSettings = contentSettings;
		_transformQueueDataService = new TransformQueueDataService();
		_resourceCoreDataService = new ResourceCoreDataService();
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_maxResources = GetArgumentInt32("maxResources", 100000);
		_isbns = GetArgument("isbns");
		_resourceFileTableName = GetArgument("resourceFileTableName") ?? _r2UtilitiesSettings.ResourceFileTableName;
		_logResourceFileSql = GetArgumentBoolean("logResourceFileSql", defaultValue: true);
		_resourceFileDataService = new ResourceFileDataService(_resourceFileTableName, _logResourceFileSql);
		_minResourceId = GetArgumentInt32("minResourceId", 0);
		_maxResourceId = GetArgumentInt32("maxResourceId", 100000);
		R2UtilitiesBase.Log.InfoFormat(">>> _maxResources: {0}, _isbns: {1}, _resourceFileTableName: {2}", _maxResources, _isbns, _resourceFileTableName);
		R2UtilitiesBase.Log.InfoFormat(">>> _minResourceId: {0}, _maxResourceId: {1}", _minResourceId, _maxResourceId);
		_truncateAndReloadResourceFileTable = GetArgumentBoolean("truncateAndReloadResourceFileTable", defaultValue: false);
		_generateDtSearchListIndexFile = GetArgumentBoolean("generateDtSearchListIndexFile", defaultValue: false);
		_addBadResourcesToTransformQueue = GetArgumentBoolean("addBadResourcesToTransformQueue", defaultValue: false);
		_removeBadResourcesFromIndex = GetArgumentBoolean("removeBadResourcesFromIndex", defaultValue: false);
		_removeBadDatabaseDocIds = GetArgumentBoolean("removeBadDatabaseDocIds", defaultValue: false);
		_fixDocIdsInDb = GetArgumentBoolean("fixDocIdsInDb", defaultValue: false);
		R2UtilitiesBase.Log.InfoFormat(">>> _truncateAndReloadResourceFileTable: {0}, _generateDtSearchListIndexFile: {1}", _truncateAndReloadResourceFileTable, _generateDtSearchListIndexFile);
		R2UtilitiesBase.Log.InfoFormat(">>> _removeBadResourcesFromIndex: {0}, _removeBadDatabaseDocIds: {1}", _removeBadResourcesFromIndex, _removeBadDatabaseDocIds);
		R2UtilitiesBase.Log.InfoFormat(">>> _addBadResourcesToTransformQueue: {0}, _logResourceFileSql: {1}, _fixDocIdsInDb: {2}", _addBadResourcesToTransformQueue, _logResourceFileSql, _fixDocIdsInDb);
	}

	public override void Run()
	{
		StringBuilder firstStepResults = new StringBuilder();
		firstStepResults.AppendFormat("_maxResources: {0}, _isbns: {1}, _resourceFileTableName: {2}", _maxResources, _isbns, _resourceFileTableName);
		firstStepResults.AppendFormat("_minResourceId: {0}, _maxResourceId: {1}", _minResourceId, _maxResourceId);
		firstStepResults.AppendFormat("_truncateAndReloadResourceFileTable: {0}, _generateDtSearchListIndexFile: {1}", _truncateAndReloadResourceFileTable, _generateDtSearchListIndexFile);
		firstStepResults.AppendFormat("_removeBadResourcesFromIndex: {0}, _removeBadDatabaseDocIds: {1}", _removeBadResourcesFromIndex, _removeBadDatabaseDocIds);
		firstStepResults.AppendFormat("_addBadResourcesToTransformQueue: {0}, _logResourceFileSql: {1}, _fixDocIdsInDb: {2}", _addBadResourcesToTransformQueue, _logResourceFileSql, _fixDocIdsInDb);
		base.TaskResult.Information = "This task will validate the document id within the database (tResourceFile) are the same document ids that are in the dtSearch index.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "DtSearchFixIndexTask",
			StartTime = DateTime.Now,
			Results = firstStepResults.ToString()
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			Stopwatch totalRunTime = new Stopwatch();
			totalRunTime.Start();
			IList<ResourceCore> resourceCores;
			if (!string.IsNullOrEmpty(_isbns))
			{
				string[] array = _isbns.Split(',');
				resourceCores = _resourceCoreDataService.GetResourcesByIsbns(array, orderByDescending: true);
			}
			else
			{
				resourceCores = _resourceCoreDataService.GetResources(_minResourceId, _maxResourceId, _maxResources, orderByDescending: true);
			}
			if (_generateDtSearchListIndexFile)
			{
				_dtSearchService.CreateListIndexFile();
			}
			IDictionary<string, DocIds> docIdsList = _dtSearchService.GetResourceDocIdsFromIndexList();
			Dictionary<string, int> indexFileCounts = new Dictionary<string, int>();
			IList<ResourceDocIds> allResourceDocIds = _resourceFileDataService.GetAllResourceDocIds();
			List<InvalidDocIds> invalidDocIdResources = new List<InvalidDocIds>();
			List<ResourceCore> removeFromIndex = new List<ResourceCore>();
			List<ResourceCore> removeDatabaseDocIds = new List<ResourceCore>();
			List<ResourceContentStatus> resourceContentStatuses = new List<ResourceContentStatus>();
			if (_truncateAndReloadResourceFileTable)
			{
				_resourceFileDataService.TruncateTable();
				_resourceFileDataService.DeleteAll();
			}
			int resourceCount = 0;
			int databaseFileCount = 0;
			int totalResourceCount = resourceCores.Count;
			int activeStatusCount = 0;
			int archivedStatusCount = 0;
			int forthcomingStatusCount = 0;
			int inactiveStatusCount = 0;
			int softDeletedCount = 0;
			StringBuilder results = new StringBuilder();
			int shouldResourceBeIndexedCount = 0;
			foreach (ResourceCore resourceCore in resourceCores)
			{
				resourceCount++;
				string resourceInfo = $"Id: {resourceCore.Id}, ISBN: {resourceCore.Isbn} - status: {resourceCore.StatusId}, record status: {resourceCore.RecordStatus}";
				R2UtilitiesBase.Log.InfoFormat(">>> {0} of {1} - {2}", resourceCount, resourceCores.Count, resourceInfo);
				results.AppendFormat(resourceInfo);
				switch (resourceCore.StatusId)
				{
				case 6:
					activeStatusCount++;
					break;
				case 7:
					archivedStatusCount++;
					break;
				case 8:
					forthcomingStatusCount++;
					break;
				case 72:
					inactiveStatusCount++;
					break;
				}
				if (resourceCore.RecordStatus == 0)
				{
					softDeletedCount++;
				}
				ResourceContentStatus resourceContentStatus = new ResourceContentStatus(resourceCore, _contentSettings);
				ResourceDocIds resourceDocIds = allResourceDocIds.SingleOrDefault((ResourceDocIds x) => x.Id == resourceCore.Id);
				DocIds docIds = new DocIds();
				if (resourceDocIds != null)
				{
					databaseFileCount += resourceDocIds.MaxDocId - resourceDocIds.MinDocId + 1;
				}
				bool isIsbnInIndex = docIdsList.ContainsKey(resourceCore.Isbn);
				bool areResouceDocIdsInDatabase = resourceDocIds != null;
				bool docIdsMatch = false;
				if (isIsbnInIndex)
				{
					docIds = docIdsList[resourceCore.Isbn];
					indexFileCounts[resourceCore.Isbn] = docIds.Filenames.Count;
					resourceContentStatus.ValidateResourceInIndex(docIds);
					if (areResouceDocIdsInDatabase)
					{
						docIdsMatch = docIds.MinimumDocId == resourceDocIds.MinDocId && docIds.MaximumDocId == resourceDocIds.MaxDocId;
					}
				}
				if (ShouldResourceBeIndexed(resourceCore))
				{
					shouldResourceBeIndexedCount++;
					InvalidReasonId invalidReasonId = InvalidReasonId.NotDefined;
					string warning = "";
					if (!isIsbnInIndex)
					{
						warning = ResourceMessage("RESOURCE NOT IN INDEX", resourceCore);
						invalidReasonId = InvalidReasonId.ResourceNotInIndex;
					}
					else if (docIds.Filenames.Any((DocIdFilename x) => x.IsInvalidPath))
					{
						warning = ResourceMessage("INDEX CONTAINS RESOURCE WITH INVALID PATH", resourceCore);
						invalidReasonId = InvalidReasonId.IndexContainsResourceWithInvalidPath;
					}
					else if (!areResouceDocIdsInDatabase)
					{
						warning = ResourceMessage("RESOURCE DOC IDS NOT IN DATABASE", resourceCore);
						invalidReasonId = InvalidReasonId.ResourceDocIdsNotInDatabase;
					}
					else if (!docIdsMatch)
					{
						warning = $"RESOURCE DOC IDS DIFFER - ISBN: {resourceCore.Isbn}, Id: {resourceCore.Id} --> {docIds.MinimumDocId} != {resourceDocIds.MinDocId} and/or {docIds.MaximumDocId} != {resourceDocIds.MaxDocId}";
						invalidReasonId = InvalidReasonId.ResourceDocIdsDiffer;
					}
					else if (IsXmlMissing(resourceContentStatus.Status))
					{
						warning = ResourceMessage("XML FILES MISSING FOR RESOURCE", resourceCore);
						invalidReasonId = InvalidReasonId.XmlFilesMissingForResource;
					}
					else if (IsHtmlMissing(resourceContentStatus.Status))
					{
						warning = ResourceMessage("HTML FILES MISSING FOR RESOURCE", resourceCore);
						invalidReasonId = InvalidReasonId.HtmlFilesMissingForResource;
					}
					else if (IsHtmlNotIndexed(resourceContentStatus.Status))
					{
						warning = ResourceMessage("HTML FILES NOT IN INDEX", resourceCore);
						invalidReasonId = InvalidReasonId.HtmlFilesNotInIndex;
					}
					else if (DoesIndexContainMissingFiles(resourceContentStatus.Status))
					{
						warning = ResourceMessage("INDEX CONTAINS MISSING FILES", resourceCore);
						invalidReasonId = InvalidReasonId.IndexContainsMissingFiles;
					}
					if (invalidReasonId != InvalidReasonId.NotDefined)
					{
						R2UtilitiesBase.Log.Warn(warning);
						InvalidDocIds invalidDocIds = new InvalidDocIds(resourceCore, invalidReasonId, warning, docIds, resourceDocIds);
						invalidDocIdResources.Add(invalidDocIds);
					}
					else
					{
						R2UtilitiesBase.Log.InfoFormat("Resource doc ids match - ISBN: {0}, Id: {1}", resourceCore.Isbn, resourceCore.Id);
					}
				}
				else
				{
					bool isIndexRemove = false;
					if (isIsbnInIndex)
					{
						if (ShouldIsbnBeIndexed(resourceCores, resourceCore.Isbn))
						{
							R2UtilitiesBase.Log.Info(ResourceMessage("Do not remove ISBN from index, another valid resource exists for this ISBN", resourceCore));
						}
						else
						{
							isIndexRemove = true;
							removeFromIndex.Add(resourceCore);
							R2UtilitiesBase.Log.Warn((!docIdsMatch) ? ResourceMessage("Doc ids differ", resourceCore, removeFromIndex: true) : ResourceMessage("Doc ids ok", resourceCore, removeFromIndex: true));
						}
					}
					else
					{
						R2UtilitiesBase.Log.Info(ResourceMessage("Resource is not in index and does not need to be", resourceCore));
					}
					if (areResouceDocIdsInDatabase)
					{
						if (!isIndexRemove)
						{
							removeDatabaseDocIds.Add(resourceCore);
							R2UtilitiesBase.Log.Warn(ResourceMessage("Bad resource doc ids exist in database", resourceCore));
						}
					}
					else
					{
						R2UtilitiesBase.Log.Info(ResourceMessage("Resource is not in the database and does not need to be", resourceCore));
					}
				}
				resourceContentStatuses.Add(resourceContentStatus);
				R2UtilitiesBase.Log.InfoFormat("resourceContentStatus.Isbn: {0}, resourceContentStatus.Status: {1}", resourceContentStatus.Isbn, resourceContentStatus.Status);
				foreach (string statusMessage in resourceContentStatus.StatusMessages)
				{
					R2UtilitiesBase.Log.InfoFormat(" --> error: {0}", statusMessage);
				}
			}
			long indexFileCount = indexFileCounts.Sum((KeyValuePair<string, int> p) => p.Value);
			totalRunTime.Stop();
			R2UtilitiesBase.Log.InfoFormat("### Total Run Time: {0:c}, total resource processed: {1:#,###}, total files verified: {2:#,###}/{3:#,###}", totalRunTime.Elapsed, resourceCount, databaseFileCount, indexFileCount);
			firstStepResults.Insert(0, $"tasked finished in {totalRunTime.Elapsed:c}, ");
			step.Results = firstStepResults.ToString();
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			R2UtilitiesBase.Log.WarnFormat("Invalid resource count: {0}", invalidDocIdResources.Count);
			R2UtilitiesBase.Log.WarnFormat("shouldResourceBeIndexedCount: {0}", shouldResourceBeIndexedCount);
			R2UtilitiesBase.Log.WarnFormat("RESOURCE NOT IN INDEX:                     {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.ResourceNotInIndex));
			R2UtilitiesBase.Log.WarnFormat("RESOURCE DOC IDS NOT IN DATABASE:          {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.ResourceDocIdsNotInDatabase));
			R2UtilitiesBase.Log.WarnFormat("RESOURCE DOC IDS DIFFER:                   {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.ResourceDocIdsDiffer));
			R2UtilitiesBase.Log.WarnFormat("XML FILES MISSING FOR RESOURCE:            {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.XmlFilesMissingForResource));
			R2UtilitiesBase.Log.WarnFormat("HTML FILES MISSING FOR RESOURCE:           {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.HtmlFilesMissingForResource));
			R2UtilitiesBase.Log.WarnFormat("HTML FILES NOT IN INDEX:                   {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.HtmlFilesNotInIndex));
			R2UtilitiesBase.Log.WarnFormat("INDEX CONTAINS RESOURCE WITH INVALID PATH: {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.IndexContainsResourceWithInvalidPath));
			R2UtilitiesBase.Log.WarnFormat("INDEX CONTAINS MISSING FILES:              {0}", invalidDocIdResources.Count((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.IndexContainsMissingFiles));
			AddMissingDocIdsToDatabase(invalidDocIdResources);
			AddBadResourcesToTransformQueue(invalidDocIdResources);
			RemoveDocIdsFromDatabase(removeDatabaseDocIds, resourceContentStatuses);
			foreach (DocIds docIds2 in docIdsList.Values)
			{
				_totalIndexFileCount += docIds2.MaximumDocId - docIds2.MinimumDocId + 1;
			}
			RemoveResourcesFromIndex(removeFromIndex, docIdsList, invalidDocIdResources);
			step = new TaskResultStep
			{
				Name = "Summary",
				StartTime = DateTime.Now
			};
			base.TaskResult.AddStep(step);
			StringBuilder summaryMessage = new StringBuilder();
			summaryMessage.AppendFormat("{0} resource in db", totalResourceCount);
			summaryMessage.AppendFormat(",\r\n {0} active resources", activeStatusCount);
			summaryMessage.AppendFormat(",\r\n {0} archived resources", archivedStatusCount);
			summaryMessage.AppendFormat(",\r\n {0} Pre-Order resources", forthcomingStatusCount);
			summaryMessage.AppendFormat(",\r\n {0} inactive resources", inactiveStatusCount);
			summaryMessage.AppendFormat(",\r\n {0} soft deleted resources", softDeletedCount);
			summaryMessage.AppendFormat(",\r\n {0} total resources in db with doc ids", allResourceDocIds.Count);
			summaryMessage.AppendFormat(",\r\n {0} total resources in index", docIdsList.Count);
			summaryMessage.AppendFormat(",\r\n {0} resources with invalid doc ids", invalidDocIdResources.Count);
			summaryMessage.AppendFormat(",\r\n {0} resources to remove from index", removeFromIndex.Count);
			summaryMessage.AppendFormat(",\r\n {0} resources removed from index", _totalResourcesRemovedFromIndexCount);
			summaryMessage.AppendFormat(",\r\n {0} files removed from index", _totalFilesRemovedFromIndexCount);
			summaryMessage.AppendFormat(",\r\n {0:#,###} total files in index", _totalIndexFileCount);
			summaryMessage.AppendFormat(",\r\n {0:#,###} XML files", _totalXmlFileCount);
			summaryMessage.AppendFormat(",\r\n {0:#,###} HTML files", _totalHtmlFileCount);
			summaryMessage.AppendFormat(",\r\n {0:#,###} XML files in bytes", _totalXmlDirectorySizeInBytes);
			summaryMessage.AppendFormat(",\r\n {0:#,###} HTML files in bytes", _totalHtmlDirectorySizeInBytes);
			summaryMessage.AppendFormat(",\r\n -maxResources={0}", _maxResources);
			summaryMessage.AppendFormat(",\r\n -isbns={0}", _isbns);
			summaryMessage.AppendFormat(",\r\n -resourceFileTableName={0}", _resourceFileTableName);
			summaryMessage.AppendFormat(",\r\n -minResourceId={0}", _minResourceId);
			summaryMessage.AppendFormat(",\r\n -maxResourceId={0}", _maxResourceId);
			summaryMessage.AppendFormat(",\r\n -truncateAndReloadResourceFileTable={0}", _truncateAndReloadResourceFileTable);
			summaryMessage.AppendFormat(",\r\n -generateDtSearchListIndexFile={0}", _generateDtSearchListIndexFile);
			summaryMessage.AppendFormat(",\r\n -removeBadResourcesFromIndex={0}", _removeBadResourcesFromIndex);
			summaryMessage.AppendFormat(",\r\n -removeBadDatabaseDocIds={0}", _removeBadDatabaseDocIds);
			summaryMessage.AppendFormat(",\r\n -addBadResourcesToTransformQueue={0}", _addBadResourcesToTransformQueue);
			summaryMessage.AppendFormat(",\r\n -logResourceFileSql={0}", _logResourceFileSql);
			summaryMessage.AppendFormat(",\r\n -fixDocIdsInDb={0}", _fixDocIdsInDb);
			step.Results = summaryMessage.ToString();
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			step.CompletedSuccessfully = false;
			firstStepResults.Insert(0, "EXCEPTION: " + ex.Message + "\r\n\r\n");
			step.Results = firstStepResults.ToString();
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private void AddBadResourcesToTransformQueue(List<InvalidDocIds> invalidDocIdResources)
	{
		if (_addBadResourcesToTransformQueue)
		{
			foreach (InvalidDocIds invalidDoc in invalidDocIdResources)
			{
				TaskResultStep step = new TaskResultStep
				{
					Name = "AddResourceToTransformQueue-" + invalidDoc.Resource.Isbn,
					StartTime = DateTime.Now
				};
				base.TaskResult.AddStep(step);
				R2UtilitiesBase.Log.WarnFormat("(Add to queue) - Invalid resources doc ids - Id: {0}, ISBN: {1} - Reason: {2} - {3}", invalidDoc.Resource.Id, invalidDoc.Resource.Isbn, invalidDoc.InvalidReasonId, invalidDoc.InvalidReason);
				AddInvalidResourcesToTransformQueue(invalidDoc);
				step.Results = $"Invalid Reason: {invalidDoc.InvalidReasonId}, {invalidDoc.InvalidReason} -- Successfully added to transform queue.";
				step.CompletedSuccessfully = true;
				step.EndTime = DateTime.Now;
				UpdateTaskResult();
			}
			return;
		}
		foreach (InvalidDocIds invalidDoc2 in invalidDocIdResources)
		{
			R2UtilitiesBase.Log.WarnFormat("(No action) - Invalid resources doc ids - Id: {0}, ISBN: {1} - Reason: {2} - {3}", invalidDoc2.Resource.Id, invalidDoc2.Resource.Isbn, invalidDoc2.InvalidReasonId, invalidDoc2.InvalidReason);
			TaskResultStep step = new TaskResultStep
			{
				Name = "ReIndexNeeded-" + invalidDoc2.Resource.Isbn,
				StartTime = DateTime.Now
			};
			base.TaskResult.AddStep(step);
			step.Results = $"(No action) - Invalid resources doc ids - Id: {invalidDoc2.Resource.Id}, ISBN: {invalidDoc2.Resource.Isbn} - Reason: {invalidDoc2.InvalidReasonId} - {invalidDoc2.InvalidReason}";
			step.CompletedSuccessfully = false;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private void RemoveDocIdsFromDatabase(List<ResourceCore> removeDatabaseDocIds, List<ResourceContentStatus> resourceContentStatuses)
	{
		if (removeDatabaseDocIds.Count > 0)
		{
			R2UtilitiesBase.Log.WarnFormat("Bad database docids resource count: {0}", removeDatabaseDocIds.Count);
		}
		if (_removeBadDatabaseDocIds)
		{
			foreach (ResourceCore resourceCore in removeDatabaseDocIds)
			{
				TaskResultStep step = new TaskResultStep
				{
					Name = $"RemoveBadDatabaseDocIds-{resourceCore.Isbn}, Id:{resourceCore.Id}",
					StartTime = DateTime.Now
				};
				base.TaskResult.AddStep(step);
				R2UtilitiesBase.Log.WarnFormat("Deleting bad database doc ids - Id: {0}, ISBN: {1} ", resourceCore.Id, resourceCore.Isbn);
				_resourceFileDataService.DeleteByResourceId(resourceCore.Id);
				step.Results = "Successfully deleted bad doc ids";
				step.CompletedSuccessfully = true;
				step.EndTime = DateTime.Now;
				UpdateTaskResult();
			}
		}
		else
		{
			foreach (ResourceCore resourceCore2 in removeDatabaseDocIds)
			{
				R2UtilitiesBase.Log.WarnFormat("(No action) - Bad database doc ids - Id: {0}, ISBN: {1} ", resourceCore2.Id, resourceCore2.Isbn);
				TaskResultStep step = new TaskResultStep
				{
					Name = $"BadDatabaseDocIdRemovalNeeded-{resourceCore2.Isbn}, Id:{resourceCore2.Id}",
					StartTime = DateTime.Now
				};
				base.TaskResult.AddStep(step);
				step.Results = $"(No action) - Bad database doc ids - Id: {resourceCore2.Id}, ISBN: {resourceCore2.Isbn} ";
				step.CompletedSuccessfully = false;
				step.EndTime = DateTime.Now;
				UpdateTaskResult();
			}
		}
		foreach (ResourceContentStatus resourceContentStatus in resourceContentStatuses)
		{
			_totalXmlFileCount += resourceContentStatus.XmlFileCount;
			_totalHtmlFileCount += resourceContentStatus.HtmlFileCount;
			_totalXmlDirectorySizeInBytes += resourceContentStatus.XmlDirectorySizeInBytes;
			_totalHtmlDirectorySizeInBytes += resourceContentStatus.HtmlDirectorySizeInBytes;
			if (resourceContentStatus.IsSoftDeleted || (resourceContentStatus.ResourceStatus != ResourceStatus.Active && resourceContentStatus.ResourceStatus != ResourceStatus.Archived) || resourceContentStatus.Status == ResourceContentStatusType.Ok || resourceContentStatus.Status == ResourceContentStatusType.XmlAndHtmlOk)
			{
				continue;
			}
			TaskResultStep step = new TaskResultStep
			{
				Name = $"{resourceContentStatus.Status}-{resourceContentStatus.Isbn}",
				StartTime = DateTime.Now
			};
			base.TaskResult.AddStep(step);
			StringBuilder msg = new StringBuilder();
			foreach (string statusMessage in resourceContentStatus.StatusMessages)
			{
				msg.AppendFormat("{0}{1}", (msg.Length == 0) ? "" : ",\r\n ", statusMessage);
			}
			step.Results = msg.ToString();
			step.CompletedSuccessfully = false;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private void RemoveResourcesFromIndex(List<ResourceCore> removeFromIndex, IDictionary<string, DocIds> docIdsList, List<InvalidDocIds> invalidDocIdResources)
	{
		IList<InvalidDocIds> invalidPathDocIds = invalidDocIdResources.Where((InvalidDocIds x) => x.InvalidReasonId == InvalidReasonId.IndexContainsResourceWithInvalidPath || x.InvalidReasonId == InvalidReasonId.ResourceDocIdsNotInDatabase).ToArray();
		if (_removeBadResourcesFromIndex)
		{
			foreach (ResourceCore resourceCore in removeFromIndex)
			{
				TaskResultStep step = new TaskResultStep
				{
					Name = "RemoveFromIndex",
					StartTime = DateTime.Now
				};
				base.TaskResult.AddStep(step);
				R2UtilitiesBase.Log.InfoFormat("Remove from index - ISBN: {0}, id: {1}, StatusId: {2}", resourceCore.Isbn, resourceCore.Id, resourceCore.StatusId);
				DocIds deleteDocIds = docIdsList[resourceCore.Isbn];
				int indexRemovedCount = _dtSearchService.RemoveDocumentIds(deleteDocIds, resourceCore.Id);
				bool removeSuccessful = indexRemovedCount >= 0;
				string removeResults;
				if (removeSuccessful)
				{
					_totalResourcesRemovedFromIndexCount++;
					_totalFilesRemovedFromIndexCount += indexRemovedCount;
					removeResults = $"Successfully removed ISBN {deleteDocIds.Isbn} from dtSearch index ({indexRemovedCount} docids)";
					R2UtilitiesBase.Log.InfoFormat(removeResults);
				}
				else
				{
					removeResults = "Failed to remove ISBN " + deleteDocIds.Isbn + " from index, see log for error detail.";
					R2UtilitiesBase.Log.ErrorFormat(removeResults);
				}
				step.Results = removeResults;
				step.CompletedSuccessfully = removeSuccessful;
				step.EndTime = DateTime.Now;
				UpdateTaskResult();
			}
			{
				foreach (InvalidDocIds invalidDoc in invalidPathDocIds)
				{
					DocIds deleteDocIds2 = invalidDoc.IndexDocIds.GetInvalidDocsInIndex(_contentSettings.NewContentLocation);
					if (deleteDocIds2.Filenames.Count > 0)
					{
						TaskResultStep step = new TaskResultStep
						{
							Name = "RemoveFromIndex-InvalidPath",
							StartTime = DateTime.Now
						};
						base.TaskResult.AddStep(step);
						R2UtilitiesBase.Log.InfoFormat("Remove from index (Invalid Path) - ISBN: {0}, id: {1}, StatusId: {2}", invalidDoc.Resource.Isbn, invalidDoc.Resource.Id, invalidDoc.Resource.StatusId);
						int indexRemovedCount2 = _dtSearchService.RemoveDocumentIds(deleteDocIds2, invalidDoc.Resource.Id);
						bool removeSuccessful2 = indexRemovedCount2 >= 0;
						string removeResults2;
						if (removeSuccessful2)
						{
							_totalResourcesRemovedFromIndexCount++;
							_totalFilesRemovedFromIndexCount += indexRemovedCount2;
							removeResults2 = $"Successfully removed ISBN {deleteDocIds2.Isbn} from dtSearch index ({indexRemovedCount2} docids)";
							R2UtilitiesBase.Log.InfoFormat(removeResults2);
						}
						else
						{
							removeResults2 = "Failed to remove ISBN " + deleteDocIds2.Isbn + " from index, see log for error detail.";
							R2UtilitiesBase.Log.ErrorFormat(removeResults2);
						}
						step.Results = removeResults2;
						step.CompletedSuccessfully = removeSuccessful2;
						step.EndTime = DateTime.Now;
						UpdateTaskResult();
					}
					else
					{
						R2UtilitiesBase.Log.InfoFormat("RemoveFromIndex-InvalidPath-{0} => no resource file inserts required", invalidDoc.Resource.Isbn);
					}
				}
				return;
			}
		}
		foreach (ResourceCore resourceCore2 in removeFromIndex)
		{
			R2UtilitiesBase.Log.WarnFormat("(No action) - Remove from index - Id: {0}, ISBN: {1}, StatusId: {2}, RecordStatus: {3}", resourceCore2.Id, resourceCore2.Isbn, resourceCore2.StatusId, resourceCore2.RecordStatus);
			TaskResultStep step = new TaskResultStep
			{
				Name = "IndexRemovalNeeded-" + resourceCore2.Isbn,
				StartTime = DateTime.Now
			};
			base.TaskResult.AddStep(step);
			step.Results = $"(No action) - Remove from index - Id: {resourceCore2.Id}, ISBN: {resourceCore2.Isbn}, StatusId: {resourceCore2.StatusId}, RecordStatus: {resourceCore2.RecordStatus}";
			step.CompletedSuccessfully = false;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
		foreach (InvalidDocIds invalidDoc2 in invalidPathDocIds)
		{
			R2UtilitiesBase.Log.WarnFormat("(No action) - Remove from index - Id: {0}, ISBN: {1}, StatusId: {2}, RecordStatus: {3}", invalidDoc2.Resource.Id, invalidDoc2.Resource.Isbn, invalidDoc2.Resource.StatusId, invalidDoc2.Resource.RecordStatus);
			TaskResultStep step = new TaskResultStep
			{
				Name = "IndexRemovalNeeded-" + invalidDoc2.Resource.Isbn,
				StartTime = DateTime.Now
			};
			base.TaskResult.AddStep(step);
			step.Results = $"(No action) - Remove from index - Id: {invalidDoc2.Resource.Id}, ISBN: {invalidDoc2.Resource.Isbn}, StatusId: {invalidDoc2.Resource.StatusId}, RecordStatus: {invalidDoc2.Resource.RecordStatus}";
			step.CompletedSuccessfully = false;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private void AddMissingDocIdsToDatabase(List<InvalidDocIds> invalidDocIdResources)
	{
		if (_fixDocIdsInDb)
		{
			foreach (InvalidDocIds invalidDoc in invalidDocIdResources)
			{
				if (invalidDoc.InvalidReasonId == InvalidReasonId.ResourceDocIdsNotInDatabase || invalidDoc.InvalidReasonId == InvalidReasonId.ResourceDocIdsDiffer || invalidDoc.InvalidReasonId == InvalidReasonId.ResourceDocIdsNotInDatabase || invalidDoc.InvalidReasonId == InvalidReasonId.ResourceDocIdsDiffer)
				{
					TaskResultStep step = new TaskResultStep
					{
						Name = "FixDocIdsInDb-" + invalidDoc.Resource.Isbn,
						StartTime = DateTime.Now
					};
					base.TaskResult.AddStep(step);
					R2UtilitiesBase.Log.WarnFormat("(Action) - Fix doc ids - Id: {0}, ISBN: {1} - Reason: {2} - {3}", invalidDoc.Resource.Id, invalidDoc.Resource.Isbn, invalidDoc.InvalidReasonId, invalidDoc.InvalidReason);
					int rowsDeleted = _resourceFileDataService.DeleteByResourceId(invalidDoc.Resource.Id);
					int countByDocIds = invalidDoc.IndexDocIds.MaximumDocId - invalidDoc.IndexDocIds.MinimumDocId + 1;
					if (invalidDoc.IndexDocIds.Filenames.Count != countByDocIds)
					{
						R2UtilitiesBase.Log.ErrorFormat("invalidDoc.IndexDocIds.Filenames.Count != countByDocIds, {0} != {1}", invalidDoc.IndexDocIds.Filenames.Count, countByDocIds);
						step.Results = $"Invalid Reason: {invalidDoc.InvalidReasonId}, {rowsDeleted} -- Deleted {invalidDoc.InvalidReason}, invalidDoc.IndexDocIds.Filenames.Count != countByDocIds, {invalidDoc.IndexDocIds.Filenames.Count} != {countByDocIds}";
						step.CompletedSuccessfully = false;
						foreach (DocIdFilename docIdFilename in invalidDoc.IndexDocIds.Filenames)
						{
							R2UtilitiesBase.Log.DebugFormat("-->> Id: {0}, IsInvalidPath: {1}, Name: {2}", docIdFilename.Id, docIdFilename.IsInvalidPath, docIdFilename.Name);
						}
					}
					else
					{
						List<R2Utilities.DataAccess.ResourceFile> resourceFiles = new List<R2Utilities.DataAccess.ResourceFile>();
						foreach (DocIdFilename docIdFilename2 in invalidDoc.IndexDocIds.Filenames)
						{
							resourceFiles.Add(new R2Utilities.DataAccess.ResourceFile(docIdFilename2.Id, docIdFilename2.Name, invalidDoc.Resource.Id));
						}
						int rowsInserted = _resourceFileDataService.InsertBatch(resourceFiles, _r2UtilitiesSettings.ResourceFileInsertBatchSize);
						step.Results = $"Invalid Reason: {invalidDoc.InvalidReasonId}, {invalidDoc.InvalidReason} -- Deleted {rowsDeleted}, added {rowsInserted} rows to resource file table.";
						step.CompletedSuccessfully = true;
					}
					step.EndTime = DateTime.Now;
					UpdateTaskResult();
				}
			}
			return;
		}
		foreach (InvalidDocIds invalidDoc2 in invalidDocIdResources)
		{
			if (invalidDoc2.InvalidReasonId == InvalidReasonId.ResourceDocIdsNotInDatabase || invalidDoc2.InvalidReasonId == InvalidReasonId.ResourceDocIdsDiffer)
			{
				R2UtilitiesBase.Log.WarnFormat("(No action) - Fix doc ids - Id: {0}, ISBN: {1} - Reason: {2} - {3}", invalidDoc2.Resource.Id, invalidDoc2.Resource.Isbn, invalidDoc2.InvalidReasonId, invalidDoc2.InvalidReason);
				TaskResultStep step = new TaskResultStep
				{
					Name = "FixDocIdsNeeded-" + invalidDoc2.Resource.Isbn,
					StartTime = DateTime.Now
				};
				base.TaskResult.AddStep(step);
				step.Results = $"(No action) - Fix doc ids - Id: {invalidDoc2.Resource.Id}, ISBN: {invalidDoc2.Resource.Isbn} - Reason: {invalidDoc2.InvalidReasonId} - {invalidDoc2.InvalidReason}";
				step.CompletedSuccessfully = false;
				step.EndTime = DateTime.Now;
				UpdateTaskResult();
			}
		}
	}

	private static string ResourceMessage(string message, ResourceCore resourceCore, bool removeFromIndex = false)
	{
		string removeMessage = (removeFromIndex ? "Resource needs to be removed from dtSearch index, " : "");
		return $"{removeMessage}{message} - ISBN: {resourceCore.Isbn}, Id: {resourceCore.Id}, StatusId: {resourceCore.StatusId}, RecordStatus: {resourceCore.RecordStatus}";
	}

	private static bool IsXmlMissing(ResourceContentStatusType resourceContentStatusType)
	{
		return resourceContentStatusType == ResourceContentStatusType.XmlDirectoryDoesNotExist || resourceContentStatusType == ResourceContentStatusType.XmlDirectoryIsEmpty || resourceContentStatusType == ResourceContentStatusType.MissingXmlFiles;
	}

	private static bool IsHtmlMissing(ResourceContentStatusType resourceContentStatusType)
	{
		return resourceContentStatusType == ResourceContentStatusType.HtmlDirectoryDoesNotExist || resourceContentStatusType == ResourceContentStatusType.HtmlDirectoryIsEmpty || resourceContentStatusType == ResourceContentStatusType.MissingHtmlFiles || resourceContentStatusType == ResourceContentStatusType.MissingHtmlGlossaryFiles;
	}

	private static bool IsHtmlNotIndexed(ResourceContentStatusType resourceContentStatusType)
	{
		return resourceContentStatusType == ResourceContentStatusType.HtmlFilesNotInIndex;
	}

	private static bool DoesIndexContainMissingFiles(ResourceContentStatusType resourceContentStatusType)
	{
		return resourceContentStatusType == ResourceContentStatusType.IndexContainsMissingFiles;
	}

	private static bool ShouldResourceBeIndexed(ResourceCore resourceCore)
	{
		return (resourceCore.StatusId == 6 || resourceCore.StatusId == 7) && resourceCore.RecordStatus == 1;
	}

	private static bool ShouldIsbnBeIndexed(IEnumerable<ResourceCore> resourceCores, string isbn)
	{
		return resourceCores.Any((ResourceCore r) => r.Isbn == isbn && ShouldResourceBeIndexed(r));
	}

	private void AddInvalidResourcesToTransformQueue(InvalidDocIds invalidDoc)
	{
		if (_transformQueueDataService.GetCount(invalidDoc.Resource.Id, invalidDoc.Resource.Isbn, "A") == 0)
		{
			_transformQueueDataService.Insert(invalidDoc.Resource.Id, invalidDoc.Resource.Isbn, "A");
		}
	}
}
