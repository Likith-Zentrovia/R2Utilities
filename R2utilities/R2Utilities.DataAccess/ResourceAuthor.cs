namespace R2Utilities.DataAccess;

public class ResourceAuthor
{
	public virtual int ResourceId { get; set; }

	public virtual short Order { get; set; }

	public virtual string FirstName { get; set; }

	public virtual string LastName { get; set; }

	public virtual string MiddleName { get; set; }

	public virtual string Lineage { get; set; }

	public virtual string Degrees { get; set; }

	public virtual string GetFullName()
	{
		string fullname = FirstName + " " + MiddleName + " " + LastName + " " + Lineage + " " + Degrees;
		return fullname.Replace("  ", " ");
	}
}
