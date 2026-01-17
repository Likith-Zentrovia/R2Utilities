namespace R2Utilities.Tasks.ContentTasks;

public enum ResourceContentStatusType
{
	Unknown,
	Exception,
	Ok,
	XmlAndHtmlOk,
	XmlDirectoryDoesNotExist,
	XmlDirectoryIsEmpty,
	HtmlDirectoryDoesNotExist,
	HtmlDirectoryIsEmpty,
	MissingXmlFiles,
	MissingHtmlFiles,
	IndexContainsMissingFiles,
	HtmlFilesNotInIndex,
	MissingHtmlGlossaryFiles
}
