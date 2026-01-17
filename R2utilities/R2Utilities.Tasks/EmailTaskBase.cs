using Microsoft.Practices.ServiceLocation;
using R2Utilities.Email;
using R2V2.Infrastructure.Email;

namespace R2Utilities.Tasks;

public abstract class EmailTaskBase : TaskBase
{
	protected readonly EmailDeliveryService EmailDeliveryService;

	protected EmailTaskBase(string taskName, string taskSwitch, string taskSwitchSmall, TaskGroup taskGroup, string taskDescription, bool enabled)
		: base(taskName, taskSwitch, taskSwitchSmall, taskGroup, taskDescription, enabled)
	{
		EmailDeliveryService = ServiceLocator.Current.GetInstance<EmailDeliveryService>();
	}

	public string PopulateField(string label, string value = "")
	{
		return string.IsNullOrWhiteSpace(value) ? ("<strong>" + label + "</strong>") : ("<strong>" + label + "</strong>" + value);
	}

	public string PopulateFieldOrNull(string label, string value)
	{
		return string.IsNullOrWhiteSpace(value) ? "" : ("<strong>" + label + "</strong>" + value);
	}

	protected void AddTaskCcToEmailMessage(R2V2.Infrastructure.Email.EmailMessage emailMessage)
	{
		foreach (string address in base.EmailSettings.TaskEmailConfig.CcAddresses)
		{
			if (!emailMessage.AddCcRecipient(address))
			{
				R2UtilitiesBase.Log.WarnFormat("invalid CC email address <{0}>", address);
			}
		}
	}

	protected void AddTaskBccToEmailMessage(R2V2.Infrastructure.Email.EmailMessage emailMessage)
	{
		foreach (string address in base.EmailSettings.TaskEmailConfig.BccAddresses)
		{
			if (!emailMessage.AddBccRecipient(address))
			{
				R2UtilitiesBase.Log.WarnFormat("invalid BCC email address <{0}>", address);
			}
		}
	}
}
