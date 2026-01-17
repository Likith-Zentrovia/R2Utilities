using System;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.Services;
using R2Utilities.Tasks.ContentTasks.Xsl;

namespace R2Utilities.Tasks.ContentTasks;

public class TransformXmlTask : TaskBase, ITask
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly TransformXmlService _transformXmlService;

	private bool _emailErrorOnValidationWarnings;

	private string _isbn;

	private int _maxBatchSize;

	private int _maxResourceId;

	private int _minResourceId;

	private bool _orderBatchDescending;

	private ResourceCoreDataService _resourceCoreDataService;

	private TransformQueueDataService _transformQueueDataService;

	public TransformXmlTask(IR2UtilitiesSettings r2UtilitiesSettings, TransformXmlService transformXmlService)
		: base("TransformXmlTask", "-TransformXmlTask", "00", TaskGroup.ContentLoading, "Transforms resource XML based on TransformQueue table", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_transformXmlService = transformXmlService;
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_maxBatchSize = GetArgumentInt32("maxBatchSize", _r2UtilitiesSettings.HtmlIndexerBatchSize);
		_minResourceId = GetArgumentInt32("minResourceId", 0);
		_maxResourceId = GetArgumentInt32("maxResourceId", 100000);
		_orderBatchDescending = GetArgumentBoolean("orderBatchDescending", _r2UtilitiesSettings.OrderBatchDescending);
		_isbn = GetArgument("isbn");
		_emailErrorOnValidationWarnings = GetArgumentBoolean("emailErrorOnValidationWarnings", defaultValue: false);
		R2UtilitiesBase.Log.InfoFormat("-maxBatchSize: {0}, -minResourceId: {1}, -maxResourceId: {2}", _maxBatchSize, _minResourceId, _maxResourceId);
		R2UtilitiesBase.Log.InfoFormat("-orderBatchDescending: {0}, -isbn: {1}, -emailErrorOnValidationWarnings: {2}", _orderBatchDescending, _isbn, _emailErrorOnValidationWarnings);
		SetSummaryEmailSetting(includeOkTaskSteps: false, showStepTotals: true, 10);
	}

	public override void Run()
	{
		StringBuilder taskInfo = new StringBuilder();
		TaskResultStep summaryStep = new TaskResultStep
		{
			Name = "TransformSummary",
			StartTime = DateTime.Now,
			Results = string.Empty
		};
		base.TaskResult.AddStep(summaryStep);
		UpdateTaskResult();
		_resourceCoreDataService = new ResourceCoreDataService();
		_transformQueueDataService = new TransformQueueDataService();
		try
		{
			if (string.IsNullOrWhiteSpace(_isbn))
			{
				taskInfo.AppendFormat("Transform XML for up to {0} resources in the transform queue. ", _maxBatchSize);
				R2UtilitiesBase.Log.InfoFormat("Transform XML for up to {0} resources in the transform queue. ", _maxBatchSize);
				int transformQueueSize = _transformQueueDataService.GetQueueSize();
				R2UtilitiesBase.Log.InfoFormat("Transform Queue Size: {0} = number of resources to transform", transformQueueSize);
				TransformQueue transformQueue = _transformQueueDataService.GetNext(_orderBatchDescending, _minResourceId, _maxResourceId);
				int resourceCount = 0;
				summaryStep.Results = $"maxBatchSize: {_maxBatchSize}, _minResourceId: {_minResourceId}, _maxResourceId: {_maxResourceId} - ISNBs:";
				while (transformQueue != null && !string.IsNullOrWhiteSpace(transformQueue.Isbn))
				{
					resourceCount++;
					summaryStep.Results = summaryStep.Results + ((resourceCount == 1) ? "" : ",") + transformQueue.Isbn;
					TransformSingleResource(transformQueue.Isbn, resourceCount, transformQueue, transformQueue.Id, transformQueue.ResourceId);
					if (resourceCount >= _maxBatchSize)
					{
						R2UtilitiesBase.Log.InfoFormat("MAX BATCH SIZE REACHED: {0}", _maxBatchSize);
						break;
					}
					transformQueueSize = _transformQueueDataService.GetQueueSize();
					R2UtilitiesBase.Log.InfoFormat("Transform Queue Size: {0} = number of resources to transform", transformQueueSize);
					transformQueue = _transformQueueDataService.GetNext(_orderBatchDescending, _minResourceId, _maxResourceId);
				}
				if (resourceCount == 0)
				{
					TaskResultStep step = new TaskResultStep
					{
						Name = "Transform Queue contains zero resources to transform.",
						StartTime = DateTime.Now,
						CompletedSuccessfully = true,
						Results = "No resources",
						EndTime = DateTime.Now
					};
					base.TaskResult.AddStep(step);
					UpdateTaskResult();
				}
				else
				{
					summaryStep.Results = $"Transform count: {resourceCount}, {summaryStep.Results}";
				}
			}
			else
			{
				taskInfo.AppendFormat("Transform XML for ISBN: {0}", _isbn);
				TransformSingleResource(_isbn, 1, null, -1, -1);
				summaryStep.Results = "Single ISBN: " + _isbn;
			}
			summaryStep.CompletedSuccessfully = true;
			if (_transformXmlService.ValidationWarningMessages.Length > 0)
			{
				TaskResultStep validationWarningStep = new TaskResultStep
				{
					Name = "Validation Warning",
					StartTime = DateTime.Now,
					Results = _transformXmlService.ValidationWarningMessages,
					CompletedSuccessfully = !_emailErrorOnValidationWarnings,
					EndTime = DateTime.Now
				};
				base.TaskResult.AddStep(validationWarningStep);
				UpdateTaskResult();
			}
		}
		catch (Exception ex)
		{
			summaryStep.Results = "EXCEPTION: " + ex.Message + "\r\n\t" + summaryStep.Results;
			summaryStep.CompletedSuccessfully = false;
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			base.TaskResult.Information = taskInfo.ToString();
			summaryStep.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private void TransformSingleResource(string isbn, int resourceCount, TransformQueue transformQueue, int transformQueueId, int resourceId)
	{
		TaskResultStep step = new TaskResultStep
		{
			Name = $"Transform XML for {isbn}, Resource Id: {resourceId}, Transform Queue Id: {transformQueueId}",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		StringBuilder stepResults = new StringBuilder();
		try
		{
			R2UtilitiesBase.Log.Info("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
			R2UtilitiesBase.Log.InfoFormat("Processing {0} of {1}, ISBN: {2}", resourceCount, _maxBatchSize, isbn);
			stepResults = new StringBuilder();
			ResourceTransformData data = ProcessIsbn(isbn);
			stepResults.AppendLine(data.ToDebugString());
			step.CompletedSuccessfully = data.Successful;
			step.HasWarnings = data.HasWarning;
			if (transformQueue != null)
			{
				transformQueue.DateFinished = DateTime.Now;
				transformQueue.Status = (data.Successful ? "T" : ((data.TransferCount > 0) ? "F" : "E"));
				transformQueue.StatusMessage = $"{data.TransferCount} files transformed, {data.ErrorCount} errors, {data.ValidationFailureCount} validation failures";
				_transformQueueDataService.Update(transformQueue);
			}
			R2UtilitiesBase.Log.Info(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
		}
		catch (Exception ex)
		{
			step.CompletedSuccessfully = false;
			stepResults.AppendLine().AppendFormat("\tEXCEPTION: {0}", ex.Message);
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			step.Results = stepResults.ToString();
			UpdateTaskResult();
		}
	}

	private ResourceTransformData ProcessIsbn(string isbn)
	{
		ResourceCore resource = _resourceCoreDataService.GetResourceByIsbn(isbn, excludeForthcoming: true);
		return _transformXmlService.TransformResource(resource);
	}
}
