using System;
using System.Text;
using Newtonsoft.Json;

namespace R2Utilities.Tasks.ContentTasks;

public class ResourceRestoreResult
{
	public string Isbn { get; set; }

	public string BackupFileFullPath { get; set; }

	public string RestoreXmlDirectory { get; set; }

	public DateTime BackupFileDateTime { get; set; }

	public DateTime RestoreStartTime { get; set; }

	public DateTime RestoreEndTime { get; set; }

	public int XmlFileCount { get; set; }

	public int HtmlFileCount { get; set; }

	public int ImageFileCount { get; set; }

	public int MediaFileCount { get; set; }

	public bool WasRestoreSuccessful { get; set; }

	public string ErrorMessage { get; set; }

	public string ToDebugString()
	{
		StringBuilder sb = new StringBuilder("ResourceRestoreResult = [");
		sb.AppendFormat("BackupFileDateTime: {0}", Isbn);
		sb.AppendFormat(", BackupFileFullPath: {0}", BackupFileFullPath);
		sb.AppendFormat(", RestoreXmlDirectory: {0}", RestoreXmlDirectory);
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", BackupFileDateTime: {0}", BackupFileDateTime);
		sb.AppendFormat(", RestoreStartTime: {0}", RestoreStartTime);
		sb.AppendFormat(", RestoreEndTime: {0}", RestoreEndTime);
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", XmlFileCount: {0}", XmlFileCount);
		sb.AppendFormat(", HtmlFileCount: {0}", HtmlFileCount);
		sb.AppendFormat(", ImageFileCount: {0}", ImageFileCount);
		sb.AppendFormat(", MediaFileCount: {0}", MediaFileCount);
		sb.AppendLine().Append("\t");
		sb.AppendFormat(", WasRestoreSuccessful: {0}", WasRestoreSuccessful);
		sb.AppendFormat(", ErrorMessage: {0}", ErrorMessage);
		sb.Append("]");
		return sb.ToString();
	}

	public string ToJsonString()
	{
		return JsonConvert.SerializeObject(this, Formatting.Indented);
	}

	public static ResourceRestoreResult ParseJson(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return new ResourceRestoreResult();
		}
		return JsonConvert.DeserializeObject<ResourceRestoreResult>(json);
	}
}
