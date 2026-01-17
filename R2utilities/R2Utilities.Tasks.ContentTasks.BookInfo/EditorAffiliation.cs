using System.Text;

namespace R2Utilities.Tasks.ContentTasks.BookInfo;

public class EditorAffiliation
{
	public string JobTitle { get; set; }

	public string OrganizationName { get; set; }

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder("EditorAffiliation = [").AppendFormat("JobTitle = {0}", JobTitle).AppendFormat(", OrganizationName = {0}", OrganizationName).Append("]");
		return sb.ToString();
	}
}
