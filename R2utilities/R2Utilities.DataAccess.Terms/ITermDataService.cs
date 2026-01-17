using System.Collections.Generic;

namespace R2Utilities.DataAccess.Terms;

public interface ITermDataService
{
	IEnumerable<TermToHighlight> SelectTermsToHighlight(HashSet<SearchTermItem> terms);
}
