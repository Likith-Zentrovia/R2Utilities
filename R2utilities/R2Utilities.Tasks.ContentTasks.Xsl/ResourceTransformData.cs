using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Common.Logging;
using R2Library.Data.ADO.R2Utility;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess;

namespace R2Utilities.Tasks.ContentTasks.Xsl;

public class ResourceTransformData
{
	protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private readonly IList<string> _errorFiles = new List<string>();

	private readonly IList<string> _errorMesages = new List<string>();

	private readonly IList<DateTime> _errorTimestamps = new List<DateTime>();

	private readonly Dictionary<string, long> _oldHtmlFiles = new Dictionary<string, long>();

	private readonly TransformedResource _transformedResource = new TransformedResource();

	public string Isbn { get; }

	public ResourceCore Resource { get; }

	public DirectoryInfo HtmlDirectoryInfo { get; set; }

	public string StatusMessage { get; set; }

	public bool Successful { get; set; }

	public bool HasWarning { get; set; }

	public int TransferCount { get; set; }

	public int ErrorCount { get; private set; }

	public int ValidationFailureCount { get; private set; }

	public ResourceTransformData(ResourceCore resource, int transformedResourceId, string htmlRootPath)
	{
		Resource = resource;
		Isbn = (resource.Isbn ?? "").Trim();
		Log.DebugFormat("resource.Id: {0}, Isbn: '{1}'", resource.Id, Isbn);
		_transformedResource.ResourceId = Resource.Id;
		_transformedResource.Isbn = Resource.Isbn;
		_transformedResource.Id = transformedResourceId;
		if (!string.IsNullOrEmpty(Isbn))
		{
			string htmlDirectoryName = htmlRootPath + "\\" + Isbn;
			HtmlDirectoryInfo = new DirectoryInfo(htmlDirectoryName);
			if (HtmlDirectoryInfo.Exists)
			{
				_oldHtmlFiles = GetDirectoryFiles(htmlDirectoryName);
				HtmlDirectoryInfo.Delete(recursive: true);
			}
			HtmlDirectoryInfo.Create();
		}
	}

	public void AddError(string errorMsg, string filename)
	{
		_errorTimestamps.Add(DateTime.Now);
		_errorMesages.Add(errorMsg);
		_errorFiles.Add(filename);
		ErrorCount++;
	}

	public void Complete()
	{
		_transformedResource.DateCompleted = DateTime.Now;
		_transformedResource.Successfully = Successful;
		_transformedResource.Results = StatusMessage;
		TransformedResourceDataService transformedResourceFactory = new TransformedResourceDataService();
		transformedResourceFactory.Update(_transformedResource);
		TransformedResourceErrorDataService factory = new TransformedResourceErrorDataService();
		for (int i = 0; i < ErrorCount; i++)
		{
			Log.WarnFormat("FILE ERRORS! - file: {0}, error: {1}", _errorFiles[i], _errorMesages[i]);
			factory.Insert(GetTransformedResourceError(i));
		}
	}

	private TransformedResourceError GetTransformedResourceError(int index)
	{
		return new TransformedResourceError(Resource.Id, Resource.Isbn, _errorFiles[index], _errorMesages[index], _errorTimestamps[index]);
	}

	public string ToDebugString()
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendFormat("ISBN: {0}", Isbn);
		sb.AppendFormat(", Successful: {0}", Successful);
		sb.AppendFormat(", StatusMessage: {0}", StatusMessage);
		sb.AppendFormat(", TransferCount: {0}", TransferCount);
		sb.AppendFormat(", ErrorCount: {0}", ErrorCount);
		sb.AppendFormat(", ValidationFailureCount: {0}", ValidationFailureCount);
		if (_errorMesages.Count > 0)
		{
			for (int i = 0; i < _errorMesages.Count; i++)
			{
				sb.AppendLine().AppendFormat("\tError[{0}]: {1}, {2} - {3}", i, _errorFiles[i], _errorMesages[i], _errorTimestamps[i]);
			}
		}
		sb.Append("]");
		return sb.ToString();
	}

	private Dictionary<string, long> GetDirectoryFiles(string directory)
	{
		DirectoryInfo dirInfo = new DirectoryInfo(directory);
		FileInfo[] fileInfos = dirInfo.GetFiles();
		Dictionary<string, long> files = new Dictionary<string, long>();
		FileInfo[] array = fileInfos;
		foreach (FileInfo fileInfo in array)
		{
			files.Add(fileInfo.Name, fileInfo.Length);
		}
		return files;
	}

	public void ValidateNewHtmlFiles()
	{
		if (!_oldHtmlFiles.Any())
		{
			return;
		}
		FileInfo[] fileInfos = HtmlDirectoryInfo.GetFiles();
		FileInfo[] array = fileInfos;
		foreach (FileInfo fileInfo in array)
		{
			if (!_oldHtmlFiles.ContainsKey(fileInfo.Name))
			{
				ValidationFailureCount++;
				Log.WarnFormat("ValidationFailureCount: {0}, New HTML file '{1}' was not found in list of old HTML files.", ValidationFailureCount, fileInfo.Name);
				continue;
			}
			long oldHtmlFileLenghth = _oldHtmlFiles[fileInfo.Name];
			long diff = Math.Abs(oldHtmlFileLenghth - fileInfo.Length);
			if (diff > 250)
			{
				ValidationFailureCount++;
				Log.WarnFormat("ValidationFailureCount: {0}, New HTML file '{1}' length differs to much! (New: {2:#,###}, Old: {3:#,###}, Diff: {4:#,###})", ValidationFailureCount, fileInfo.Name, fileInfo.Length, oldHtmlFileLenghth, diff);
			}
			else if (oldHtmlFileLenghth > fileInfo.Length)
			{
				Log.DebugFormat("New HTML file '{0}' is smaller than old file! (New: {1:#,###}, Old: {2:#,###}, Diff: {3:#,###})", fileInfo.Name, fileInfo.Length, oldHtmlFileLenghth, fileInfo.Length - oldHtmlFileLenghth);
			}
			else if (oldHtmlFileLenghth < fileInfo.Length)
			{
				Log.DebugFormat("New HTML file '{0}' is larger than old file! (New: {1:#,###}, Old: {2:#,###}, Diff: {3:#,###})", fileInfo.Name, fileInfo.Length, oldHtmlFileLenghth, fileInfo.Length - oldHtmlFileLenghth);
			}
		}
	}
}
