using System;

namespace R2Utilities.DataAccess.Terms;

public class SearchTermItem : IEquatable<SearchTermItem>
{
	public string SearchTerm { get; set; }

	public bool IsKeyword { get; set; }

	public bool Equals(SearchTermItem other)
	{
		return SearchTerm == other.SearchTerm && IsKeyword == other.IsKeyword;
	}

	public override int GetHashCode()
	{
		int hashSearchTerm = ((SearchTerm != null) ? SearchTerm.GetHashCode() : 0);
		int hashIsKeyword = IsKeyword.GetHashCode();
		return hashSearchTerm ^ hashIsKeyword;
	}
}
