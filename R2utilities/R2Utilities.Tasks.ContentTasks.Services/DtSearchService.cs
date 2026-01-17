using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using dtSearch.Engine;
using NHibernate;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;
using R2V2.Infrastructure.Threads;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class DtSearchService : R2UtilitiesBase, IIndexStatusHandler
{
	private const string IndexStatusCodeError = "E";

	private const string IndexStatusCodeProcessing = "P";

	private const string IndexStatusCodeIndexed = "I";

	private readonly IContentSettings _contentSettings;

	private readonly string _htmlRootPath;

	private readonly string _indexListFile;

	private readonly string _indexPath;

	private readonly IndexQueueDataService _indexQueueDataService;

	private readonly ILog<DtSearchService> _log;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly string _removalFilePath;

	private readonly ResourceFileDataService _resourceFileDataService;

	private readonly SearchService _searchService;

	private int _indexedFileCount;

	private List<IndexQueue> _indexQueues;

	private ResourceToIndex _resourceToIndex;

	private List<ResourceToIndex> _resourceToIndexList;

	public DtSearchService(IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings, SearchService searchService, ILog<DtSearchService> log)
	{
		_contentSettings = contentSettings;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_searchService = searchService;
		_log = log;
		_indexQueueDataService = new IndexQueueDataService();
		_indexPath = _contentSettings.DtSearchIndexLocation;
		_htmlRootPath = _contentSettings.NewContentLocation;
		_resourceFileDataService = new ResourceFileDataService(r2UtilitiesSettings.ResourceFileTableName, logSql: true);
		_indexListFile = Path.Combine(_contentSettings.DtSearchIndexLocation, "docids.txt");
		_removalFilePath = Path.Combine(_contentSettings.DtSearchIndexLocation, "remove_docids.txt");
	}

	void IIndexStatusHandler.OnProgressUpdate(IndexProgressInfo info)
	{
		if (info.UpdateType != MessageCode.dtsnIndexFileDone)
		{
			return;
		}
		R2Utilities.DataAccess.ResourceFile resourceFile = new R2Utilities.DataAccess.ResourceFile(info.File.DocId, info.File.DisplayName);
		_indexedFileCount++;
		if (_resourceToIndex == null || _resourceToIndex.IndexQueue.Isbn != resourceFile.Isbn)
		{
			R2UtilitiesBase.Log.InfoFormat("Indexing ISBN: {0}, First file: {1}, DocId: {2}, Indexed {3} files in {4:#,###}s, {5}s remaining, {6}% complete", resourceFile.Isbn, resourceFile.FilenameFull, resourceFile.DocumentId, _indexedFileCount, info.ElapsedSeconds, info.EstRemainingSeconds, info.PercentDone);
			IList<ResourceToIndex> list = _resourceToIndexList.FindAll((ResourceToIndex x) => x.IndexQueue.Isbn == resourceFile.Isbn);
			if (list.Any())
			{
				if (list.Count > 1)
				{
					R2UtilitiesBase.Log.WarnFormat("ISBN found multiple times: {0}, use first", list.Count());
				}
				_resourceToIndex = list.First();
				_resourceToIndex.AddResourceFile(resourceFile);
			}
			else
			{
				R2UtilitiesBase.Log.WarnFormat("Can't find resource by ISBN {0}, {1}, {2}", resourceFile.FilenameFull, resourceFile.Isbn, resourceFile.DocumentId);
			}
		}
		else
		{
			_resourceToIndex.AddResourceFile(resourceFile);
		}
	}

	AbortValue IIndexStatusHandler.CheckForAbort()
	{
		return AbortValue.Continue;
	}

	public void CreateDtSearchIndex()
	{
		try
		{
			Options options = new Options();
			string indexDirectory = _indexPath;
			DirectoryInfo directoryInfo = new DirectoryInfo(indexDirectory);
			if (!directoryInfo.Exists)
			{
				options.MaxStoredFieldSize = 1024;
				options.Save();
				directoryInfo.Create();
			}
			FileInfo[] files = directoryInfo.GetFiles();
			if (!files.Any())
			{
				IndexJob indexJob = CreateIndexJob(actionCreate: true, actionAdd: true, actionCompress: false);
				indexJob.ActionCreate = true;
				indexJob.Execute();
				R2UtilitiesBase.Log.InfoFormat("DTSEARCH INDEX CREATED - {0}", indexDirectory);
			}
			string indexInfo = GetIndexStatus();
			R2UtilitiesBase.Log.Info(indexInfo);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	private IndexJob CreateIndexJob(bool actionCreate, bool actionAdd, bool actionCompress)
	{
		IndexJob indexJob = new IndexJob
		{
			IndexPath = _indexPath,
			CreateRelativePaths = true,
			ActionAdd = actionAdd,
			ActionCreate = actionCreate,
			ActionCompress = actionCompress,
			IndexingFlags = (IndexingFlags.dtsIndexCreateRelativePaths | IndexingFlags.dtsIndexCacheText | IndexingFlags.dtsIndexKeepExistingDocIds)
		};
		indexJob.EnumerableFields.AddRange(_r2UtilitiesSettings.IndexEnumerableFields);
		indexJob.StoredFields.AddRange(_r2UtilitiesSettings.IndexStoredFields);
		return indexJob;
	}

	public bool DoesDirectoryHaveFileToIndex(string isbn, out int directoryFileCount, out long directorySize)
	{
		directoryFileCount = 0;
		directorySize = 0L;
		try
		{
			string contentDirectoryName = _htmlRootPath + "\\" + isbn.Trim();
			DirectoryInfo directoryInfo = new DirectoryInfo(contentDirectoryName);
			if (!directoryInfo.Exists)
			{
				R2UtilitiesBase.Log.WarnFormat("Content directory does not exist, {0}", contentDirectoryName);
				return false;
			}
			FileInfo[] fileInfos = directoryInfo.GetFiles();
			bool containsBookXml = false;
			bool containsTocXml = false;
			long folderSize = 0L;
			if (fileInfos.Length != 0)
			{
				directoryFileCount += fileInfos.Length;
				FileInfo[] array = fileInfos;
				foreach (FileInfo fileInfo in array)
				{
					directorySize += fileInfo.Length;
					folderSize += fileInfo.Length;
					if (fileInfo.Name.StartsWith("book."))
					{
						containsBookXml = true;
					}
					else if (fileInfo.Name.StartsWith("toc."))
					{
						containsTocXml = true;
					}
				}
			}
			R2UtilitiesBase.Log.DebugFormat("contentDirectoryName: {0}, file count: {1}, folder size: {2:0,000}, containsBookXml: {3}, containsTocXml: {4}", contentDirectoryName, fileInfos.Length, folderSize, containsBookXml, containsTocXml);
			return fileInfos.Length != 0 && containsBookXml && containsTocXml;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	public bool AddDirectoriesToDtSearchIndex(IEnumerable<IndexQueue> indexQueues, bool compressIndex, ref int indexedResourceCount, ref int indexedDocumentCount)
	{
		_indexQueues = new List<IndexQueue>(indexQueues);
		_resourceToIndexList = new List<ResourceToIndex>();
		try
		{
			int batchFileCount = 0;
			int batchDocumentCount = 0;
			IndexJob indexJob = CreateIndexJob(actionCreate: false, actionAdd: true, compressIndex);
			foreach (IndexQueue indexQueue in _indexQueues)
			{
				ResourceToIndex resourceToIndex = new ResourceToIndex(indexQueue);
				_resourceToIndexList.Add(resourceToIndex);
				string directoryPath = _htmlRootPath + "\\html\\" + indexQueue.Isbn.Trim();
				DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
				if (!directoryInfo.Exists)
				{
					R2UtilitiesBase.Log.WarnFormat("DIRECTORY DOES NOT EXIST! path: {0}", directoryInfo);
					indexQueue.IndexStatus = "E";
					indexQueue.StatusMessage = $"DIRECTORY DOES NOT EXIST! path: {directoryInfo}";
					continue;
				}
				FileInfo[] files = directoryInfo.GetFiles();
				if (files.Length == 0)
				{
					R2UtilitiesBase.Log.WarnFormat("DIRECTORY IS EMPTY! path: {0}", directoryInfo);
					indexQueue.IndexStatus = "E";
					indexQueue.StatusMessage = $"DIRECTORY IS EMPTY! path: {directoryInfo}";
					continue;
				}
				R2UtilitiesBase.Log.DebugFormat("{0} files in directory '{1}'", files.Length, directoryPath);
				indexQueue.IndexStatus = "P";
				DateTime lastModofiedDate = DateTime.Now;
				FileInfo[] array = files;
				foreach (FileInfo fileInfo in array)
				{
					string fullName = fileInfo.FullName;
					Attempt.Execute(() => File.SetLastWriteTime(fullName, lastModofiedDate), 3, 3000);
				}
				string folderName = directoryPath + "<+>";
				indexJob.FoldersToIndex.Add(folderName);
				indexedResourceCount++;
				indexedDocumentCount += files.Length;
				batchFileCount++;
				batchDocumentCount += files.Length;
			}
			_indexedFileCount = 0;
			indexJob.StatusHandler = this;
			R2UtilitiesBase.Log.InfoFormat("Indexing {0} resources, {1:#,###} files", batchFileCount, batchDocumentCount);
			bool jobStatus = indexJob.Execute();
			R2UtilitiesBase.Log.InfoFormat("jobStatus: {0}", jobStatus);
			R2UtilitiesBase.Log.InfoFormat("Indexing {0} total resources, {1:#,###} total files", indexedResourceCount, indexedDocumentCount);
			return jobStatus;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	public int SaveResourceFiles(TaskResult taskResult, DateTime batchStartTime)
	{
		Stopwatch insertTimer = new Stopwatch();
		int resourceCount = 0;
		foreach (ResourceToIndex resourceToIndex in _resourceToIndexList)
		{
			IndexQueue indexQueue = resourceToIndex.IndexQueue;
			TaskResultStep docIdsStep = new TaskResultStep
			{
				Name = $"Updating doc ids for ISBN: {indexQueue.Isbn}, resource id: {indexQueue.ResourceId}, index queue id: {indexQueue.Id}",
				StartTime = DateTime.Now
			};
			taskResult.AddStep(docIdsStep);
			resourceCount++;
			R2UtilitiesBase.Log.Info(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
			R2UtilitiesBase.Log.InfoFormat("resourceCount: {0}, Id: {1}, ISBN: {2}", resourceCount, indexQueue.Id, indexQueue.Isbn);
			if (indexQueue.IndexStatus == "P")
			{
				insertTimer.Restart();
				bool successful = SaveDocumentIds(resourceToIndex);
				insertTimer.Stop();
				if (successful)
				{
					indexQueue.IndexStatus = "I";
					indexQueue.StatusMessage = "resource indexed successfully.";
				}
				else
				{
					indexQueue.IndexStatus = "E";
					indexQueue.StatusMessage = "Updating tResourceFile failed.";
				}
			}
			indexQueue.DateStarted = batchStartTime;
			indexQueue.DateFinished = DateTime.Now;
			_indexQueueDataService.Update(indexQueue);
			docIdsStep.CompletedSuccessfully = indexQueue.IndexStatus != "E";
			docIdsStep.EndTime = DateTime.Now;
			docIdsStep.Results = indexQueue.StatusMessage;
			long insertElapsed = insertTimer.ElapsedMilliseconds;
			int fileCount = indexQueue.LastDocumentId - indexQueue.FirstDocumentId + 1;
			double avgInsertTimePerFile = (double)insertElapsed / (double)fileCount;
			R2UtilitiesBase.Log.DebugFormat("insertElapsed: {0:0,000} ms, avgInsertTimePerFile: {1:0.000} ms", insertElapsed, avgInsertTimePerFile);
			R2UtilitiesBase.Log.Info("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
		}
		return resourceCount;
	}

	public bool CompressIndex()
	{
		try
		{
			IndexJob indexJob = CreateIndexJob(actionCreate: false, actionAdd: false, actionCompress: true);
			bool jobStatus = indexJob.Execute();
			R2UtilitiesBase.Log.InfoFormat("jobStatus: {0}", jobStatus);
			return jobStatus;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	public void LoadDocumentIds(Resource resource, ISession session)
	{
		IList<ISearchResultItem> searchResults = _searchService.PerformSearchByIsbn(resource.Isbn.Trim());
		List<R2Utilities.DataAccess.ResourceFile> resourceFiles = new List<R2Utilities.DataAccess.ResourceFile>();
		foreach (ISearchResultItem searchResultItem in searchResults)
		{
			R2UtilitiesBase.Log.DebugFormat("DocumnetId: {0}, Filename: {1}", searchResultItem.DocumnetId, searchResultItem.DisplayName);
			R2Utilities.DataAccess.ResourceFile resourceFile = new R2Utilities.DataAccess.ResourceFile(searchResultItem.DocumnetId, searchResultItem.DisplayName, resource.Id);
			resourceFiles.Add(resourceFile);
		}
		_resourceFileDataService.InsertBatch(resourceFiles, _r2UtilitiesSettings.HtmlIndexerMaxIndexBatches);
	}

	public bool LoadDocumentIds(IndexQueue indexQueue)
	{
		IList<ISearchResultItem> searchResults = null;
		try
		{
			searchResults = _searchService.PerformSearchByIsbn(indexQueue.Isbn.Trim());
			List<R2Utilities.DataAccess.ResourceFile> resourceFiles = new List<R2Utilities.DataAccess.ResourceFile>();
			indexQueue.FirstDocumentId = 0;
			indexQueue.LastDocumentId = 0;
			foreach (ISearchResultItem searchResultItem in searchResults)
			{
				R2Utilities.DataAccess.ResourceFile resourceFile = new R2Utilities.DataAccess.ResourceFile(searchResultItem.DocumnetId, searchResultItem.DisplayName, indexQueue.ResourceId);
				resourceFiles.Add(resourceFile);
				if (resourceFile.DocumentId < indexQueue.FirstDocumentId || indexQueue.FirstDocumentId == 0)
				{
					indexQueue.FirstDocumentId = resourceFile.DocumentId;
				}
				if (resourceFile.DocumentId > indexQueue.LastDocumentId || indexQueue.LastDocumentId == 0)
				{
					indexQueue.LastDocumentId = resourceFile.DocumentId;
				}
			}
			_resourceFileDataService.DeleteByResourceId(indexQueue.ResourceId);
			_resourceFileDataService.InsertBatch(resourceFiles, _r2UtilitiesSettings.ResourceFileInsertBatchSize);
			return true;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			if (searchResults != null)
			{
				foreach (ISearchResultItem searchResultItem2 in searchResults)
				{
					R2UtilitiesBase.Log.DebugFormat("DocumnetId: {0}, Filename: {1}", searchResultItem2.DocumnetId, searchResultItem2.DisplayName);
				}
			}
			return false;
		}
	}

	private bool SaveDocumentIds(ResourceToIndex resourceToIndex)
	{
		try
		{
			_resourceFileDataService.DeleteByResourceId(resourceToIndex.IndexQueue.ResourceId);
			_resourceFileDataService.InsertBatch(resourceToIndex.ResourceFiles, _r2UtilitiesSettings.ResourceFileInsertBatchSize);
			return true;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			foreach (R2Utilities.DataAccess.ResourceFile resourceFile in resourceToIndex.ResourceFiles)
			{
				R2UtilitiesBase.Log.WarnFormat("DocumentId: {0}, FilenameFull: {1}", resourceFile.DocumentId, resourceFile.FilenameFull);
			}
			return false;
		}
	}

	public string GetIndexStatus()
	{
		IndexInfo indexInfo = IndexJob.GetIndexInfo(_indexPath);
		StringBuilder info = new StringBuilder().AppendLine(">>>>>>>>>> INDEX STATUS:");
		info.AppendFormat("CompressedDate  : {0}", indexInfo.CompressedDate).AppendLine();
		info.AppendFormat("CreatedDate     : {0}", indexInfo.CreatedDate).AppendLine();
		info.AppendFormat("DocCount        : {0}", indexInfo.DocCount).AppendLine();
		info.AppendFormat("Fragmentation   : {0}", indexInfo.Fragmentation).AppendLine();
		info.AppendFormat("IndexSize       : {0}", indexInfo.IndexSize).AppendLine();
		info.AppendFormat("LastDocId       : {0}", indexInfo.LastDocId).AppendLine();
		info.AppendFormat("ObsoleteCount   : {0}", indexInfo.ObsoleteCount).AppendLine();
		info.AppendFormat("PercentFull     : {0}", indexInfo.PercentFull).AppendLine();
		info.AppendFormat("StartingDocId   : {0}", indexInfo.StartingDocId).AppendLine();
		info.AppendFormat("StructureVersion: {0}", indexInfo.StructureVersion).AppendLine();
		info.AppendFormat("UpdatedDate     : {0}", indexInfo.UpdatedDate).AppendLine();
		info.AppendFormat("WordCount       : {0}", indexInfo.WordCount).AppendLine();
		int alwaysAdd = (int)(indexInfo.Flags & IndexingFlags.dtsAlwaysAdd);
		int checkDiskSpace = (int)(indexInfo.Flags & IndexingFlags.dtsCheckDiskSpace);
		int indexCacheOriginalFile = (int)(indexInfo.Flags & IndexingFlags.dtsIndexCacheOriginalFile);
		int indexCacheText = (int)(indexInfo.Flags & IndexingFlags.dtsIndexCacheText);
		int indexCacheTextWithoutFields = (int)(indexInfo.Flags & IndexingFlags.dtsIndexCacheTextWithoutFields);
		int indexCreateAccentSensitive = (int)(indexInfo.Flags & IndexingFlags.dtsIndexCreateAccentSensitive);
		int indexCreateCaseSensitive = (int)(indexInfo.Flags & IndexingFlags.dtsIndexCreateCaseSensitive);
		int indexCreateRelativePaths = (int)(indexInfo.Flags & IndexingFlags.dtsIndexCreateRelativePaths);
		int indexCreateVersion6 = (int)(indexInfo.Flags & IndexingFlags.dtsIndexCreateVersion6);
		int indexKeepExistingDocIds = (int)(indexInfo.Flags & IndexingFlags.dtsIndexKeepExistingDocIds);
		int indexIndexResumeUpdate = (int)(indexInfo.Flags & IndexingFlags.dtsIndexResumeUpdate);
		StringBuilder indexFlags = new StringBuilder();
		indexFlags.AppendFormat("alwaysAdd: {0}", alwaysAdd);
		indexFlags.AppendFormat(", checkDiskSpace: {0}", checkDiskSpace);
		indexFlags.AppendFormat(", indexCacheOriginalFile: {0}", indexCacheOriginalFile);
		indexFlags.AppendFormat(", indexCacheText: {0}", indexCacheText);
		indexFlags.AppendFormat(", indexCacheTextWithoutFields: {0}", indexCacheTextWithoutFields);
		indexFlags.AppendFormat(", indexCreateAccentSensitive: {0}", indexCreateAccentSensitive);
		indexFlags.AppendFormat(", indexCreateCaseSensitive: {0}", indexCreateCaseSensitive);
		indexFlags.AppendFormat(", indexCreateRelativePaths: {0}", indexCreateRelativePaths);
		indexFlags.AppendFormat(", indexCreateVersion6: {0}", indexCreateVersion6);
		indexFlags.AppendFormat(", indexKeepExistingDocIds: {0}", indexKeepExistingDocIds);
		indexFlags.AppendFormat(", indexIndexResumeUpdate: {0}", indexIndexResumeUpdate);
		info.AppendFormat("Flags           : {0} - [{1}]", indexInfo.Flags, indexFlags).AppendLine();
		info.AppendLine("<<<<<<<<<< INDEX STATUS");
		_log.Info(info.ToString());
		return info.ToString();
	}

	public int GetIndexFragmentationStatus()
	{
		IndexInfo indexInfo = IndexJob.GetIndexInfo(_indexPath);
		int fragmentationPercentage = (int)indexInfo.Fragmentation;
		R2UtilitiesBase.Log.DebugFormat("fragmentationPercentage: {0}", fragmentationPercentage);
		return fragmentationPercentage;
	}

	public void CreateListIndexFile()
	{
		ListIndexJob listIndexJob = new ListIndexJob
		{
			IndexPath = _indexPath,
			ListIndexFlags = (ListIndexFlags.dtsListIndexFiles | ListIndexFlags.dtsListIndexIncludeDocId),
			OutputToString = false,
			OutputFile = _indexListFile
		};
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		listIndexJob.Execute();
		stopwatch.Stop();
		R2UtilitiesBase.Log.DebugFormat("listIndexJob.Execute() took {0:#,###} ms", stopwatch.ElapsedMilliseconds);
	}

	public IDictionary<string, DocIds> GetResourceDocIdsFromIndexList()
	{
		Dictionary<string, DocIds> resourcesDocIds = new Dictionary<string, DocIds>();
		int counter = 0;
		int validPathCount = 0;
		int invalidPathCount = 0;
		using (StreamReader file = new StreamReader(_indexListFile))
		{
			string line;
			while ((line = file.ReadLine()) != null)
			{
				counter++;
				string[] parts = line.Trim().Split(' ');
				int docId = int.Parse(parts[0]);
				string[] filePathParts = parts[1].Split('\\');
				string isbn = filePathParts[filePathParts.Length - 2];
				string filename = filePathParts.Last();
				DocIdFilename docIdFilename = new DocIdFilename
				{
					Id = docId,
					Name = filename,
					IsInvalidPath = !parts[1].StartsWith(_contentSettings.NewContentLocation, StringComparison.CurrentCultureIgnoreCase)
				};
				if (docIdFilename.IsInvalidPath)
				{
					invalidPathCount++;
					R2UtilitiesBase.Log.WarnFormat("Invalid Path: {0}", parts[1]);
				}
				else
				{
					validPathCount++;
				}
				DocIds docIds;
				if (resourcesDocIds.ContainsKey(isbn))
				{
					docIds = resourcesDocIds[isbn];
					if (docIds.MaximumDocId < docId)
					{
						docIds.MaximumDocId = docId;
					}
					else if (docIds.MinimumDocId > docId)
					{
						docIds.MinimumDocId = docId;
					}
				}
				else
				{
					R2UtilitiesBase.Log.DebugFormat("line: {0} --> {1}", counter, line);
					docIds = new DocIds
					{
						Isbn = isbn,
						MaximumDocId = docId,
						MinimumDocId = docId
					};
					resourcesDocIds.Add(isbn, docIds);
				}
				docIds.Filenames.Add(docIdFilename);
			}
			file.Close();
		}
		R2UtilitiesBase.Log.InfoFormat("file: {0}, line count: {1}, valid path count: {2}, invalid path count: {3}", _indexListFile, counter, validPathCount, invalidPathCount);
		return resourcesDocIds;
	}

	public int RemoveDocumentIds(DocIds docIds, int resourceId)
	{
		try
		{
			GenerateRemovalFile(docIds);
			int[] ids = docIds.Filenames.Select((DocIdFilename x) => x.Id).ToArray();
			bool jobStatus = RemoveDocumentIds();
			if (jobStatus)
			{
				_resourceFileDataService.DeleteBatch(ids, resourceId);
			}
			return jobStatus ? docIds.Filenames.Count : (-1);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	public bool RemoveDocumentIds(string docIdsToRemove)
	{
		try
		{
			File.WriteAllText(_removalFilePath, docIdsToRemove);
			return RemoveDocumentIds();
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	private void GenerateRemovalFile(DocIds docIds)
	{
		using StreamWriter streamWriter = new StreamWriter(_removalFilePath, append: false);
		foreach (DocIdFilename filename in docIds.Filenames)
		{
			streamWriter.WriteLine(">{0}", filename.Id);
		}
	}

	private bool RemoveDocumentIds(bool deleteFile = true)
	{
		IndexJob indexJob = CreateIndexJob(actionCreate: false, actionAdd: false, actionCompress: false);
		indexJob.ActionRemoveListed = true;
		indexJob.ToRemoveListName = _removalFilePath;
		bool jobStatus = indexJob.Execute();
		if (!jobStatus)
		{
			R2UtilitiesBase.Log.Error("RemoveDocumentIds indexjob failed! Errors:");
			for (int i = 0; i < indexJob.Errors.Count; i++)
			{
				R2UtilitiesBase.Log.ErrorFormat("{0}", indexJob.Errors.Message(i));
			}
		}
		else if (deleteFile)
		{
			string newFilename = Path.Combine(_contentSettings.DtSearchIndexLocation, $"remove_docids_{DateTime.Now:yyyyMMdd-HHmmss}.txt");
			File.Move(_removalFilePath, newFilename);
		}
		return jobStatus;
	}

	public void CleanupDocIds(TaskResult taskResult, DateTime batchStartTime)
	{
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		List<ResourceToIndex> indexedResources = _resourceToIndexList.Where((ResourceToIndex x) => x.IndexQueue.IndexStatus == "I").ToList();
		TaskResultStep cleanupStep = new TaskResultStep
		{
			Name = $"Cleanup Doc Ids for {indexedResources.Count} resources",
			StartTime = DateTime.Now
		};
		taskResult.AddStep(cleanupStep);
		try
		{
			int badDocIdCount = 0;
			StringBuilder docIdsToRemove = new StringBuilder();
			if (indexedResources.Any())
			{
				R2UtilitiesBase.Log.Info(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
				R2UtilitiesBase.Log.InfoFormat("Creating List Index File: {0}", _indexListFile);
				CreateListIndexFile();
				R2UtilitiesBase.Log.InfoFormat("Reading file: {0}", _indexListFile);
				IDictionary<string, DocIds> docIdsList = GetResourceDocIdsFromIndexList();
				R2UtilitiesBase.Log.Info("Finding bad doc ids ...");
				foreach (ResourceToIndex resourceToIndex in indexedResources)
				{
					DocIds docIds = docIdsList[resourceToIndex.IndexQueue.Isbn];
					foreach (DocIdFilename filename in docIds.Filenames)
					{
						R2Utilities.DataAccess.ResourceFile resourceFile = resourceToIndex.ResourceFiles.FirstOrDefault((R2Utilities.DataAccess.ResourceFile x) => x.FilenameFull.Equals(filename.Name));
						if (resourceFile == null)
						{
							R2UtilitiesBase.Log.DebugFormat("Indexed file not in tResourceFile, Doc Id: {0}, Filename: {1}, IsInvalidPath: {2}", filename.Id, filename.Name, filename.IsInvalidPath);
							docIdsToRemove.AppendFormat(">{0}", filename.Id).AppendLine();
							badDocIdCount++;
						}
					}
				}
			}
			if (badDocIdCount > 0)
			{
				R2UtilitiesBase.Log.InfoFormat("Removing {0} doc ids from index ...", badDocIdCount);
				RemoveDocumentIds(docIdsToRemove.ToString());
				stopwatch.Stop();
				R2UtilitiesBase.Log.InfoFormat("Removed {0} doc ids from index in {1:#,###} ms", badDocIdCount, stopwatch.ElapsedMilliseconds);
				cleanupStep.Results = $"Removed {badDocIdCount} doc ids from index in {stopwatch.ElapsedMilliseconds:#,###} ms";
			}
			else
			{
				R2UtilitiesBase.Log.Info("No bad doc ids found!");
				cleanupStep.Results = "No bad doc ids found!";
			}
			cleanupStep.CompletedSuccessfully = true;
		}
		catch (Exception ex)
		{
			cleanupStep.CompletedSuccessfully = false;
			cleanupStep.Results = "EXCEPTION: " + ex.Message;
			R2UtilitiesBase.Log.ErrorFormat(ex.Message, ex);
		}
		finally
		{
			stopwatch.Stop();
			cleanupStep.EndTime = DateTime.Now;
		}
	}
}
