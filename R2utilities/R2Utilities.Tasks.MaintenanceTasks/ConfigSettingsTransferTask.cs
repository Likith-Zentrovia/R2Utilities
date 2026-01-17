using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.R2Utility;
using R2V2.Core.Configuration;

namespace R2Utilities.Tasks.MaintenanceTasks;

public class ConfigSettingsTransferTask : TaskBase, ITask
{
	private readonly IQueryable<DbConfigurationSetting> _configurationSettings;

	private string _file;

	public ConfigSettingsTransferTask(IQueryable<DbConfigurationSetting> configurationSettings)
		: base("ConfigSettingsTransfer", "-ConfigSettingsTransfer", "12", TaskGroup.ContentLoading, "Task to export or import Configuration Settings", enabled: true)
	{
		_configurationSettings = configurationSettings;
	}

	public new void Init(string[] commandLineArguments)
	{
		base.Init(commandLineArguments);
		string file = GetArgument("file");
		if (file.Contains("{"))
		{
			file = file.Replace("date", "0");
			_file = string.Format(file, $"_{DateTime.Now:yyyyMdd-HHmmss}");
		}
		else
		{
			_file = file;
		}
		R2UtilitiesBase.Log.Info("-job: ConfigSettingsTransferTask, -file: " + _file);
	}

	public override void Run()
	{
		base.TaskResult.Information = new StringBuilder().Append("This task will export or import the Configuration Settings from the RIT001 Database. ").Append("The data is written to a json file and the file will only be saved if the data is different than the previous file. ").ToString();
		TaskResultStep step = new TaskResultStep
		{
			Name = "ConfigSettingsTransfer",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			StringBuilder resultsBuilder = new StringBuilder();
			bool success = ExportFile(resultsBuilder);
			resultsBuilder.Append($"Export succeeded : {success}");
			step.Results = resultsBuilder.ToString();
			step.CompletedSuccessfully = success;
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

	public bool ExportFile(StringBuilder resultsBuilder)
	{
		try
		{
			R2UtilitiesBase.Log.Info("Start ExportFile()");
			List<DbConfigurationSetting> configurationSettings = _configurationSettings.ToList();
			R2UtilitiesBase.Log.Info("configurationSettings found");
			using (StreamWriter sw = new StreamWriter(_file))
			{
				foreach (DbConfigurationSetting configurationSetting in configurationSettings)
				{
					sw.WriteLine(configurationSetting.ToInsertString());
				}
			}
			R2UtilitiesBase.Log.Info("file has been written");
			FileInfo currentFile = new FileInfo(_file);
			if (currentFile.Exists && currentFile.DirectoryName != null)
			{
				string searchPattern = (currentFile.Name.Contains("_") ? currentFile.Name.Split('_').First() : currentFile.Name.Split('.').First());
				DirectoryInfo directory = new DirectoryInfo(currentFile.DirectoryName);
				FileInfo latestFile = (from f in directory.GetFiles(searchPattern + "*")
					where f.Name != currentFile.Name
					orderby f.LastWriteTime descending
					select f).FirstOrDefault();
				if (latestFile != null && File.ReadLines(currentFile.FullName).SequenceEqual(File.ReadLines(latestFile.FullName)))
				{
					R2UtilitiesBase.Log.Info("Configurations have NOT changed. Deleting file that was just created.");
					File.Delete(currentFile.FullName);
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			resultsBuilder.AppendLine(ex.Message + "\r\n");
		}
		return false;
	}
}
