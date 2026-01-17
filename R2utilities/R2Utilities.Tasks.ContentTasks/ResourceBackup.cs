using System.IO;
using System.Text;
using Newtonsoft.Json;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Resource;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks;

public class ResourceBackup
{
	public string Isbn { get; }

	public ResourceContentDirectory Xml { get; }

	public ResourceContentDirectory Html { get; }

	public ResourceContentDirectory Images { get; }

	public ResourceContentDirectory Media { get; }

	public FileInfo BookCoverImage { get; }

	public FileInfo BackupZipFile { get; }

	public bool ResourceBackupRequired { get; }

	public ResourceBackup(string baseFilePath, string backupLocation, string isbn)
	{
		Isbn = isbn;
		Xml = new ResourceContentDirectory(ResourceContentDirectoryType.Xml, baseFilePath, Isbn);
		string zipFilePath = Path.Combine(backupLocation, Isbn + ".zip");
		BackupZipFile = new FileInfo(zipFilePath);
	}

	public ResourceBackup(IResource resource, IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings)
	{
		Isbn = resource.Isbn;
		Xml = new ResourceContentDirectory(ResourceContentDirectoryType.Xml, contentSettings.ContentLocation, Isbn);
		Html = new ResourceContentDirectory(ResourceContentDirectoryType.Html, Path.Combine(contentSettings.NewContentLocation, "html"), Isbn);
		Images = new ResourceContentDirectory(ResourceContentDirectoryType.Images, contentSettings.ImageBaseFileLocation, Isbn);
		Media = new ResourceContentDirectory(ResourceContentDirectoryType.Media, Path.Combine(contentSettings.NewContentLocation, "media"), Isbn);
		string bookCoverImageRootPath = Path.Combine(contentSettings.ImageBaseFileLocation, "book-covers");
		if (!string.IsNullOrEmpty(resource.ImageFileName))
		{
			string bookCoverImagePath = Path.Combine(bookCoverImageRootPath, resource.ImageFileName);
			BookCoverImage = new FileInfo(bookCoverImagePath);
		}
		else
		{
			string bookCoverImagePath2 = Path.Combine(bookCoverImageRootPath, Isbn + ".jpg");
			if (File.Exists(bookCoverImagePath2))
			{
				BookCoverImage = new FileInfo(bookCoverImagePath2);
			}
			else
			{
				bookCoverImagePath2 = Path.Combine(bookCoverImageRootPath, Isbn + ".gif");
				BookCoverImage = new FileInfo(bookCoverImagePath2);
			}
		}
		string zipFilePath = Path.Combine(r2UtilitiesSettings.ContentBackupDirectory, Isbn + ".zip");
		BackupZipFile = new FileInfo(zipFilePath);
		if (!BackupZipFile.Exists)
		{
			ResourceBackupRequired = true;
		}
		else
		{
			ResourceBackupRequired = Xml.IsNewestFileNewer(BackupZipFile.LastWriteTimeUtc) || Html.IsNewestFileNewer(BackupZipFile.LastWriteTimeUtc) || Images.IsNewestFileNewer(BackupZipFile.LastWriteTimeUtc) || Media.IsNewestFileNewer(BackupZipFile.LastWriteTimeUtc) || IsBookCoverImageNewer();
		}
	}

	private bool IsBookCoverImageNewer()
	{
		if (!BookCoverImage.Exists)
		{
			return false;
		}
		return BookCoverImage.CreationTimeUtc > BackupZipFile.CreationTimeUtc || BookCoverImage.LastWriteTimeUtc > BackupZipFile.LastWriteTimeUtc;
	}

	public string ToDebugString()
	{
		StringBuilder sb = new StringBuilder("ResourcePaths = [");
		sb.AppendFormat("Isbn: {0}", Isbn);
		sb.AppendFormat(", ResourceBackupRequired: {0}", ResourceBackupRequired);
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", Xml: {0}", (Xml == null) ? "null" : Xml.ToDebugString());
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", Html: {0}", (Html == null) ? "null" : Html.ToDebugString());
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", Images: {0}", (Images == null) ? "null" : Images.ToDebugString());
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", Media: {0}", (Media == null) ? "null" : Media.ToDebugString());
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", BookCoverImage.FullName: {0}", (BookCoverImage == null) ? "" : BookCoverImage.FullName);
		sb.AppendFormat(", BookCoverImage.CreationTimeUtc: {0}", (BookCoverImage == null) ? "" : $"{BookCoverImage.CreationTimeUtc:u}");
		sb.AppendFormat(", BookCoverImage.LastWriteTimeUtc: {0}", (BookCoverImage == null) ? "" : $"{BookCoverImage.LastWriteTimeUtc:u}");
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", BackupZipFile.FullName: {0}", (BackupZipFile == null) ? "" : BackupZipFile.FullName);
		sb.AppendFormat(", BackupZipFile.CreationTimeUtc: {0}", (BackupZipFile == null) ? "" : $"{BackupZipFile.CreationTimeUtc:u}");
		sb.AppendFormat(", BackupZipFile.LastWriteTimeUtc: {0}", (BackupZipFile == null) ? "" : $"{BackupZipFile.LastWriteTimeUtc:u}");
		sb.AppendLine().Append("\t");
		sb.Append("]");
		return sb.ToString();
	}

	public string ToJsonString()
	{
		return JsonConvert.SerializeObject(this);
	}
}
