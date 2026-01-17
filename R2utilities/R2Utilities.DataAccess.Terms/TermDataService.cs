using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.R2.DataServices;
using R2Utilities.Infrastructure.Settings;
using R2V2.Extensions;

namespace R2Utilities.DataAccess.Terms;

public abstract class TermDataService : DataServiceBase, ITermDataService
{
	private static readonly string TermToHighlightExec = new StringBuilder().Append("declare @searchTerms as dbo.SearchTermType ").Append("{0} ").Append("exec GetTermsToHighlight @searchTerms ")
		.ToString();

	private readonly ITermHighlightSettings _termHighlightSettings;

	protected TermDataService(ITermHighlightSettings termHighlightSettings, string connectionString)
		: base(connectionString)
	{
		_termHighlightSettings = termHighlightSettings;
	}

	public virtual IEnumerable<TermToHighlight> SelectTermsToHighlight(HashSet<SearchTermItem> terms)
	{
		int maxWordCount = _termHighlightSettings.MaxWordCountPerDataCall;
		IEnumerable<TermToHighlight> result = new List<TermToHighlight>();
		if (terms.Count == 0)
		{
			return result;
		}
		if (terms.Count > maxWordCount)
		{
			FactoryBase.Log.WarnFormat("Warning: Word list contains {0} words!", terms.Count);
			result = terms.InSetsOf(maxWordCount).SelectMany(GetTermsToHighlight);
		}
		else
		{
			result = GetTermsToHighlight(terms);
		}
		return result;
	}

	private IEnumerable<TermToHighlight> GetTermsToHighlight(IEnumerable<SearchTermItem> terms)
	{
		string inserts = terms.Select((SearchTermItem term) => string.Format("insert into @searchTerms (searchTerm, isKeyword) values ('{0}', {1})\n", term.SearchTerm.Replace("'", "''"), Convert.ToInt32(term.IsKeyword))).Aggregate(new StringBuilder(), (StringBuilder current, string insert) => current.Append(insert)).ToString();
		return GetEntityList<TermToHighlight>(string.Format(TermToHighlightExec, inserts), null, logSql: false);
	}
}
