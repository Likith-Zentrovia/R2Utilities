using System.Collections.Generic;
using System.Text;

namespace R2Utilities.Tasks.ContentTasks.BookInfo;

public class Editor
{
	public string FirstName { get; set; }

	public string LastName { get; set; }

	public string MiddleInitial { get; set; }

	public string Degrees { get; set; }

	public string Lineage { get; set; }

	public List<EditorAffiliation> Affiliations { get; set; } = new List<EditorAffiliation>();

	public string GetXmlElementValue()
	{
		string fullname = FirstName + " " + MiddleInitial + " " + LastName + " " + Lineage + " " + Degrees;
		StringBuilder xml = new StringBuilder(fullname.Trim().Replace("  ", " "));
		foreach (EditorAffiliation affiliation in Affiliations)
		{
			xml.AppendFormat("{0} {1}, {2}", (xml.Length > 0) ? " - " : string.Empty, affiliation.JobTitle, affiliation.OrganizationName);
		}
		return xml.ToString().Trim().Replace("  ", " ")
			.Replace(" ,", ",");
	}

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder("Editor = [").AppendFormat("LastName = {0}", LastName).AppendFormat(", FirstName = {0}", FirstName).AppendFormat(", MiddleInitial = {0}", MiddleInitial)
			.AppendFormat(", Lineage = {0}", Lineage)
			.AppendFormat(", Degrees = {0}", Degrees);
		foreach (EditorAffiliation editorAffiliation in Affiliations)
		{
			sb.AppendLine().Append("\t\t, Affiliation: ").Append(editorAffiliation);
		}
		sb.Append("]");
		return sb.ToString();
	}
}
