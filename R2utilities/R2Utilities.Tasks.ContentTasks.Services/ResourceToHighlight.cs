using System;
using System.Collections.Generic;
using System.IO;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Compression;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class ResourceToHighlight : R2UtilitiesBase
{
	private readonly string _contentLocation;

	private readonly string _outputLocation;

	private readonly string _backupLocation;

	private readonly string _timestamp;

	private readonly ContentSettings _contentSettings = new ContentSettings();

	private readonly List<ResourceFile> _resourceFiles = new List<ResourceFile>();

	public IEnumerable<ResourceFile> ResourceFiles => _resourceFiles;

	public TermHighlightQueue TermHighlightQueue { get; }

	public ResourceCore ResourceCore { get; set; }

	public List<ContentToHighlight> Content { get; set; }

	public HashSet<string> Words { get; set; }

	public HashSet<string> Keywords { get; set; }

	public string ResourceLocation => Path.Combine(_contentLocation, TermHighlightQueue.Isbn);

	public string OutputLocation => GetPath(_outputLocation);

	public string BackupLocation => GetPath(_backupLocation);

	public string TempLocation => Path.Combine(OutputLocation, "Temp");

	public int TotalFileCount { get; set; }

	public int HighlightedFileCount { get; set; }

	public ResourceToHighlight(ITermHighlightSettings termHighlightSettings, TermHighlightQueue termHighlightQueue, DateTime timestamp)
	{
		TermHighlightQueue = termHighlightQueue;
		_contentLocation = _contentSettings.ContentLocation;
		_outputLocation = termHighlightSettings.OutputLocation;
		_backupLocation = termHighlightSettings.BackupLocation;
		_timestamp = $"{timestamp:yyyy-MM-dd_HH-mm-ss-tt}";
		Content = new List<ContentToHighlight>();
		Keywords = new HashSet<string>();
		InitializeContent();
	}

	public void AddResourceFile(ResourceFile resourceFile)
	{
		resourceFile.ResourceId = TermHighlightQueue.ResourceId;
		if (TermHighlightQueue.FirstDocumentId <= 0 || TermHighlightQueue.FirstDocumentId > resourceFile.DocumentId)
		{
			TermHighlightQueue.FirstDocumentId = resourceFile.DocumentId;
		}
		if (TermHighlightQueue.LastDocumentId <= 0 || TermHighlightQueue.LastDocumentId < resourceFile.DocumentId)
		{
			TermHighlightQueue.LastDocumentId = resourceFile.DocumentId;
		}
		_resourceFiles.Add(resourceFile);
	}

	public void LoadContent(bool removeComments = false)
	{
		foreach (ContentToHighlight content in Content)
		{
			content.Load();
			Keywords.UnionWith(content.Keywords);
		}
	}

	public void WriteTempContent()
	{
		Directory.CreateDirectory(TempLocation);
		foreach (ContentToHighlight content in Content)
		{
			File.WriteAllText(content.TempPath, content.TempContent);
		}
	}

	public void DeleteTempContent()
	{
		Directory.Delete(TempLocation, recursive: true);
	}

	public void WriteResourceBackup()
	{
		foreach (ContentToHighlight content in Content)
		{
			File.Copy(content.ResourcePath, content.BackupPath);
		}
	}

	public void ZipResourceBackup()
	{
		ZipHelper.CompressDirectory(BackupLocation);
		Directory.Delete(BackupLocation, recursive: true);
	}

	private void InitializeContent()
	{
		string[] filePaths = Directory.GetFiles(ResourceLocation);
		string[] array = filePaths;
		foreach (string filePath in array)
		{
			string fileName = new FileInfo(filePath).Name;
			ContentToHighlight content = new ContentToHighlight
			{
				FileName = fileName,
				ResourcePath = filePath,
				OutputPath = Path.Combine(OutputLocation, fileName),
				BackupPath = Path.Combine(BackupLocation, fileName),
				TempPath = Path.Combine(TempLocation, fileName),
				TermHighlightType = TermHighlightQueue.TermHighlightType
			};
			Content.Add(content);
		}
		TotalFileCount = filePaths.Length;
	}

	private string GetPath(string location)
	{
		return Path.Combine(location, "Job " + TermHighlightQueue.JobId, "Batch - " + _timestamp, TermHighlightQueue.Isbn);
	}
}
