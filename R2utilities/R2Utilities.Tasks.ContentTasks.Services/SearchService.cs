using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using dtSearch.Engine;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class SearchService : R2UtilitiesBase
{
	private readonly IContentSettings _contentSettings;

	public SearchService(IContentSettings contentSettings)
	{
		_contentSettings = contentSettings;
	}

	public IList<ISearchResultItem> PerformSearchByIsbn(string isbn)
	{
		List<ISearchResultItem> results = new List<ISearchResultItem>();
		StringBuilder logInfo = new StringBuilder();
		Options opts = new Options
		{
			HomeDir = "C:\\Program Files\\dtSearch Developer\\bin"
		};
		opts.Save();
		using (SearchJob searchJob = new SearchJob())
		{
			Stopwatch searchTimer = new Stopwatch();
			searchTimer.Start();
			searchJob.Request = new StringBuilder().AppendFormat("(r2isbn contains ( {0} )) or (Filename contains ( {0} ))", isbn).ToString();
			logInfo.AppendFormat("Request: '{0}'", searchJob.Request);
			searchJob.AutoStopLimit = 25000;
			searchJob.TimeoutSeconds = 30;
			searchJob.SearchFlags = SearchFlags.dtsSearchDelayDocInfo | SearchFlags.dtsSearchAutoTermWeight | SearchFlags.dtsSearchPositionalScoring;
			string indexPath = _contentSettings.DtSearchIndexLocation;
			searchJob.IndexesToSearch.Add(indexPath);
			searchJob.Execute();
			searchTimer.Stop();
			if (searchJob.Errors.Count > 0)
			{
				string fullError = searchJob.Errors.Message(0);
				fullError = fullError.Substring(fullError.IndexOf(" ", StringComparison.Ordinal) + 1);
				R2UtilitiesBase.Log.InfoFormat("The search returned an error: {0}", fullError);
			}
			logInfo.AppendFormat(", searchJob.TaskResults.Count: {0} - {1:0.000 ms}", searchJob.Results.Count, searchTimer.ElapsedMilliseconds);
			Stopwatch resultsTimer = new Stopwatch();
			resultsTimer.Start();
			for (int i = 0; i < searchJob.Results.Count; i++)
			{
				searchJob.Results.GetNthDoc(i);
				SearchResultsItem item = searchJob.Results.CurrentItem;
				R2SearchResultItem r2Item = new R2SearchResultItem(item);
				results.Add(r2Item);
			}
			resultsTimer.Stop();
			logInfo.AppendFormat(", results.Count: {0} - {1:0.000 ms}", results.Count, resultsTimer.ElapsedMilliseconds);
			R2UtilitiesBase.Log.Debug(logInfo.ToString());
		}
		return results;
	}
}
