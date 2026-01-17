using System;
using System.Linq;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess.Mesh;
using R2Utilities.DataAccess.Terms;
using R2V2.Infrastructure.Logging;

namespace R2Utilities.Tasks.ContentTasks;

public class LoadDiseasesTask : TaskBase
{
	private readonly DiseaseNameDataService _diseaseNameDataService;

	private readonly DiseaseSynonymDataService _diseaseSynonymDataService;

	private readonly ILog<LoadDiseasesTask> _log;

	private readonly MeshDataService _meshDataService;

	public LoadDiseasesTask(ILog<LoadDiseasesTask> log, MeshDataService meshDataService, DiseaseNameDataService diseaseNameDataService, DiseaseSynonymDataService diseaseSynonymDataService)
		: base("LoadDiseasesTask", "-LoadDiseasesTask", "06", TaskGroup.ContentLoading, "Task for loading the diseases", enabled: true)
	{
		_log = log;
		_meshDataService = meshDataService;
		_diseaseNameDataService = diseaseNameDataService;
		_diseaseSynonymDataService = diseaseSynonymDataService;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will load the R2 disease tables from the MeSH database.";
		TaskResultStep step = AddTaskStep("Select MeSH Disease Terms");
		try
		{
			_diseaseNameDataService.MeshTerms = _meshDataService.SelectDiseaseTerms().ToList();
			step.Results = "MeSH disease term count: " + _diseaseNameDataService.MeshTerms.Count();
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
			step = AddTaskStep("Inactivate R2 DiseaseNames not present in MeSH database");
			step.Results = "R2 disease names inactivated count: " + _diseaseNameDataService.InactivateNonMeshDiseases();
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
			step = AddTaskStep("Update R2 DiseaseName Table with MeSH Terms");
			step.Results = "R2 disease names update/insert count: " + _diseaseNameDataService.UpdateDiseases(base.TaskName);
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
			step = AddTaskStep("Update Parent IDs in R2 DiseaseName Table");
			step.Results = "R2 disease names updated with parentid count: " + _diseaseNameDataService.UpdateParentDiseaseIds(base.TaskName);
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
			_diseaseSynonymDataService.DiseaseNames = _diseaseNameDataService.DiseaseNames;
			step = AddTaskStep("Select MeSH Disease Synonym Terms");
			_diseaseSynonymDataService.MeshTerms = _meshDataService.SelectDiseaseSynonymTerms();
			step.Results = "MeSH disease term synonym count: " + _diseaseSynonymDataService.MeshTerms.Count();
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
			step = AddTaskStep("Update R2 DiseaseSynonym Table with MeSH Terms");
			step.Results = "R2 disease synonyms update/insert count: " + _diseaseSynonymDataService.UpdateDiseaseSynonyms(base.TaskName);
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
			step = AddTaskStep("Inactivate R2 DiseaseSynonyms for Inactive Diseases");
			step.Results = "R2 disease synonyms updated count: " + _diseaseSynonymDataService.InactivateSynonymsForInactiveDiseases(base.TaskName);
			step.CompletedSuccessfully = true;
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
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

	private TaskResultStep AddTaskStep(string stepName)
	{
		_log.Info(stepName);
		TaskResultStep step = new TaskResultStep
		{
			Name = stepName,
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		return step;
	}
}
