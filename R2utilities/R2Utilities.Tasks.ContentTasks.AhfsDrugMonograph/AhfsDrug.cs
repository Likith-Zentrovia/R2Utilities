using System.Collections.Generic;
using System.Text;
using R2V2.Core;

namespace R2Utilities.Tasks.ContentTasks.AhfsDrugMonograph;

public class AhfsDrug : IDebugInfo
{
	private readonly List<string> _chemicalNames = new List<string>();

	private readonly List<string> _genericNames = new List<string>();

	private readonly List<string> _printNames = new List<string>();

	private readonly List<string> _synonyms = new List<string>();

	public string UnitNumber { get; set; }

	public string FullTitle { get; set; }

	public string ShortTitle { get; set; }

	public string Introduction { get; set; }

	public string ClassNumber { get; set; }

	public string ClassText { get; set; }

	public string XmlFileName { get; set; }

	public IEnumerable<string> Synonyms => _synonyms;

	public IEnumerable<string> PrintNames => _printNames;

	public IEnumerable<string> GenericNames => _genericNames;

	public IEnumerable<string> ChecmicalNames => _chemicalNames;

	public string ToDebugString()
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendFormat("AhfsDrug = [UnitNumber: {0}, FullTitle: {1}, ShortTitle: {2}", UnitNumber, FullTitle, ShortTitle);
		sb.AppendFormat(", ClassNumber: {0}", ClassNumber);
		sb.AppendFormat(", ClassText: {0}", ClassText).AppendLine();
		sb.AppendFormat("\t, Introduction: {0}", Introduction).AppendLine();
		sb.AppendFormat("\t, PrintNames: [{0}]", string.Join(", ", _printNames.ToArray())).AppendLine();
		sb.AppendFormat("\t, GenericNames: [{0}]", string.Join(", ", _genericNames.ToArray())).AppendLine();
		sb.AppendFormat("\t, ChecmicalName: [{0}]", string.Join(", ", _chemicalNames.ToArray())).AppendLine();
		sb.AppendFormat("\t, Synonyms: [{0}]", string.Join(", ", _synonyms.ToArray())).AppendLine();
		sb.Append("]");
		return sb.ToString();
	}

	public void AddSynonym(string synonym)
	{
		_synonyms.Add(synonym);
	}

	public void AddPrintName(string name)
	{
		_printNames.Add(name);
	}

	public void AddGenericName(string name)
	{
		_genericNames.Add(name);
	}

	public void AddChecmicalName(string name)
	{
		_chemicalNames.Add(name);
	}
}
