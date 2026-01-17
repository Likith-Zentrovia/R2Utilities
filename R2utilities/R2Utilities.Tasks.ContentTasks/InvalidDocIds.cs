using R2Utilities.DataAccess;

namespace R2Utilities.Tasks.ContentTasks;

public class InvalidDocIds
{
	public ResourceCore Resource { get; set; }

	public DocIds IndexDocIds { get; set; }

	public ResourceDocIds ResourceDocIds { get; set; }

	public InvalidReasonId InvalidReasonId { get; set; }

	public string InvalidReason { get; set; }

	public InvalidDocIds()
	{
	}

	public InvalidDocIds(ResourceCore resource, InvalidReasonId invalidReasonId, string invalidReason, DocIds indexDocIds = null, ResourceDocIds resourceDocIds = null)
	{
		Resource = resource;
		IndexDocIds = indexDocIds;
		ResourceDocIds = resourceDocIds;
		InvalidReasonId = invalidReasonId;
		InvalidReason = invalidReason;
	}
}
