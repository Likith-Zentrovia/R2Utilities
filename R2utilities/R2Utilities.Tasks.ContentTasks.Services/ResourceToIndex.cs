using System.Collections.Generic;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class ResourceToIndex
{
	private readonly List<ResourceFile> _resourceFiles = new List<ResourceFile>();

	public IEnumerable<ResourceFile> ResourceFiles => _resourceFiles;

	public IndexQueue IndexQueue { get; }

	public ResourceToIndex(IndexQueue indexQueue)
	{
		IndexQueue = indexQueue;
	}

	public void AddResourceFile(ResourceFile resourceFile)
	{
		resourceFile.ResourceId = IndexQueue.ResourceId;
		if (IndexQueue.FirstDocumentId <= 0 || IndexQueue.FirstDocumentId > resourceFile.DocumentId)
		{
			IndexQueue.FirstDocumentId = resourceFile.DocumentId;
		}
		if (IndexQueue.LastDocumentId <= 0 || IndexQueue.LastDocumentId < resourceFile.DocumentId)
		{
			IndexQueue.LastDocumentId = resourceFile.DocumentId;
		}
		_resourceFiles.Add(resourceFile);
	}
}
