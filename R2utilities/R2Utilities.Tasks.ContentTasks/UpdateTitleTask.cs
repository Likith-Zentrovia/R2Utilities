using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Tasks.ContentTasks.Services;

namespace R2Utilities.Tasks.ContentTasks;

public class UpdateTitleTask : TaskBase, ITask
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly ResourceCoreDataService _resourceCoreDataService;

	private readonly TitleXmlService _titleXmlService;

	private string _file;

	private bool _ignoreEqual;

	private Dictionary<string, string> _isbnAndTitles;

	private bool _isTestMode;

	private int _maxResourceId;

	private int _maxResources;

	private int _minResourceId;

	private bool _revertAll;

	public UpdateTitleTask(ResourceCoreDataService resourceCoreDataService, IR2UtilitiesSettings r2UtilitiesSettings, TitleXmlService titleXmlService)
		: base("UpdateTitleTask", "-UpdateTitleTask", "19", TaskGroup.ContentLoading, "Replaces the title in the XML files with the Rittenhouse Title.", enabled: true)
	{
		_resourceCoreDataService = resourceCoreDataService;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_titleXmlService = titleXmlService;
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		_isTestMode = GetArgumentBoolean("testmode", defaultValue: true);
		_revertAll = GetArgumentBoolean("revertAll", defaultValue: true);
		_ignoreEqual = GetArgumentBoolean("ignoreEqual", defaultValue: true);
		_file = GetArgument("file");
		_maxResources = GetArgumentInt32("maxResources", 100000);
		_minResourceId = GetArgumentInt32("minResourceId", 0);
		_maxResourceId = GetArgumentInt32("maxResourceId", 100000);
		R2UtilitiesBase.Log.InfoFormat(">>> _isTestMode: {0}, _revertAll: {1}, _resourceFileTableName: {2}", _isTestMode, _revertAll, _file);
		R2UtilitiesBase.Log.InfoFormat(">>> _minResourceId: {0}, _maxResourceId: {1}, _maxResources: {2}", _minResourceId, _maxResourceId, _maxResources);
		if (base.CommandLineArguments.Length <= 1)
		{
			return;
		}
		string[] args = base.CommandLineArguments;
		string isbn = null;
		_isbnAndTitles = new Dictionary<string, string>();
		string[] array = args;
		foreach (string commandLineArgument in array)
		{
			if (commandLineArgument.Contains("-isbn="))
			{
				string arg = commandLineArgument.Replace("-isbn=", "");
				isbn = arg;
			}
			else if (commandLineArgument.Contains("-title="))
			{
				string arg2 = commandLineArgument.Replace("-title=", "");
				string title = arg2;
				if (!string.IsNullOrWhiteSpace(isbn))
				{
					_isbnAndTitles.Add(isbn, title);
				}
			}
		}
		if (!_isbnAndTitles.Any())
		{
			_isbnAndTitles = null;
		}
	}

	public override void Run()
	{
		base.TaskResult.Information = base.TaskDescription;
		TaskResultStep step = new TaskResultStep
		{
			Name = "UpdateTitleTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		try
		{
			List<ResourceTitleChange> rittenhouseResourceTitles = GetRittenhouseResourceTitles();
			if (_revertAll)
			{
				int restoredResources = _titleXmlService.RestoreXmlFiles(rittenhouseResourceTitles, _isTestMode);
				step.Results = $"{restoredResources} Resources have had there XML reverted to the backup.";
			}
			else
			{
				int resourcesUpdated = 0;
				string resultFlatFile = StartResultFile();
				foreach (ResourceTitleChange item in rittenhouseResourceTitles)
				{
					TaskResultStep lastStep = _titleXmlService.UpdateTitleXml(item, base.TaskResult, _isTestMode);
					resourcesUpdated++;
					AppendResultToFile(resultFlatFile, item, lastStep);
				}
				step.Results = $"{resourcesUpdated} Resource Titles Updated";
			}
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

	private string StartResultFile()
	{
		string resultFlatFile = Path.Combine(_r2UtilitiesSettings.UpdateTitleTaskWorkingFolder, "UpdateTitleTask_Result_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt");
		StreamWriter file = new StreamWriter(resultFlatFile);
		file.WriteLine("ResourceId\tISBN\tNew Title\tOld Title\tComplete Status\tDescription");
		file.Close();
		return resultFlatFile;
	}

	private void AppendResultToFile(string fileName, ResourceTitleChange item, TaskResultStep step)
	{
		using StreamWriter sw = File.AppendText(fileName);
		sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", item.ResourceId, item.Isbn, item.GetNewTitle(), item.Title, step.CompletedSuccessfully ? "Success" : "Fail", step.Results.Replace("\r\n", " "));
	}

	private List<ResourceTitleChange> GetRittenhouseResourceTitles()
	{
		List<ResourceTitleChange> rittenhouseResourceTitles = ParseArgumentTitles();
		if (rittenhouseResourceTitles == null && _file == null && _isbnAndTitles == null)
		{
			rittenhouseResourceTitles = new List<ResourceTitleChange>();
			List<ResourceTitleChange> rittenhouseResourceTitlesFromDatabase = _resourceCoreDataService.GetRittenhouseTitles(_r2UtilitiesSettings.PreludeDataLinkedServer, _minResourceId, _maxResourceId);
			Dictionary<ResourceTitleUpdateType, List<ResourceTitleChange>> dictionary = new Dictionary<ResourceTitleUpdateType, List<ResourceTitleChange>>
			{
				{
					ResourceTitleUpdateType.Equal,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.Equal).ToList()
				},
				{
					ResourceTitleUpdateType.RittenhouseEqualR2TitleAndSub,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.RittenhouseEqualR2TitleAndSub).ToList()
				},
				{
					ResourceTitleUpdateType.R2EqualRittenhouseTitleAndSub,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.R2EqualRittenhouseTitleAndSub).ToList()
				},
				{
					ResourceTitleUpdateType.NotExist,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.NotExist).ToList()
				},
				{
					ResourceTitleUpdateType.DifferentSub,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.DifferentSub).ToList()
				},
				{
					ResourceTitleUpdateType.RittenhouseSubNull,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.RittenhouseSubNull).ToList()
				},
				{
					ResourceTitleUpdateType.R2SubNull,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.R2SubNull).ToList()
				},
				{
					ResourceTitleUpdateType.Other,
					rittenhouseResourceTitlesFromDatabase.Where((ResourceTitleChange x) => x.UpdateType == ResourceTitleUpdateType.Other).ToList()
				}
			};
			foreach (KeyValuePair<ResourceTitleUpdateType, List<ResourceTitleChange>> keyValuePair in dictionary)
			{
				R2UtilitiesBase.Log.Info($"{keyValuePair.Key}-Count:{keyValuePair.Value.Count}");
			}
			ResourceTitleUpdateType[] titleTypeToIgnore = new ResourceTitleUpdateType[2]
			{
				ResourceTitleUpdateType.NotExist,
				ResourceTitleUpdateType.Other
			};
			foreach (KeyValuePair<ResourceTitleUpdateType, List<ResourceTitleChange>> keyValuePair2 in dictionary)
			{
				if (!titleTypeToIgnore.Contains(keyValuePair2.Key))
				{
					rittenhouseResourceTitles.AddRange(keyValuePair2.Value);
				}
			}
		}
		if (rittenhouseResourceTitles == null)
		{
			return null;
		}
		if (_ignoreEqual)
		{
			rittenhouseResourceTitles = rittenhouseResourceTitles.Where((ResourceTitleChange x) => x.Title != x.GetNewTitle()).ToList();
		}
		if (rittenhouseResourceTitles.Count > _maxResources)
		{
			rittenhouseResourceTitles = rittenhouseResourceTitles.GetRange(0, _maxResources);
		}
		foreach (ResourceTitleChange rittenhouseResourceTitle in rittenhouseResourceTitles)
		{
			R2UtilitiesBase.Log.Info(rittenhouseResourceTitle.Isbn + " -- to be Updated");
		}
		return rittenhouseResourceTitles;
	}

	private List<ResourceTitleChange> ParseArgumentTitles()
	{
		List<ResourceTitleChange> rittenhouseResourceTitles = null;
		if (!string.IsNullOrWhiteSpace(_file))
		{
			FileInfo file = new FileInfo(_file);
			if (file.Exists)
			{
				JavaScriptSerializer js = new JavaScriptSerializer();
				List<TitleUpdateProduct> titleUpdateProducts = new List<TitleUpdateProduct>();
				using (TextReader tr = file.OpenText())
				{
					string line = tr.ReadLine();
					while (!string.IsNullOrWhiteSpace(line))
					{
						TitleUpdateProduct titleUpdateProduct = (TitleUpdateProduct)js.Deserialize(line, typeof(TitleUpdateProduct));
						titleUpdateProducts.Add(titleUpdateProduct);
						line = tr.ReadLine();
					}
				}
				_isbnAndTitles = titleUpdateProducts.ToDictionary((TitleUpdateProduct x) => x.Isbn, (TitleUpdateProduct y) => y.Title);
			}
		}
		if (_isbnAndTitles != null)
		{
			rittenhouseResourceTitles = _resourceCoreDataService.GetRittenhouseTitles(_r2UtilitiesSettings.PreludeDataLinkedServer, _isbnAndTitles);
		}
		return rittenhouseResourceTitles;
	}
}
