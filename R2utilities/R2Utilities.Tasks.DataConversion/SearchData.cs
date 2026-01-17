using System;
using System.Collections.Generic;
using System.Text;

namespace R2Utilities.Tasks.DataConversion;

public class SearchData
{
	public string SearchType { get; set; }

	public string Resources { get; set; }

	public string SearchOnly { get; set; }

	public string Archive { get; set; }

	public string Results { get; set; }

	public string LimitCriteriaType { get; set; }

	public List<SearchCriteria> SearchCriteria { get; set; } = new List<SearchCriteria>();

	public List<LimitCriteria> LimitCriteria { get; set; } = new List<LimitCriteria>();

	public virtual int UserSearchHistoryId { get; set; }

	public virtual string SearchXml { get; set; }

	public virtual int UserId { get; set; }

	public virtual string CreatedBy { get; set; }

	public virtual DateTime DateCreated { get; set; }

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder("SearchData = [");
		sb.AppendFormat("SearchType: {0}", SearchType);
		sb.AppendFormat(", Resources: {0}", Resources);
		sb.AppendFormat(", SearchOnly: {0}", SearchOnly);
		sb.AppendFormat(", Archive: {0}", Archive);
		sb.AppendFormat(", Results: {0}", Results);
		sb.AppendFormat(", UserSearchHistoryId: {0}", UserSearchHistoryId);
		sb.AppendFormat(", UserId: {0}", UserId);
		sb.AppendFormat(", CreatedBy: {0}", CreatedBy);
		sb.AppendFormat(", DateCreated: {0}", DateCreated);
		sb.AppendLine();
		foreach (SearchCriteria searchCriterion in SearchCriteria)
		{
			sb.AppendFormat(" \tSearchCriteria = [Type: {0}, Phrase: {1}]", searchCriterion.Type, searchCriterion.Phrase).AppendLine();
		}
		foreach (LimitCriteria limitCriterion in LimitCriteria)
		{
			sb.AppendFormat(" \tLimitCriteria = [Discipline: {0}, Resource: {1}, Library: {2}, ReserverShelf: {3}]", limitCriterion.Discipline, limitCriterion.Resource, limitCriterion.Library, limitCriterion.ReserverShelf).AppendLine();
		}
		sb.Append("]");
		return sb.ToString();
	}
}
