using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.Services;
using R2V2.Core.Resource;
using R2V2.Core.Resource.BookSearch;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class DtSearchHtmlIndexerTask : TaskBase, ITask
{
	private readonly IContentSettings _contentSettings;

	private readonly DtSearchBatchIndexer _dtSearchBatchIndexer;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly IQueryable<IResource> _resources;

	private bool _indexForthcoming;

	private int _maxBatchSize;

	private int _maxResourceId;

	private int _minResourceId;

	public DtSearchHtmlIndexerTask(DtSearchBatchIndexer dtSearchBatchIndexer, IR2UtilitiesSettings r2UtilitiesSettings, IQueryable<IResource> resources, IContentSettings contentSettings)
		: base("DtSearchHtmlIndexerTask", "-DtSearchHtmlIndexerTask", "01", TaskGroup.ContentLoading, "Indexes HTML content produced by TransformXmlTask based in IndexQueue table", enabled: true)
	{
		_dtSearchBatchIndexer = dtSearchBatchIndexer;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_resources = resources;
		_contentSettings = contentSettings;
		SetSummaryEmailSetting(includeOkTaskSteps: false, showStepTotals: true, 10);
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_maxBatchSize = GetArgumentInt32("maxBatchSize", _r2UtilitiesSettings.HtmlIndexerBatchSize);
		_minResourceId = GetArgumentInt32("minResourceId", 0);
		_maxResourceId = GetArgumentInt32("maxResourceId", 100000);
		_indexForthcoming = GetArgumentBoolean("indexForthcoming", defaultValue: false);
		R2UtilitiesBase.Log.InfoFormat("_maxBatchSize: {0}, _minResourceId: {1}, _maxResourceId: {2}", _maxBatchSize, _minResourceId, _maxResourceId);
	}

	public override void Run()
	{
		if (_indexForthcoming)
		{
			IndexForthcomingSearchData();
			return;
		}
		Stopwatch runtimeTimer = new Stopwatch();
		runtimeTimer.Start();
		try
		{
			int totalIndexedResourceCount = 0;
			int totalIndexedDocumentCount = 0;
			TimeSpan totalIndexTimeSpan = default(TimeSpan);
			TimeSpan totalResourceFileLoadTimeSpan = default(TimeSpan);
			int batchNumber = 0;
			while (_dtSearchBatchIndexer.ProcessNextBatch(base.TaskResult, _maxBatchSize, _minResourceId, _maxResourceId))
			{
				batchNumber++;
				R2UtilitiesBase.Log.InfoFormat("batchNumber: {0} - COMPLETE", batchNumber);
				totalIndexedResourceCount += _dtSearchBatchIndexer.IndexedResourceCount;
				totalIndexedDocumentCount += _dtSearchBatchIndexer.IndexedDocumentCount;
				totalIndexTimeSpan = totalIndexTimeSpan.Add(_dtSearchBatchIndexer.IndexTimeSpan);
				totalResourceFileLoadTimeSpan = totalResourceFileLoadTimeSpan.Add(_dtSearchBatchIndexer.ResourceFileLoadTimeSpan);
				R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
				R2UtilitiesBase.Log.InfoFormat("IndexedResourceCount: {0}", _dtSearchBatchIndexer.IndexedResourceCount);
				R2UtilitiesBase.Log.InfoFormat("IndexedDocumentCount: {0}", _dtSearchBatchIndexer.IndexedDocumentCount);
				R2UtilitiesBase.Log.InfoFormat("IndexTimeSpan: {0:c}", _dtSearchBatchIndexer.IndexTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("ResourceFileLoadTimeSpan: {0:c}", _dtSearchBatchIndexer.ResourceFileLoadTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("Resource Index Average: {0} ms", _dtSearchBatchIndexer.IndexTimeSpan.TotalMilliseconds / (double)_dtSearchBatchIndexer.IndexedResourceCount);
				R2UtilitiesBase.Log.InfoFormat("ResourceFile Insert Average: {0} ms", _dtSearchBatchIndexer.ResourceFileLoadTimeSpan.TotalMilliseconds / (double)_dtSearchBatchIndexer.IndexedDocumentCount);
				R2UtilitiesBase.Log.Info("+    +    +    +    +    +    +    +    +    +    +");
				R2UtilitiesBase.Log.InfoFormat("totalIndexedResourceCount: {0}", totalIndexedResourceCount);
				R2UtilitiesBase.Log.InfoFormat("totalIndexedDocumentCount: {0}", totalIndexedDocumentCount);
				R2UtilitiesBase.Log.InfoFormat("totalIndexTimeSpan: {0:c}", totalIndexTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("totalResourceFileLoadTimeSpan: {0:c}", totalResourceFileLoadTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("Total Run Time: {0:c}", runtimeTimer.Elapsed);
				R2UtilitiesBase.Log.InfoFormat("Total Resource Index Average: {0} ms", totalIndexTimeSpan.TotalMilliseconds / (double)totalIndexedResourceCount);
				R2UtilitiesBase.Log.InfoFormat("Total ResourceFile Insert Average: {0} ms", totalResourceFileLoadTimeSpan.TotalMilliseconds / (double)totalIndexedDocumentCount);
				R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
				if (_r2UtilitiesSettings.HtmlIndexerMaxIndexBatches > 0 && _r2UtilitiesSettings.HtmlIndexerMaxIndexBatches == batchNumber)
				{
					R2UtilitiesBase.Log.InfoFormat("MAXIMUM INDEX BATCHES REACHED: {0}", _r2UtilitiesSettings.HtmlIndexerMaxIndexBatches);
					break;
				}
			}
			runtimeTimer.Stop();
			base.TaskResult.Information = $"Resource Count: {totalIndexedResourceCount}, Document Count: {totalIndexedDocumentCount}, Index Time: {totalIndexTimeSpan:c}, Database Update Time: {totalResourceFileLoadTimeSpan:c}, Total Task Time: {runtimeTimer.Elapsed:c}";
			R2UtilitiesBase.Log.InfoFormat("APP COMPLETE -- run time: {0:c}", runtimeTimer.Elapsed);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}

	public void IndexForthcomingSearchData()
	{
		Stopwatch runtimeTimer = new Stopwatch();
		runtimeTimer.Start();
		try
		{
			int totalIndexedResourceCount = 0;
			int totalIndexedDocumentCount = 0;
			TimeSpan totalIndexTimeSpan = default(TimeSpan);
			TimeSpan totalResourceFileLoadTimeSpan = default(TimeSpan);
			List<IResource> resources = _resources.Where((IResource x) => !x.NotSaleable && x.StatusId == 8).ToList();
			string contentLocation = _contentSettings.NewContentLocation;
			foreach (IResource resource in resources)
			{
				BookSearchResource bsr = new BookSearchResource(resource, contentLocation);
				if (!bsr.DoesR2BookSearchXmlExist())
				{
					bsr.SaveR2BookSearchXml();
				}
				_dtSearchBatchIndexer.InsertForthcomingResource(resource);
			}
			_dtSearchBatchIndexer.ProcessForthcoming(base.TaskResult);
			totalIndexedResourceCount += _dtSearchBatchIndexer.IndexedResourceCount;
			totalIndexedDocumentCount += _dtSearchBatchIndexer.IndexedDocumentCount;
			totalIndexTimeSpan = totalIndexTimeSpan.Add(_dtSearchBatchIndexer.IndexTimeSpan);
			totalResourceFileLoadTimeSpan = totalResourceFileLoadTimeSpan.Add(_dtSearchBatchIndexer.ResourceFileLoadTimeSpan);
			R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
			R2UtilitiesBase.Log.InfoFormat("IndexedResourceCount: {0}", _dtSearchBatchIndexer.IndexedResourceCount);
			R2UtilitiesBase.Log.InfoFormat("IndexedDocumentCount: {0}", _dtSearchBatchIndexer.IndexedDocumentCount);
			R2UtilitiesBase.Log.InfoFormat("IndexTimeSpan: {0:c}", _dtSearchBatchIndexer.IndexTimeSpan);
			R2UtilitiesBase.Log.InfoFormat("ResourceFileLoadTimeSpan: {0:c}", _dtSearchBatchIndexer.ResourceFileLoadTimeSpan);
			R2UtilitiesBase.Log.InfoFormat("Resource Index Average: {0} ms", _dtSearchBatchIndexer.IndexTimeSpan.TotalMilliseconds / (double)_dtSearchBatchIndexer.IndexedResourceCount);
			R2UtilitiesBase.Log.InfoFormat("ResourceFile Insert Average: {0} ms", _dtSearchBatchIndexer.ResourceFileLoadTimeSpan.TotalMilliseconds / (double)_dtSearchBatchIndexer.IndexedDocumentCount);
			R2UtilitiesBase.Log.Info("+    +    +    +    +    +    +    +    +    +    +");
			R2UtilitiesBase.Log.InfoFormat("totalIndexedResourceCount: {0}", totalIndexedResourceCount);
			R2UtilitiesBase.Log.InfoFormat("totalIndexedDocumentCount: {0}", totalIndexedDocumentCount);
			R2UtilitiesBase.Log.InfoFormat("totalIndexTimeSpan: {0:c}", totalIndexTimeSpan);
			R2UtilitiesBase.Log.InfoFormat("totalResourceFileLoadTimeSpan: {0:c}", totalResourceFileLoadTimeSpan);
			R2UtilitiesBase.Log.InfoFormat("Total Run Time: {0:c}", runtimeTimer.Elapsed);
			R2UtilitiesBase.Log.InfoFormat("Total Resource Index Average: {0} ms", totalIndexTimeSpan.TotalMilliseconds / (double)totalIndexedResourceCount);
			R2UtilitiesBase.Log.InfoFormat("Total ResourceFile Insert Average: {0} ms", totalResourceFileLoadTimeSpan.TotalMilliseconds / (double)totalIndexedDocumentCount);
			R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}
}
