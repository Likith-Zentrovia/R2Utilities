using System;
using System.Collections.Specialized;
using dtSearch.Engine;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class R2SearchResultItem : ISearchResultItem
{
	public int DocumnetId { get; set; }

	public DateTime CreatedDate { get; set; }

	public string DisplayName { get; set; }

	public string Filename { get; set; }

	public int HitCount { get; set; }

	public string[] HitDetails { get; set; }

	public int[] Hits { get; set; }

	public string[] HitsByWord { get; set; }

	public int IndexedBy { get; set; }

	public string IndexRetrievedFrom { get; set; }

	public string Location { get; set; }

	public DateTime ModifiedDate { get; set; }

	public int PhraseCount { get; set; }

	public int Score { get; set; }

	public int ScorePercent { get; set; }

	public string ShortName { get; set; }

	public int Size { get; set; }

	public string Synopsis { get; set; }

	public string Title { get; set; }

	public TypeId TypeId { get; set; }

	public StringDictionary UserFields { get; set; }

	public bool VetoThisItem { get; set; }

	public int WhichIndex { get; set; }

	public int WordCount { get; set; }

	public R2SearchResultItem(SearchResultsItem item)
	{
		DocumnetId = item.DocId;
		CreatedDate = item.CreatedDate;
		DisplayName = item.DisplayName;
		Filename = item.Filename;
		HitCount = item.HitCount;
		HitDetails = item.HitDetails;
		Hits = item.Hits;
		HitsByWord = item.HitsByWord;
		IndexedBy = item.IndexedBy;
		IndexRetrievedFrom = item.IndexRetrievedFrom;
		Location = item.Location;
		ModifiedDate = item.ModifiedDate;
		PhraseCount = item.PhraseCount;
		Score = item.Score;
		ScorePercent = item.ScorePercent;
		ShortName = item.ShortName;
		Size = item.Size;
		Synopsis = item.Synopsis;
		Title = item.Title;
		TypeId = item.TypeId;
		UserFields = item.UserFields;
		VetoThisItem = item.VetoThisItem;
		WhichIndex = item.WhichIndex;
		WordCount = item.WordCount;
	}
}
