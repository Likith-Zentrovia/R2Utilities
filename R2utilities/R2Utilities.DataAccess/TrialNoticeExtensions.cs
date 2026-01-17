namespace R2Utilities.DataAccess;

public static class TrialNoticeExtensions
{
	public static string ToTitle(this TrialNotice trialNotice)
	{
		return trialNotice switch
		{
			TrialNotice.First => "9 day Trial Notification", 
			TrialNotice.Second => "3 day Trial Notification", 
			TrialNotice.Final => "Final Trial Notification", 
			TrialNotice.Extension => "Extension Trial Notification", 
			_ => null, 
		};
	}
}
