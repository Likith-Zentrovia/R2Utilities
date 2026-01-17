namespace R2Utilities.DataAccess.WebActivity;

public class TopInstitutionResource : TopInstitution
{
	public virtual int ResourceId { get; set; }

	public virtual string Isbn { get; set; }

	public virtual string Title { get; set; }
}
