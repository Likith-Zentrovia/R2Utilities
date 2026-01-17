using R2Utilities.DataAccess;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class AuditFilesOnDiskResult
{
	public ResourceCore ResourceCore { get; set; }

	public bool ExceptionWhileProcessing { get; set; }

	public string ExceptionMessage { get; set; }

	public string ExceptionStackTrace { get; set; }

	public bool ResourcesWithoutTocXml { get; set; }

	public int FilesReferencedInTocXmlCount { get; set; }

	public int FilesOnDiskCount { get; set; }

	public int FilesNotInTocXmlCount { get; set; }

	public int FilesDateDiffersFromTocCount { get; set; }

	public int FilesNotOnDiskCount { get; set; }

	public int FilesConfirmedInTocXmlCount { get; set; }

	public int FilesToDeleteCount { get; set; }

	public bool ContainsExtraXmlFile => FilesNotInTocXmlCount > 0 && FilesDateDiffersFromTocCount > 0;

	public bool MissingXmlFile => FilesNotOnDiskCount > 0;
}
