using System;
using System.Diagnostics;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Tasks.ContentTasks.Services;

namespace R2Utilities.Tasks.ContentTasks;

public class DtSearchIndexCompressTask : TaskBase
{
	private readonly DtSearchService _dtSearchService;

	public DtSearchIndexCompressTask(DtSearchService dtSearchService)
		: base("DtSearchIndexCompressTask", "-DtSearchIndexCompressTask", "x20", TaskGroup.Deprecated, "Task to compress the dtSearch index", enabled: false)
	{
		_dtSearchService = dtSearchService;
	}

	public override void Run()
	{
		Stopwatch runtimeTimer = new Stopwatch();
		runtimeTimer.Start();
		TaskResultStep step = new TaskResultStep
		{
			Name = "CompressIndex",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		try
		{
			R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
			R2UtilitiesBase.Log.Info("COMPRESSING INDEX ... (please wait)");
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			_dtSearchService.CompressIndex();
			stopwatch.Stop();
			R2UtilitiesBase.Log.InfoFormat("Index compressed in {0:c}", stopwatch.Elapsed);
			R2UtilitiesBase.Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++");
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
		}
	}
}
