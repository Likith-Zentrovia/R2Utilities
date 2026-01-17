using System;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Tasks.ContentTasks.Services;

namespace R2Utilities.Tasks.ContentTasks;

public class IndexStatusTask : TaskBase
{
	private readonly DtSearchService _dtSearchService;

	public IndexStatusTask(DtSearchService dtSearchService)
		: base("IndexStatusTask", "-IndexStatusTask", "21", TaskGroup.DiagnosticsMaintenance, "Task to report the status of the dtSearch index", enabled: true)
	{
		_dtSearchService = dtSearchService;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will log the dtSearch index status";
		TaskResultStep step = new TaskResultStep
		{
			Name = "IndexStatusTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		try
		{
			UpdateTaskResult();
			string indexStatus = _dtSearchService.GetIndexStatus();
			R2UtilitiesBase.Log.Info(indexStatus);
			step.Results = indexStatus;
			step.CompletedSuccessfully = true;
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
}
