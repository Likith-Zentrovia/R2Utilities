using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2V2.Core.Reports;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class AggregateInstitutionStatisticsTask : TaskBase
{
	private readonly DashboardService _dashboardService;

	private readonly UtilitiesStatisticsService _utilitiesStatisticsService;

	public AggregateInstitutionStatisticsTask(UtilitiesStatisticsService utilitiesStatisticsService, DashboardService dashboardService)
		: base("AggregateInstitutionStatisticsTask", "-AggregateInstitutionStatisticsTask", "14", TaskGroup.ContentLoading, "Aggregates institution data for Dashboard Statistics", enabled: true)
	{
		_utilitiesStatisticsService = utilitiesStatisticsService;
		_dashboardService = dashboardService;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will aggregate Institution Statistics.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "InstitutionStatisticsTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		int totalResourcesInserted = 0;
		int totalInstitutionMonthsAggregated = 0;
		try
		{
			List<InstitutionStatistics> institutionStatisticsList = _dashboardService.GetInstitutionsForStatistics();
			foreach (InstitutionStatistics institutionStatistics in institutionStatisticsList)
			{
				Stopwatch timer = new Stopwatch();
				timer.Start();
				InstitutionStatistics institutionStatisticsToRun = institutionStatistics;
				DateTime maxDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
				while (institutionStatisticsToRun.StartDate != maxDate)
				{
					Stopwatch subTimer = new Stopwatch();
					subTimer.Start();
					int tryCount = 0;
					bool success = false;
					try
					{
						while (!success && tryCount < 3)
						{
							institutionStatisticsToRun = _dashboardService.GetAggregatedInstitutionStatistics(institutionStatisticsToRun);
							success = _utilitiesStatisticsService.InsertInstitutionStatistics(institutionStatisticsToRun);
							if (success)
							{
								totalInstitutionMonthsAggregated++;
								R2UtilitiesBase.Log.InfoFormat("Stats for Institution: {0}. Date Aggregated :{1} it took: {2}ms", institutionStatisticsToRun.InstitutionId, institutionStatisticsToRun.StartDate, subTimer.ElapsedMilliseconds);
								subTimer.Restart();
								totalResourcesInserted += _utilitiesStatisticsService.InsertMonthlyResourceStatistics(institutionStatisticsToRun);
								institutionStatisticsToRun = new InstitutionStatistics
								{
									InstitutionId = institutionStatistics.InstitutionId,
									StartDate = institutionStatisticsToRun.StartDate.AddMonths(1)
								};
							}
							else
							{
								R2UtilitiesBase.Log.InfoFormat("FAIL!!! -- InstitutionId: {0}, StatisticStartDate: {1}", institutionStatisticsToRun.InstitutionId, institutionStatisticsToRun.StartDate);
							}
							tryCount++;
						}
					}
					catch (Exception ex)
					{
						R2UtilitiesBase.Log.Info(ex.Message, ex);
					}
					subTimer.Restart();
				}
				R2UtilitiesBase.Log.InfoFormat("It took: {1}ms to Aggrate All Stats for Institution {0}", institutionStatisticsToRun.InstitutionId, timer.ElapsedMilliseconds);
				timer.Restart();
			}
			StringBuilder stepResults = new StringBuilder();
			stepResults.AppendFormat("{0} Total institutions aggregated.", totalResourcesInserted).AppendLine();
			stepResults.AppendFormat("{0} Total months aggregated for all institutions.", totalInstitutionMonthsAggregated).AppendLine();
			stepResults.AppendFormat("{0} Total resources aggregated for institutions.", totalResourcesInserted).AppendLine();
			step.Results = stepResults.ToString();
			step.CompletedSuccessfully = true;
		}
		catch (Exception ex2)
		{
			R2UtilitiesBase.Log.Error(ex2.Message, ex2);
			step.CompletedSuccessfully = false;
			step.Results = ex2.Message;
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}
}
