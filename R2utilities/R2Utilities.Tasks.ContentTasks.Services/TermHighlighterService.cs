using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;
using R2Utilities.DataAccess.Terms;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class TermHighlighterService : R2UtilitiesBase
{
	private const string TermHighlightStatusCodeError = "E";

	private const string TermHighlightStatusCodeProcessing = "P";

	private const string TermHighlightStatusCodeHighlighted = "H";

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private ITermHighlightSettings _termHighlightSettings;

	private readonly HitHighlighter _hitHighlighter;

	private readonly ResourceCoreDataService _resourceCoreDataService;

	private TermHighlightQueueDataService _termHighlightQueueDataService;

	public int HighlightedResourceCount { get; private set; }

	public int HighlightedFileCount { get; private set; }

	public TimeSpan TermHighlightTimeSpan { get; set; }

	public TimeSpan ResourceFileLoadTimeSpan { get; set; }

	public TermHighlighterService(IR2UtilitiesSettings r2UtilitiesSettings, HitHighlighter hitHighlighter)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_hitHighlighter = hitHighlighter;
		_resourceCoreDataService = new ResourceCoreDataService();
	}

	public void Init(ITermHighlightSettings termHighlightSettings, ITermDataService termDataService, string taskName)
	{
		_termHighlightSettings = termHighlightSettings;
		_termHighlightQueueDataService = new TermHighlightQueueDataService(_termHighlightSettings.TermHighlightType);
		_hitHighlighter.Init(_termHighlightSettings, termDataService, taskName);
	}

	public bool ProcessNextBatch(TaskResult taskResult)
	{
		TermHighlightTimeSpan = default(TimeSpan);
		ResourceFileLoadTimeSpan = default(TimeSpan);
		TaskResultStep step = new TaskResultStep
		{
			Name = "TermHighlightBatch",
			StartTime = DateTime.Now
		};
		taskResult.AddStep(step);
		StringBuilder stepResults = new StringBuilder();
		try
		{
			int termHighlightQueueSize = _termHighlightQueueDataService.GetTermHighlightQueueSize();
			R2UtilitiesBase.Log.DebugFormat("termHighlightQueueSize: {0}", termHighlightQueueSize);
			int resourceCount = 0;
			int maxBatchSize = _termHighlightSettings.BatchSize;
			R2UtilitiesBase.Log.InfoFormat(">>>>>>>>>> HIGHLIGHTING UP TO {0} RESOURCES <<<<<<<<<<", maxBatchSize);
			int batchResourceCount = 0;
			int batchFileCount = 0;
			DateTime timestamp = DateTime.Now;
			TermHighlightQueue queue;
			do
			{
				if (resourceCount >= maxBatchSize)
				{
					R2UtilitiesBase.Log.InfoFormat("MAX BATCH SIZE REACHED: {0}", maxBatchSize);
					break;
				}
				queue = _termHighlightQueueDataService.GetNext(_r2UtilitiesSettings.OrderBatchDescending);
				if (queue != null)
				{
					resourceCount++;
					R2UtilitiesBase.Log.InfoFormat("Processing {0} out of a possible {1} resources", resourceCount, maxBatchSize);
					stepResults.AppendFormat("\tISBN: {0}", string.Join(",", queue.Isbn)).AppendLine();
					Stopwatch termHighlightTimer = new Stopwatch();
					termHighlightTimer.Start();
					int highlightedFileCount;
					bool termHighlightWasSuccessful = HighlightTerms(queue, timestamp, out highlightedFileCount);
					termHighlightTimer.Stop();
					if (termHighlightWasSuccessful)
					{
						HighlightedResourceCount++;
						HighlightedFileCount += highlightedFileCount;
						batchResourceCount++;
						batchFileCount += highlightedFileCount;
					}
					TermHighlightTimeSpan = termHighlightTimer.Elapsed;
					UpdateQueue(queue, taskResult, highlightedFileCount);
					step.CompletedSuccessfully = true;
				}
			}
			while (queue != null);
			if (resourceCount == 0)
			{
				R2UtilitiesBase.Log.Info("No more resources to highlight");
				step.CompletedSuccessfully = true;
				return false;
			}
			R2UtilitiesBase.Log.InfoFormat("Highlighting {0} resources, {1:#,###} files", batchResourceCount, batchFileCount);
			R2UtilitiesBase.Log.InfoFormat("Highlighting {0} total resources, {1:#,###} total files", HighlightedResourceCount, HighlightedFileCount);
			return resourceCount > 0;
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

	private bool HighlightTerms(TermHighlightQueue termHighlightQueue, DateTime timestamp, out int highlightedFileCount)
	{
		try
		{
			highlightedFileCount = 0;
			termHighlightQueue.TermHighlightStatus = "P";
			termHighlightQueue.DateStarted = DateTime.Now;
			_termHighlightQueueDataService.Update(termHighlightQueue);
			ResourceToHighlight resourceToHighlight = new ResourceToHighlight(_termHighlightSettings, termHighlightQueue, timestamp);
			if (_termHighlightSettings.TermHighlightType == TermHighlightType.IndexTerms)
			{
			}
			resourceToHighlight.ResourceCore = _resourceCoreDataService.GetResourceByIsbn(termHighlightQueue.Isbn, excludeForthcoming: true);
			string resourceLocation = resourceToHighlight.ResourceLocation;
			string outputLocation = resourceToHighlight.OutputLocation;
			string backupLocation = resourceToHighlight.BackupLocation;
			DirectoryInfo directoryInfo = new DirectoryInfo(resourceLocation);
			if (!directoryInfo.Exists)
			{
				R2UtilitiesBase.Log.WarnFormat("DIRECTORY DOES NOT EXIST! path: {0}", directoryInfo);
				termHighlightQueue.TermHighlightStatus = "E";
				termHighlightQueue.StatusMessage = $"DIRECTORY DOES NOT EXIST! path: {directoryInfo}";
				return false;
			}
			FileInfo[] files = directoryInfo.GetFiles();
			if (files.Length == 0)
			{
				R2UtilitiesBase.Log.WarnFormat("DIRECTORY IS EMPTY! path: {0}", directoryInfo);
				termHighlightQueue.TermHighlightStatus = "E";
				termHighlightQueue.StatusMessage = $"DIRECTORY IS EMPTY! path: {directoryInfo}";
				return false;
			}
			R2UtilitiesBase.Log.DebugFormat("{0} files in directory '{1}'", files.Length, resourceLocation);
			Directory.CreateDirectory(backupLocation);
			Directory.CreateDirectory(outputLocation);
			Stopwatch termHighlightTimer = new Stopwatch();
			bool isSuccessful;
			try
			{
				termHighlightTimer.Start();
				_hitHighlighter.HighlightResource(resourceToHighlight);
				isSuccessful = true;
				termHighlightQueue.TermHighlightStatus = "H";
				termHighlightTimer.Stop();
			}
			catch (Exception ex)
			{
				isSuccessful = false;
				R2UtilitiesBase.Log.Error(ex.Message, ex);
				termHighlightQueue.TermHighlightStatus = "E";
			}
			highlightedFileCount = _hitHighlighter.ResourceToHighlight.HighlightedFileCount;
			int totalFileCount = _hitHighlighter.ResourceToHighlight.TotalFileCount;
			int errorCount = ((!isSuccessful) ? 1 : 0);
			long avgHighlightTime = ((highlightedFileCount == 0) ? termHighlightTimer.ElapsedMilliseconds : (termHighlightTimer.ElapsedMilliseconds / highlightedFileCount));
			R2UtilitiesBase.Log.InfoFormat("{0} of {1} files highlighted successfully in {2:c}, {3} error(s)", highlightedFileCount, totalFileCount, termHighlightTimer.Elapsed, errorCount);
			R2UtilitiesBase.Log.InfoFormat("Avg Highlight Time: {0} ms", avgHighlightTime);
			termHighlightQueue.StatusMessage = $"{highlightedFileCount} of {totalFileCount} files highlighted successfully in {termHighlightTimer.Elapsed:c}, {errorCount} error(s), Avg Highlight Time: {avgHighlightTime} ms";
			if (_termHighlightSettings.TermHighlightType == TermHighlightType.Tabers && _termHighlightSettings.UpdateResourceStatus)
			{
				R2UtilitiesBase.Log.Info("Updating Resource Tabers Status");
				_resourceCoreDataService.SetResourceTabersStatus(termHighlightQueue.ResourceId, resourceTabersStatus: true, "TermHighlighter");
			}
			return isSuccessful;
		}
		catch (Exception ex2)
		{
			R2UtilitiesBase.Log.Error(ex2.Message, ex2);
			throw;
		}
	}

	private void UpdateQueue(TermHighlightQueue termHighlightQueue, TaskResult taskResult, int highlightedFileCount)
	{
		Stopwatch insertTimer = new Stopwatch();
		TaskResultStep updateQueueStep = new TaskResultStep
		{
			Name = $"Updating queue for ISBN: {termHighlightQueue.Isbn}, resource id: {termHighlightQueue.ResourceId}, term highlight queue id: {termHighlightQueue.Id}",
			StartTime = DateTime.Now
		};
		taskResult.AddStep(updateQueueStep);
		R2UtilitiesBase.Log.Info(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
		R2UtilitiesBase.Log.InfoFormat("Queue Id: {0}, ISBN: {1}", termHighlightQueue.Id, termHighlightQueue.Isbn);
		termHighlightQueue.DateFinished = DateTime.Now;
		_termHighlightQueueDataService.Update(termHighlightQueue);
		updateQueueStep.CompletedSuccessfully = termHighlightQueue.TermHighlightStatus != "E";
		updateQueueStep.EndTime = DateTime.Now;
		updateQueueStep.Results = termHighlightQueue.StatusMessage;
		long insertElapsed = insertTimer.ElapsedMilliseconds;
		double avgInsertTimePerFile = (double)insertElapsed / (double)highlightedFileCount;
		R2UtilitiesBase.Log.DebugFormat("insertElapsed: {0:0,000} ms, avgInsertTimePerFile: {1:0.000} ms", insertElapsed, avgInsertTimePerFile);
		R2UtilitiesBase.Log.Info("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
	}
}
