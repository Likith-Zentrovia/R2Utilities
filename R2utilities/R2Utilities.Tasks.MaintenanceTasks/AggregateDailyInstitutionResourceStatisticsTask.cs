using System;
using System.Diagnostics;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class AggregateDailyInstitutionResourceStatisticsTask : TaskBase
{
	private readonly EmailTaskService _emailTaskService;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly UtilitiesStatisticsService _utilitiesStatisticsService;

	public DateTime AggregateStartDate { get; set; }

	public int MaxCount { get; set; }

	public AggregateDailyInstitutionResourceStatisticsTask(UtilitiesStatisticsService utilitiesStatisticsService, EmailTaskService emailTaskService, IR2UtilitiesSettings r2UtilitiesSettings)
		: base("AggregateDailyInstitutionResourceStatisticsTask", "-AggregateDailyInstitutionResourceStatisticsTask", "15", TaskGroup.ContentLoading, "Task to aggregate daily institution resource data", enabled: true)
	{
		_utilitiesStatisticsService = utilitiesStatisticsService;
		_emailTaskService = emailTaskService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public override void Run()
	{
		AggregateStartDate = new DateTime(2009, 1, 13);
		MaxCount = 1000;
		base.TaskResult.Information = "This task will aggregate Institution Resource Statistics.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "DailyInstitutionResourceStatisticsTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int totalAggregateCount = 0;
		Stopwatch timer = new Stopwatch();
		timer.Start();
		try
		{
			DateTime? startDate = _emailTaskService.GetAggregateInstitutionResourceStatisticsStartDate();
			if (startDate.HasValue)
			{
				AggregateStartDate = startDate.Value;
			}
			DateTime hardEndDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
			int zeroCounter = 0;
			while (AggregateStartDate < hardEndDate)
			{
				Stopwatch subtimer = new Stopwatch();
				subtimer.Start();
				DateTime endDate = new DateTime(AggregateStartDate.Year, AggregateStartDate.Month, AggregateStartDate.Day).AddDays(1.0);
				int count = _utilitiesStatisticsService.AggregateInstitutionResourceStatisticsCount(AggregateStartDate, endDate);
				R2UtilitiesBase.Log.InfoFormat("Records Aggregated: {0} || Total time: {1} seconds", count, TimeSpan.FromMilliseconds(subtimer.ElapsedMilliseconds).TotalSeconds);
				totalAggregateCount += count;
				AggregateStartDate = AggregateStartDate.AddDays(1.0);
				if (count == 0)
				{
					zeroCounter++;
				}
				if (zeroCounter > 7)
				{
					break;
				}
			}
			int rowsUpdated = 0;
			if (_r2UtilitiesSettings.UpdateInstitutionStatisticsPreviousDays > 0)
			{
				DateTime updateStartDate = new DateTime(hardEndDate.Year, hardEndDate.Month, hardEndDate.Day).AddDays(-_r2UtilitiesSettings.UpdateInstitutionStatisticsPreviousDays);
				rowsUpdated = _utilitiesStatisticsService.UpdateInstitutionResourceStatisticsCount(updateStartDate, hardEndDate);
			}
			_utilitiesStatisticsService.RebuildAndReorgIndexes();
			StringBuilder stepResults = new StringBuilder();
			stepResults.AppendFormat("{0} Total items aggregated (INSERT).", totalAggregateCount).AppendLine();
			stepResults.AppendFormat("{0} Total items aggregated (UPDATED).", rowsUpdated).AppendLine();
			stepResults.AppendFormat("Total time: {0} seconds, or {1} min", TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds).TotalSeconds, TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds).TotalMinutes).AppendLine();
			step.Results = stepResults.ToString();
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
			R2UtilitiesBase.Log.InfoFormat("Total time: {0} ms, or {1} min", totalAggregateCount, TimeSpan.FromMilliseconds(totalAggregateCount).TotalMinutes);
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}
}
