using System.Collections.Generic;
using System.Linq;
using R2Utilities.DataAccess;
using R2V2.Core.Resource;

namespace R2Utilities.Tasks.ContentTasks;

public class IndexedResource
{
	public ResourceCore Resource { get; set; }

	public DocIds IndexDocIds { get; set; }

	public ResourceDocIds ResourceDocIds { get; set; }

	public IndexedResourceStatus IndexedResourceStatus { get; set; }

	public ResourceStatus ResourceStatus { get; set; }

	public bool SoftDeleted { get; set; }

	private bool DeleteAllDocIdsFromDb { get; set; }

	private bool DeleteAllDocIdsFromIndex { get; set; }

	private bool DeleteMissingDocIdsFromDb { get; set; }

	private bool DeleteMissingDocIdsFromIndex { get; set; }

	private bool AddToTransformQueue { get; set; }

	private bool AddToIndexQueue { get; set; }

	public string ActionDescription => IndexedResourceStatus switch
	{
		IndexedResourceStatus.NotAction => Resource.Isbn + " - NO ACTION REQUIRED", 
		IndexedResourceStatus.ResourceIsInactive => $"{Resource.Isbn} - RESOURCE IS INACTIVE, ResourceStatus: {ResourceStatus}, SoftDeleted: {SoftDeleted}", 
		_ => null, 
	};

	public IndexedResource(ResourceCore resource, IDictionary<string, DocIds> docIdsList, IList<ResourceDocIds> allResourceDocIds, ResourceContentStatus resourceContentStatus, bool forceTransform)
	{
		Resource = resource;
		ResourceStatus = (ResourceStatus)resource.StatusId;
		SoftDeleted = resource.RecordStatus == 0;
		IndexDocIds = docIdsList[resource.Isbn];
		ResourceDocIds = allResourceDocIds.SingleOrDefault((ResourceDocIds x) => x.Id == resource.Id);
		if ((ResourceStatus == ResourceStatus.Active || ResourceStatus == ResourceStatus.Archived) && !SoftDeleted)
		{
			if (resourceContentStatus.Status == ResourceContentStatusType.XmlDirectoryDoesNotExist || resourceContentStatus.Status == ResourceContentStatusType.XmlDirectoryIsEmpty)
			{
				IndexedResourceStatus = IndexedResourceStatus.XmlDirectoryMissingOrEmpty;
			}
			if (resourceContentStatus.Status == ResourceContentStatusType.MissingXmlFiles)
			{
				AddToTransformQueue = true;
				IndexedResourceStatus = IndexedResourceStatus.XmlFilesMissing;
			}
			if (resourceContentStatus.Status == ResourceContentStatusType.HtmlDirectoryDoesNotExist || resourceContentStatus.Status == ResourceContentStatusType.HtmlDirectoryIsEmpty || resourceContentStatus.Status == ResourceContentStatusType.MissingHtmlFiles || resourceContentStatus.Status == ResourceContentStatusType.MissingHtmlGlossaryFiles)
			{
				AddToTransformQueue = true;
				IndexedResourceStatus = IndexedResourceStatus.HtmlFilesMissing;
			}
			if (resourceContentStatus.Status == ResourceContentStatusType.IndexContainsMissingFiles)
			{
				DeleteMissingDocIdsFromIndex = true;
				AddToTransformQueue = forceTransform;
				IndexedResourceStatus = IndexedResourceStatus.IndexContainsMissingFiles;
			}
		}
		else
		{
			DeleteAllDocIdsFromDb = ResourceDocIds != null && ResourceDocIds.MaxDocId > 0;
			DeleteAllDocIdsFromIndex = IndexDocIds != null && IndexDocIds.Filenames.Count > 0;
			IndexedResourceStatus = IndexedResourceStatus.ResourceIsInactive;
		}
	}
}
