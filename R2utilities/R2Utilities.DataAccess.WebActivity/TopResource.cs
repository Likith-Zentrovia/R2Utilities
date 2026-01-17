namespace R2Utilities.DataAccess.WebActivity;

public class TopResource : HitCount
{
	public virtual int ResourceId { get; set; }

	public virtual string Isbn { get; set; }

	public virtual string Title { get; set; }
}
