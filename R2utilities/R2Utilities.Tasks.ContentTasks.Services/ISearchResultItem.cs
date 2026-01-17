using System;
using System.Collections.Specialized;
using dtSearch.Engine;

namespace R2Utilities.Tasks.ContentTasks.Services;

public interface ISearchResultItem
{
	int DocumnetId { get; set; }

	DateTime CreatedDate { get; set; }

	string DisplayName { get; set; }

	string Filename { get; set; }

	int HitCount { get; set; }

	string[] HitDetails { get; set; }

	int[] Hits { get; set; }

	string[] HitsByWord { get; set; }

	int IndexedBy { get; set; }

	string IndexRetrievedFrom { get; set; }

	string Location { get; set; }

	DateTime ModifiedDate { get; set; }

	int PhraseCount { get; set; }

	int Score { get; set; }

	int ScorePercent { get; set; }

	string ShortName { get; set; }

	int Size { get; set; }

	string Synopsis { get; set; }

	string Title { get; set; }

	TypeId TypeId { get; set; }

	StringDictionary UserFields { get; set; }

	bool VetoThisItem { get; set; }

	int WhichIndex { get; set; }

	int WordCount { get; set; }
}
