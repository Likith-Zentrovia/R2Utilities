namespace R2Utilities.Tasks.ContentTasks;

public enum InvalidReasonId
{
	NotDefined = -1,
	ResourceNotInIndex,
	ResourceDocIdsNotInDatabase,
	ResourceDocIdsDiffer,
	XmlFilesMissingForResource,
	HtmlFilesMissingForResource,
	HtmlFilesNotInIndex,
	IndexContainsMissingFiles,
	IndexContainsResourceWithInvalidPath
}
