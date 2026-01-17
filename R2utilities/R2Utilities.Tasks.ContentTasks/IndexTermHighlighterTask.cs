using System;
using System.Diagnostics;
using R2Utilities.DataAccess.Terms;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.Services;

namespace R2Utilities.Tasks.ContentTasks;

public class IndexTermHighlighterTask : TaskBase
{
	private readonly IndexTermHighlightSettings _indexTermHighlightSettings;

	private readonly TermHighlighterService _termHighlighterService;

	public IndexTermHighlighterTask(TermHighlighterService termHighlighterService, IndexTermHighlightSettings indexTermHighlightSettings, IndexTermDataService indexTermDataService)
		: base("IndexTermHighlighterTask", "-IndexTermHighlighterTask", "05", TaskGroup.ContentLoading, "Task to update the term highlighting", enabled: true)
	{
		_indexTermHighlightSettings = indexTermHighlightSettings;
		_termHighlighterService = termHighlighterService;
		_termHighlighterService.Init(_indexTermHighlightSettings, indexTermDataService, "IndexTermHighlighterTask");
	}

	public override void Run()
	{
		Stopwatch runtimeTimer = new Stopwatch();
		runtimeTimer.Start();
		try
		{
			int totalHighlightedResourceCount = 0;
			int totalHighlightedDocumentCount = 0;
			TimeSpan totalHighlightTimeSpan = default(TimeSpan);
			TimeSpan totalResourceFileLoadTimeSpan = default(TimeSpan);
			int batchNumber = 0;
			while (_termHighlighterService.ProcessNextBatch(base.TaskResult))
			{
				batchNumber++;
				R2UtilitiesBase.Log.InfoFormat("batchNumber: {0} - COMPLETE", batchNumber);
				totalHighlightedResourceCount += _termHighlighterService.HighlightedResourceCount;
				totalHighlightedDocumentCount += _termHighlighterService.HighlightedFileCount;
				totalHighlightTimeSpan = totalHighlightTimeSpan.Add(_termHighlighterService.TermHighlightTimeSpan);
				totalResourceFileLoadTimeSpan = totalResourceFileLoadTimeSpan.Add(_termHighlighterService.ResourceFileLoadTimeSpan);
				double resourceHighlightAvg = ((_termHighlighterService.HighlightedResourceCount != 0) ? (_termHighlighterService.TermHighlightTimeSpan.TotalMilliseconds / (double)_termHighlighterService.HighlightedResourceCount) : 0.0);
				double resourceFileInsertAvg = ((_termHighlighterService.HighlightedFileCount != 0) ? (_termHighlighterService.ResourceFileLoadTimeSpan.TotalMilliseconds / (double)_termHighlighterService.HighlightedFileCount) : 0.0);
				double totalResourceHighlightAvg = ((totalHighlightedResourceCount != 0) ? (totalHighlightTimeSpan.TotalMilliseconds / (double)totalHighlightedResourceCount) : 0.0);
				double totalResourceFileInsertAvg = ((totalHighlightedDocumentCount != 0) ? (totalResourceFileLoadTimeSpan.TotalMilliseconds / (double)totalHighlightedDocumentCount) : 0.0);
				R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
				R2UtilitiesBase.Log.InfoFormat("HighlightedResourceCount: {0}", _termHighlighterService.HighlightedResourceCount);
				R2UtilitiesBase.Log.InfoFormat("HighlightedDocumentCount: {0}", _termHighlighterService.HighlightedFileCount);
				R2UtilitiesBase.Log.InfoFormat("IndexTimeSpan: {0:c}", _termHighlighterService.TermHighlightTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("ResourceFileLoadTimeSpan: {0:c}", _termHighlighterService.ResourceFileLoadTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("Resource Highlight Average: {0} ms", resourceHighlightAvg);
				R2UtilitiesBase.Log.InfoFormat("ResourceFile Insert Average: {0} ms", resourceFileInsertAvg);
				R2UtilitiesBase.Log.Info("+    +    +    +    +    +    +    +    +    +    +");
				R2UtilitiesBase.Log.InfoFormat("totalHighlightedResourceCount: {0}", totalHighlightedResourceCount);
				R2UtilitiesBase.Log.InfoFormat("totalHighlightedDocumentCount: {0}", totalHighlightedDocumentCount);
				R2UtilitiesBase.Log.InfoFormat("totalHighlightTimeSpan: {0:c}", totalHighlightTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("totalResourceFileLoadTimeSpan: {0:c}", totalResourceFileLoadTimeSpan);
				R2UtilitiesBase.Log.InfoFormat("Total Run Time: {0:c}", runtimeTimer.Elapsed);
				R2UtilitiesBase.Log.InfoFormat("Total Resource Highlight Average: {0} ms", totalResourceHighlightAvg);
				R2UtilitiesBase.Log.InfoFormat("Total ResourceFile Insert Average: {0} ms", totalResourceFileInsertAvg);
				R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
				if (_indexTermHighlightSettings.MaxIndexBatches <= 0 || _indexTermHighlightSettings.MaxIndexBatches != batchNumber)
				{
					continue;
				}
				R2UtilitiesBase.Log.InfoFormat("MAXIMUM HIGHLIGHTING BATCHES REACHED: {0}", _indexTermHighlightSettings.MaxIndexBatches);
				break;
			}
			runtimeTimer.Stop();
			base.TaskResult.Information = $"Resource Count: {totalHighlightedResourceCount}, Document Count: {totalHighlightedDocumentCount}, Highlight Time: {totalHighlightTimeSpan:c}, Database Update Time: {totalResourceFileLoadTimeSpan:c}, Total Task Time: {runtimeTimer.Elapsed:c}";
			R2UtilitiesBase.Log.InfoFormat("APP COMPLETE -- run time: {0:c}", runtimeTimer.Elapsed);
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
	}
}
