using System.Collections.Generic;
using System.Linq;

namespace R2Utilities.DataAccess.Terms;

public static class SearchTerm
{
	public static HashSet<SearchTermItem> HashSet(HashSet<string> terms, bool isKeywords)
	{
		return new HashSet<SearchTermItem>(terms.Select((string t) => new SearchTermItem
		{
			SearchTerm = t,
			IsKeyword = isKeywords
		}));
	}
}
