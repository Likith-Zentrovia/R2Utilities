using System;
using System.Collections.Generic;
using System.Linq;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.Services;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class UpdateTocXmlTask : TaskBase
{
	private readonly IContentSettings _contentSettings;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly IQueryable<Resource> _resources;

	private readonly TocXmlService _tocXmlService;

	private int _errorCount;

	private string _isbns;

	private int _maxResourceId = 999999;

	private int _maxResourcesToProcess = 25;

	private int _minResourceId = 1;

	private int _resourcesProcessed;

	public UpdateTocXmlTask(IQueryable<Resource> resources, IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings, TocXmlService tocXmlService)
		: base("UpdateTocXmlTask", "-UpdateTocXmlTask", "17", TaskGroup.ContentLoading, "Bulk update toc.xml files", enabled: true)
	{
		_resources = resources;
		_contentSettings = contentSettings;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_tocXmlService = tocXmlService;
	}

	public override void Run()
	{
		base.TaskResult.Information = base.TaskDescription;
		TaskResultStep step = new TaskResultStep
		{
			Name = "UpdateTocXml",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		try
		{
			_isbns = GetArgument("isbns");
			int.TryParse(GetArgument("maxResourcesToProcess") ?? "0", out _maxResourcesToProcess);
			int.TryParse(GetArgument("minResourceId") ?? "1", out _minResourceId);
			int.TryParse(GetArgument("maxResourceId") ?? "999999", out _maxResourceId);
			UpdateTaskResult();
			ProcessResourceTocs();
			if (_errorCount > 0)
			{
				step.Results = $"Error - {_errorCount} errors, {_resourcesProcessed} resources processed";
				step.CompletedSuccessfully = false;
			}
			else
			{
				step.Results = $"OK - {_errorCount} errors, {_resourcesProcessed} resources processed";
				step.CompletedSuccessfully = true;
			}
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

	private void ProcessResourceTocs()
	{
		int[] validResourceStatusIds = new int[2] { 6, 7 };
		IOrderedQueryable<Resource> query = from r in _resources
			where r.Id >= _minResourceId
			where r.Id <= _maxResourceId
			where validResourceStatusIds.Contains(r.StatusId)
			orderby r.Id descending
			select r;
		if (!string.IsNullOrWhiteSpace(_isbns))
		{
			string[] isbns = _isbns.Split(',');
			query = from r in query
				where isbns.Contains(r.Isbn)
				orderby r.Id descending
				select r;
		}
		int max = ((_maxResourcesToProcess < 0) ? 999999 : _maxResourcesToProcess);
		List<Resource> resources = query.Take(max).ToList();
		foreach (Resource resource in resources)
		{
			_resourcesProcessed++;
			R2UtilitiesBase.Log.InfoFormat("Processing ISBN: {0}, [{1}], {2} of {3} resource", resource.Isbn, resource.Id, _resourcesProcessed, resources.Count());
			ResourceBackup resourcePaths = new ResourceBackup(resource, _contentSettings, _r2UtilitiesSettings);
			R2UtilitiesBase.Log.Debug(resourcePaths.ToDebugString());
			TaskResultStep step = _tocXmlService.UpdateTocXml(resource.Isbn, base.TaskResult, resource.Id);
			UpdateTaskResult();
			if (!step.CompletedSuccessfully)
			{
				_errorCount++;
			}
			if (_resourcesProcessed >= _maxResourcesToProcess)
			{
				R2UtilitiesBase.Log.Info("MAX RESOURCES PROCESSED!");
				break;
			}
		}
	}
}
