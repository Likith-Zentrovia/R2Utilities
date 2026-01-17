namespace R2Utilities.DataAccess.WebActivity;

public class NumberOfRequestTimes
{
	public int Total { get; set; }

	public int TwoToFive { get; set; }

	public int FiveToTen { get; set; }

	public int MoreThanTen { get; set; }

	public decimal TwoToFivePercentage()
	{
		if (TwoToFive > 0 && Total > 0)
		{
			return decimal.Parse($"{TwoToFive}.0") / (decimal)Total;
		}
		return 0m;
	}

	public decimal FiveToTenPercentage()
	{
		if (TwoToFive > 0 && Total > 0)
		{
			return decimal.Parse($"{FiveToTen}.0") / (decimal)Total;
		}
		return 0m;
	}

	public decimal MoreThanTenPercentage()
	{
		if (TwoToFive > 0 && Total > 0)
		{
			return decimal.Parse($"{MoreThanTen}.0") / (decimal)Total;
		}
		return 0m;
	}
}
