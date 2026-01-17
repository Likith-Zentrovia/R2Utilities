using System.Text;

namespace R2Utilities.Tasks.ContentTasks.BookInfo;

public class Author
{
	private string _degrees;

	private string _firstName;

	private string _lastName;

	private string _lineage;

	private string _middleInitial;

	public string FirstName
	{
		get
		{
			return _firstName;
		}
		set
		{
			_firstName = (string.IsNullOrEmpty(value) ? string.Empty : value.Trim());
		}
	}

	public string LastName
	{
		get
		{
			return _lastName;
		}
		set
		{
			_lastName = (string.IsNullOrEmpty(value) ? string.Empty : value.Trim());
		}
	}

	public string MiddleInitial
	{
		get
		{
			return _middleInitial;
		}
		set
		{
			_middleInitial = (string.IsNullOrEmpty(value) ? string.Empty : value.Trim());
		}
	}

	public string Degrees
	{
		get
		{
			return _degrees;
		}
		set
		{
			_degrees = (string.IsNullOrEmpty(value) ? string.Empty : value.Trim());
		}
	}

	public string Lineage
	{
		get
		{
			return _lineage;
		}
		set
		{
			_lineage = (string.IsNullOrEmpty(value) ? string.Empty : value.Trim());
		}
	}

	public string GetFullName()
	{
		return GetFullName(lastNameFirst: false);
	}

	public string GetFullName(bool lastNameFirst)
	{
		string fullname = ((!lastNameFirst) ? (FirstName + " " + MiddleInitial + " " + LastName + " " + Lineage + " " + Degrees) : (LastName + " " + FirstName + " " + MiddleInitial + " " + Lineage + " " + Degrees));
		return fullname.Trim().Replace("  ", " ").Replace(" ,", ",");
	}

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder("Author = [").AppendFormat("LastName = {0}", LastName).AppendFormat(", FirstName = {0}", FirstName).AppendFormat(", MiddleInitial = {0}", MiddleInitial)
			.AppendFormat(", Lineage = {0}", Lineage)
			.AppendFormat(", Degrees = {0}", Degrees)
			.Append("]");
		return sb.ToString();
	}
}
