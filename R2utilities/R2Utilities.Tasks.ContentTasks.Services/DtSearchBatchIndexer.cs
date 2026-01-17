using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Resource;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class DtSearchBatchIndexer : R2UtilitiesBase
{
	private readonly DtSearchService _dtSearchService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private int _directoryFileCount = 0;

	private long _directorySize = 0L;

	private int _indexedDocumentCount;

	private int _indexedResourceCount;

	public int IndexedResourceCount => _indexedResourceCount;

	public int IndexedDocumentCount => _indexedDocumentCount;

	public TimeSpan IndexTimeSpan { get; set; }

	public TimeSpan ResourceFileLoadTimeSpan { get; set; }

	public DtSearchBatchIndexer(IR2UtilitiesSettings r2UtilitiesSettings, DtSearchService dtSearchService)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_dtSearchService = dtSearchService;
	}

	public bool ProcessNextBatch(TaskResult taskResult, int maxBatchSize, int minResourceId, int maxResourceId)
	{
		IndexQueueDataService indexQueueDataService = new IndexQueueDataService();
		IList<IndexQueue> indexQueues = indexQueueDataService.GetNextBatch(maxBatchSize, minResourceId, maxResourceId).ToList();
		int indexQueueSize = indexQueueDataService.GetIndexQueueSize();
		return ProcessNextBatch(taskResult, indexQueues, indexQueueSize);
	}

	public bool ProcessForthcoming(TaskResult taskResult)
	{
		IndexQueueDataService indexQueueDataService = new IndexQueueDataService();
		List<IndexQueue> indexQueues = indexQueueDataService.GetForthcomingResourcesToIndex(_r2UtilitiesSettings.R2DatabaseName).ToList();
		int indexQueueSize = indexQueueDataService.GetIndexQueueSize();
		return ProcessNextBatch(taskResult, indexQueues, indexQueueSize);
	}

	private bool ProcessNextBatch(TaskResult taskResult, ICollection<IndexQueue> indexQueues, int indexQueueSize)
	{
		IndexTimeSpan = default(TimeSpan);
		ResourceFileLoadTimeSpan = default(TimeSpan);
		TaskResultStep step = new TaskResultStep
		{
			Name = "AddTitlesToIndex",
			StartTime = DateTime.Now
		};
		taskResult.AddStep(step);
		StringBuilder stepResults = new StringBuilder();
		try
		{
			DateTime batchStartTime = DateTime.Now;
			_dtSearchService.CreateDtSearchIndex();
			int fragmentationPercentage = _dtSearchService.GetIndexFragmentationStatus();
			bool compressIndex = (indexQueueSize <= indexQueues.Count() && fragmentationPercentage > _r2UtilitiesSettings.HtmlIndexerFragmentationLimit) || _r2UtilitiesSettings.HtmlIndexerForceCompression;
			R2UtilitiesBase.Log.DebugFormat("compressIndex: {0}, indexQueueSize: {1}, indexQueues.Count(): {2}, fragmentationPercentage: {3}, HtmlIndexerFragmentationLimit: {4}, HtmlIndexerForceCompression: {5}", compressIndex, indexQueueSize, indexQueues.Count(), fragmentationPercentage, _r2UtilitiesSettings.HtmlIndexerFragmentationLimit, _r2UtilitiesSettings.HtmlIndexerForceCompression);
			Stopwatch indexTimer = new Stopwatch();
			indexTimer.Start();
			IList<string> isbns = indexQueues.Select((IndexQueue resource) => resource.Isbn).ToList();
			stepResults.AppendFormat("{0} resources to index", isbns.Count).AppendLine();
			if (isbns.Count == 0)
			{
				R2UtilitiesBase.Log.Info("No more resources to index");
				step.CompletedSuccessfully = true;
				return false;
			}
			stepResults.AppendFormat("\tISBNs: {0}", string.Join(",", isbns)).AppendLine();
			R2UtilitiesBase.Log.InfoFormat(">>>>>>>>>> INDEXING {0} RESOURCES <<<<<<<<<<", isbns.Count);
			bool indexWasSuccessful = _dtSearchService.AddDirectoriesToDtSearchIndex(indexQueues, compressIndex, ref _indexedResourceCount, ref _indexedDocumentCount);
			indexTimer.Stop();
			long indexElapsed = indexTimer.ElapsedMilliseconds;
			IndexTimeSpan = indexTimer.Elapsed;
			R2UtilitiesBase.Log.InfoFormat("indexElapsed: {0}, indexWasSuccessful: {1}", indexElapsed, indexWasSuccessful);
			double totalAvgIndexTimePerFile = (double)indexElapsed / (double)_directoryFileCount;
			R2UtilitiesBase.Log.DebugFormat("indexQueues.Count: {0}, indexElapsed: {1:0,000} ms, totalAvgIndexTimePerFile: {2:0.000} ms, _directoryFileCount: {3:0,000}, _directorySize: {4:0,000} bytes", indexQueues.Count, indexElapsed, totalAvgIndexTimePerFile, _directoryFileCount, _directorySize);
			_dtSearchService.SaveResourceFiles(taskResult, batchStartTime);
			_dtSearchService.CleanupDocIds(taskResult, batchStartTime);
			step.CompletedSuccessfully = true;
			return indexQueues.Count > 0;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			step.Results = stepResults.ToString();
		}
	}

	public void InsertForthcomingResource(IResource resource)
	{
		IndexQueueDataService indexQueueDataService = new IndexQueueDataService();
		indexQueueDataService.AddResourceToQueue(resource.Id, resource.Isbn);
	}
}
