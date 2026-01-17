using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace R2Utilities.Tasks.ContentTasks;

public class DocIds
{
	public string Isbn { get; set; }

	public int MinimumDocId { get; set; }

	public int MaximumDocId { get; set; }

	public List<DocIdFilename> Filenames { get; set; } = new List<DocIdFilename>();

	public IEnumerable<int> GetRange()
	{
		return Enumerable.Range(MinimumDocId, MaximumDocId - MinimumDocId + 1);
	}

	public DocIds GetInvalidDocsInIndex(string rootHtmlPath)
	{
		DocIds docIds = new DocIds
		{
			Isbn = Isbn
		};
		foreach (DocIdFilename filename in Filenames)
		{
			if (filename.IsInvalidPath || !DoesHtmlFileExist(Isbn, filename.Name, rootHtmlPath))
			{
				docIds.Filenames.Add(filename);
			}
		}
		if (docIds.Filenames.Any())
		{
			MinimumDocId = docIds.Filenames.First().Id;
			MaximumDocId = docIds.Filenames.Last().Id;
		}
		return docIds;
	}

	private bool DoesHtmlFileExist(string isbn, string filename, string rootHtmlPath)
	{
		string htmlFilePath = Path.Combine(rootHtmlPath, "html", isbn, filename);
		return File.Exists(htmlFilePath);
	}
}
