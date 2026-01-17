using System.Collections.Generic;
using dtSearch.Engine;

namespace R2Utilities.Tasks.ContentTasks.Services;

internal class StatusHandler : IIndexStatusHandler
{
	private Dictionary<string, int> DocumentIds { get; }

	public StatusHandler(Dictionary<string, int> documentIds)
	{
		DocumentIds = documentIds;
	}

	void IIndexStatusHandler.OnProgressUpdate(IndexProgressInfo info)
	{
		if (info.UpdateType == MessageCode.dtsnIndexFileDone)
		{
			DocumentIds.Add(info.File.DisplayName, info.File.DocId);
		}
	}

	AbortValue IIndexStatusHandler.CheckForAbort()
	{
		return AbortValue.Continue;
	}
}
