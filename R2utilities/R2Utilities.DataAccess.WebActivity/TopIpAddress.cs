namespace R2Utilities.DataAccess.WebActivity;

public class TopIpAddress : TopInstitution
{
	public virtual int OctetA { get; set; }

	public virtual int OctetB { get; set; }

	public virtual int OctetC { get; set; }

	public virtual int OctetD { get; set; }

	public virtual string CountryCode { get; set; }

	public virtual long GetLongValue()
	{
		long ipNumberA = 16777216L * (long)OctetA;
		long ipNumberB = 65536L * (long)OctetB;
		long ipNumberC = 256L * (long)OctetC;
		return ipNumberA + ipNumberB + ipNumberC + OctetD;
	}
}
