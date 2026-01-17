using R2Utilities.DataAccess;
using R2V2.Core.Authentication;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Email.EmailBuilders;

public class TrialNotificationEmailBuildService : EmailBuildBaseService
{
	private string Title { get; set; }

	public TrialNotificationEmailBuildService(ILog<EmailBuildBaseService> log, IEmailSettings emailSettings, IContentSettings contentSettings)
		: base(log, emailSettings, contentSettings)
	{
	}

	public void InitEmailTemplates(TrialNotice trialNotice)
	{
		string bodyTemplate = "";
		switch (trialNotice)
		{
		case TrialNotice.First:
			bodyTemplate = "TrialNotice_9Day_Body.html";
			break;
		case TrialNotice.Second:
			bodyTemplate = "TrialNotice_3Day_Body.html";
			break;
		case TrialNotice.Final:
			bodyTemplate = "TrialNotice_Final_Body.html";
			break;
		case TrialNotice.Extension:
			bodyTemplate = "TrialNotice_Extension_Body.html";
			break;
		}
		SetTemplates(bodyTemplate, includeUnsubscribe: false);
		Title = trialNotice.ToTitle();
	}

	public R2V2.Infrastructure.Email.EmailMessage BuildTrialNotificationEmail(User user)
	{
		string messageBody = GetTrialNotificationEmailHtml(user);
		return BuildEmailMessage(user, "R2 Library " + Title, messageBody);
	}

	private string GetTrialNotificationEmailHtml(User user)
	{
		string bodyBuilder = BuildBodyHtml();
		return BuildMainHtml(Title, bodyBuilder, user);
	}
}
