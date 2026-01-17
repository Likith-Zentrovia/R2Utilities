using System;
using System.Linq;
using System.Net.Mail;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Email;

public class EmailDeliveryService
{
	private readonly IEmailSettings _emailSettings;

	private readonly ILog<EmailDeliveryService> _log;

	private int EmailsSentCount { get; set; }

	public int? DebugMaxEmailsSent { get; set; }

	protected string MainTemplate { get; set; }

	protected string BodyTemplate { get; set; }

	protected string ItemTemplate { get; set; }

	protected string SubItemTemplate { get; set; }

	protected string UnsubscribeTemplate { get; set; }

	public EmailMessage EmailMessage { get; set; }

	public EmailDeliveryService(ILog<EmailDeliveryService> log, IEmailSettings emailSettings)
	{
		_log = log;
		_emailSettings = emailSettings;
	}

	public bool SendCustomerTaskEmail(R2V2.Infrastructure.Email.EmailMessage emailMessage, string fromAddress, string fromAddressName)
	{
		try
		{
			if (!_emailSettings.SendToCustomers)
			{
				_log.DebugFormat("Overwriting to email from <{0}> to <{1}>", emailMessage.ToRecipientsToString(), _emailSettings.TestEmailAddresses);
				if (emailMessage.CcRecipients.Count > 0)
				{
					_log.DebugFormat("CC Addresses cleared, <{0}>", emailMessage.CcRecipientsToString());
					emailMessage.CcRecipients.Clear();
				}
				if (emailMessage.BccRecipients.Count > 0)
				{
					_log.DebugFormat("BCC Addresses cleared, <{0}>", emailMessage.BccRecipientsToString());
					emailMessage.BccRecipients.Clear();
				}
				emailMessage.ToRecipients.Clear();
				if (_emailSettings.TestEmailAddresses.Contains(';'))
				{
					emailMessage.AddToRecipients(_emailSettings.TestEmailAddresses, ';');
				}
				else
				{
					emailMessage.AddToRecipient(_emailSettings.TestEmailAddresses);
				}
				if (DebugMaxEmailsSent.HasValue && EmailsSentCount >= DebugMaxEmailsSent)
				{
					_log.DebugFormat("Max Debug Emails Reached, {0}, EMAIL NOT SENT!", EmailsSentCount);
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			return false;
		}
		return SendEmail(emailMessage, fromAddress, fromAddressName);
	}

	public bool SendTaskReportEmail(R2V2.Infrastructure.Email.EmailMessage emailMessage, string fromAddress, string fromAddressName)
	{
		return SendEmail(emailMessage, fromAddress, fromAddressName);
	}

	private bool SendEmail(R2V2.Infrastructure.Email.EmailMessage emailMessage, string fromAddress, string fromAddressName)
	{
		int attempCount = 0;
		while (attempCount < 3)
		{
			try
			{
				using (SmtpClient client = new SmtpClient())
				{
					MailMessage mailMessage = emailMessage.ToMailMessage(fromAddress, fromAddressName);
					client.Send(mailMessage);
				}
				EmailsSentCount++;
				return true;
			}
			catch (SmtpException ex)
			{
				attempCount++;
				if (attempCount > 2)
				{
					_log.Error(ex.Message, ex);
				}
				else
				{
					_log.Warn(ex.Message, ex);
				}
			}
			catch (Exception ex2)
			{
				_log.Error(ex2.Message, ex2);
				return false;
			}
		}
		return false;
	}
}
