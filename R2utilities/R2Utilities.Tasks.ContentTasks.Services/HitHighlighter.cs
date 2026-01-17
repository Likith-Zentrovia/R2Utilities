using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using dtSearch.Engine;
using R2Utilities.DataAccess.Terms;
using R2Utilities.Infrastructure.Settings;
using R2V2.Extensions;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class HitHighlighter : R2UtilitiesBase
{
	private const SearchFlags HighlightingSearchFlags = SearchFlags.dtsSearchTypeAnyWords;

	private const int TermResourceInsertSetSize = 262;

	private string _indexLocation;

	private ContentToHighlight _content;

	private int _currentDocId;

	private readonly Dictionary<string, int> _documentIds;

	private List<TermToHighlight> _termsToHighlight;

	private TermToHighlight _termToHighlight;

	private readonly HashSet<TermResource> _termResources;

	private string _taskName;

	private bool _foundTerm;

	private readonly IContentSettings _contentSettings;

	private TermHighlightType _termHighlightType;

	private readonly TermResourceDataService _termResourceDataService;

	private ITermDataService _termDataService;

	public ResourceToHighlight ResourceToHighlight { get; private set; }

	public HitHighlighter(IContentSettings contentSettings, TermResourceDataService termResourceDataService)
	{
		_contentSettings = contentSettings;
		_termResourceDataService = termResourceDataService;
		_documentIds = new Dictionary<string, int>();
		_termsToHighlight = new List<TermToHighlight>();
		_termResources = new HashSet<TermResource>();
	}

	public void Init(ITermHighlightSettings termHighlightSettings, ITermDataService termDataService, string taskName)
	{
		_termHighlightType = termHighlightSettings.TermHighlightType;
		_indexLocation = termHighlightSettings.IndexLocation;
		_termDataService = termDataService;
		_taskName = taskName;
	}

	public void HighlightResource(ResourceToHighlight resource)
	{
		ClearCollections();
		R2UtilitiesBase.Log.InfoFormat("\nResource: {0}{1}", resource.TermHighlightQueue.Isbn, " --------------------");
		ResourceToHighlight = resource;
		R2UtilitiesBase.Log.Info("Copying files to backup");
		ResourceToHighlight.WriteResourceBackup();
		R2UtilitiesBase.Log.Info("Zipping backup");
		ResourceToHighlight.ZipResourceBackup();
		R2UtilitiesBase.Log.Info("Loading content to memory");
		ResourceToHighlight.LoadContent(removeComments: true);
		SetOptions();
		R2UtilitiesBase.Log.Info("Building index");
		BuildIndex();
		ResourceToHighlight.Words = GetWordsFromResource();
		GetTermsToHighlight();
		int fileCount = 0;
		R2UtilitiesBase.Log.Info("Begin Highlighting-------");
		R2UtilitiesBase.Log.InfoFormat("{0} file to process", ResourceToHighlight.Content.Count);
		foreach (ContentToHighlight content in ResourceToHighlight.Content)
		{
			fileCount++;
			_content = content;
			R2UtilitiesBase.Log.InfoFormat("File: {0}, {1} of {2}", _content.FileName, fileCount, ResourceToHighlight.Content.Count);
			if (!_content.IsIgnored)
			{
				HighlightFile();
			}
			_content.WriteOutput();
			ResourceToHighlight.HighlightedFileCount++;
		}
		InsertTermResources();
		InsertAtoZIndex();
	}

	private void HighlightFile()
	{
		IEnumerable<TermToHighlight> termsToHighlight = (from t in _termsToHighlight.Where(IsCandidateTerm)
			group t by t.Text into t
			select t.First()).ToList();
		foreach (TermToHighlight item in termsToHighlight)
		{
			HighlightText((_termToHighlight = item).Text);
		}
	}

	private bool IsCandidateTerm(TermToHighlight term)
	{
		TermType termType = term.TermType;
		TermType termType2 = termType;
		if (termType2 == TermType.Keyword)
		{
			return _content.Keywords.Contains(term.Text);
		}
		return _content.Words.Contains(term.Word) && !_content.Keywords.Contains(term.Text);
	}

	private void HighlightText(string term)
	{
		_foundTerm = false;
		string pattern = "(?<=\\>[^<]*)(\\b" + Regex.Escape(term) + "\\b)";
		_content.OutputContent = Regex.Replace(_content.OutputContent, pattern, MatchEvaluator, RegexOptions.IgnoreCase);
		if (_foundTerm)
		{
			AddTermResource();
		}
	}

	private string MatchEvaluator(Match match)
	{
		_foundTerm = true;
		return _termToHighlight.Highlight(match.Groups[0].ToString());
	}

	private void AddTermResource()
	{
		if (_termHighlightType == TermHighlightType.IndexTerms)
		{
			TermResource termResource = _termToHighlight.ToTermResource();
			termResource.ChapterId = _content.ChapterId;
			termResource.SectionId = _content.SectionId;
			termResource.CreatorId = _taskName;
			termResource.TermId = _termToHighlight.TermId;
			termResource.ResourceIsbn = ResourceToHighlight.ResourceCore.Isbn;
			termResource.Title = ResourceToHighlight.ResourceCore.Title;
			_termResources.Add(termResource);
		}
	}

	private HashSet<string> GetWordsFromResource()
	{
		HashSet<string> resourceWords = new HashSet<string>();
		int fileCount = 0;
		foreach (ContentToHighlight content in ResourceToHighlight.Content)
		{
			fileCount++;
			using WordListBuilder wlb = new WordListBuilder();
			using SearchFilter filter = new SearchFilter();
			int indexId = filter.AddIndex(_indexLocation);
			if (content.IsIgnored)
			{
				R2UtilitiesBase.Log.InfoFormat("Ignoring file: {0}, {1} of {2}", content.FileName, fileCount, ResourceToHighlight.Content.Count);
				continue;
			}
			R2UtilitiesBase.Log.InfoFormat("Scraping words from file: {0}, {1} of {2}", content.FileName, fileCount, ResourceToHighlight.Content.Count);
			_currentDocId = _documentIds[content.FileName];
			filter.SelectItems(indexId, _currentDocId, _currentDocId, isSelected: true);
			wlb.SetFilter(filter);
			wlb.OpenIndex(_indexLocation);
			wlb.ListMatchingWords(".*", int.MaxValue, SearchFlags.dtsSearchTypeAnyWords, 0);
			R2UtilitiesBase.Log.DebugFormat("word count: {0}", wlb.Count);
			if (wlb.Count == 0)
			{
				R2UtilitiesBase.Log.Warn("No words found in resource file!");
			}
			HashSet<string> words = new HashSet<string>();
			for (int n = 0; n < wlb.Count; n++)
			{
				string word = wlb.GetNthWord(n);
				words.Add(word);
			}
			content.Words = words;
			resourceWords.UnionWith(words);
		}
		return resourceWords;
	}

	private void GetTermsToHighlight()
	{
		R2UtilitiesBase.Log.Info("Making data call for all terms in resource");
		HashSet<SearchTermItem> searchTerms = BuildSearchTerms();
		IEnumerable<TermToHighlight> termsToHighlight = _termDataService.SelectTermsToHighlight(searchTerms);
		_termsToHighlight = (from t in termsToHighlight
			orderby t.Rank descending
			group t by new { t.Word, t.Text, t.TermType } into t
			select t.First() into t
			where !t.IsCompound || Regex.Split(t.Text, "\\W+").All(ResourceToHighlight.Words.Contains)
			orderby t.Text.Length descending
			select t).ToList();
	}

	private HashSet<SearchTermItem> BuildSearchTerms()
	{
		HashSet<SearchTermItem> searchTerms = SearchTerm.HashSet(ResourceToHighlight.Words, isKeywords: false);
		searchTerms.UnionWith(SearchTerm.HashSet(ResourceToHighlight.Keywords, isKeywords: true));
		return searchTerms;
	}

	private void InsertTermResources()
	{
		if (_termHighlightType == TermHighlightType.IndexTerms)
		{
			R2UtilitiesBase.Log.Info("Update Term Resource Tables-------");
			_termResourceDataService.InactivateTermResources(ResourceToHighlight.TermHighlightQueue.Isbn);
			int count = 0;
			_termResources.InSetsOf(262).ForEach(delegate(List<TermResource> set)
			{
				count += _termResourceDataService.InsertTermResources(set);
			});
			R2UtilitiesBase.Log.InfoFormat("Total records inserted: {0}\n", count);
		}
	}

	private void InsertAtoZIndex()
	{
		if (_termHighlightType == TermHighlightType.IndexTerms)
		{
			R2UtilitiesBase.Log.Info("Insert A to Z Index-------");
			_termResourceDataService.DeleteAtoZIndex(ResourceToHighlight.TermHighlightQueue.Isbn);
			int count = _termResourceDataService.InsertAtoZIndex(ResourceToHighlight.ResourceCore.Id);
			R2UtilitiesBase.Log.InfoFormat("Total records inserted: {0}", count);
		}
	}

	private void BuildIndex()
	{
		using IndexJob ij = new IndexJob();
		ij.IndexPath = _indexLocation;
		ij.FoldersToIndex.Add(ResourceToHighlight.ResourceLocation);
		ij.ActionAdd = true;
		ij.ActionCreate = true;
		ij.StatusHandler = new StatusHandler(_documentIds);
		if (!ij.Execute())
		{
			ThrowError("Index Job", ij.Errors.Message(0));
		}
	}

	private void SetOptions()
	{
		using Options options = new Options();
		options.HomeDir = _contentSettings.DtSearchBinLocation;
		options.FieldFlags = FieldFlags.dtsoFfXmlHideFieldNames | FieldFlags.dtsoFfXmlSkipAttributes;
		options.Hyphens = HyphenSettings.dtsoHyphenAsHyphen;
		options.BooleanConnectors = "";
		options.Save();
	}

	private void ClearCollections()
	{
		_documentIds.Clear();
		_termsToHighlight.Clear();
		_termResources.Clear();
	}

	private void ThrowError(string failedAction, string errorDetail)
	{
		string message = "Hit Highlighting Failed During " + failedAction + " for File: " + _content.FileName + "\nError Detail:" + errorDetail;
		throw new Exception(message);
	}
}
