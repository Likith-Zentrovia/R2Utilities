namespace R2Utilities.DataAccess.WebActivity;

public class TopInstitution : HitCount
{
	public virtual int InstitutionId { get; set; }

	public virtual string AccountNumber { get; set; }

	public virtual string AccountName { get; set; }
}
