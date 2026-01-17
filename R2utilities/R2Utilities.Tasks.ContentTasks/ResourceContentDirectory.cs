using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace R2Utilities.Tasks.ContentTasks;

public class ResourceContentDirectory
{
	private static readonly string[] ContentTypes = new string[4] { "xml", "html", "images", "media" };

	public string ContentType { get; }

	public DirectoryInfo ResourceDirectory { get; }

	public DirectoryInfo ContentTypeDirectory { get; private set; }

	public FileInfo[] Files { get; }

	public FileInfo NewestFile { get; }

	public ResourceContentDirectory(ResourceContentDirectoryType contentType, string contentTypeDirectory, string isbn)
	{
		ContentType = ContentTypes[(int)contentType];
		ContentTypeDirectory = new DirectoryInfo(contentTypeDirectory);
		string path = Path.Combine(contentTypeDirectory, isbn);
		ResourceDirectory = new DirectoryInfo(path);
		if (ResourceDirectory.Exists)
		{
			Files = ResourceDirectory.GetFiles();
			NewestFile = Files.OrderByDescending((FileInfo f) => f.LastWriteTimeUtc).FirstOrDefault();
		}
	}

	public bool IsRestoreRequired(FileInfo backupZipFileInfo)
	{
		if (!ResourceDirectory.Exists)
		{
			return false;
		}
		if (NewestFile == null)
		{
			return false;
		}
		return NewestFile.LastWriteTimeUtc < backupZipFileInfo.LastWriteTimeUtc;
	}

	public bool IsNewestFileNewer(DateTime lastWriteTimeUtc)
	{
		if (NewestFile == null)
		{
			return false;
		}
		return NewestFile.LastWriteTimeUtc > lastWriteTimeUtc || NewestFile.CreationTimeUtc > lastWriteTimeUtc;
	}

	public string ToDebugString()
	{
		StringBuilder sb = new StringBuilder("ResourceContentDirectory = [");
		sb.AppendFormat("ContentType: {0}", ContentType);
		sb.AppendFormat(", ResourceDirectory.FullName: {0}", ResourceDirectory.FullName);
		sb.AppendFormat(", ResourceDirectory.Exists: {0}", ResourceDirectory.Exists);
		sb.AppendFormat(", Files: {0}", (Files != null) ? Files.Length : 0);
		sb.AppendFormat(", NewestFile.FullName: {0}", (NewestFile == null) ? "" : NewestFile.FullName);
		sb.AppendFormat(", NewestFile.LastWriteTimeUtc: {0}", (NewestFile == null) ? "" : $"{NewestFile.LastWriteTimeUtc:u}");
		sb.Append("]");
		return sb.ToString();
	}

	public string ToJsonString()
	{
		return JsonConvert.SerializeObject(this);
	}
}
