using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Common.Logging;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.Email;

public class EmailMessage
{
	protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public string MessageBody { get; set; }

	public string Subject { get; set; }

	public bool IsBodyHtml { get; set; }

	public string[] ToRecipients { get; set; }

	public string[] CcRecipients { get; set; }

	public string[] BccRecipients { get; set; }

	public string FromAddress { get; set; }

	public string FromDisplayName { get; set; }

	public string ReplyToAddress { get; set; }

	public string ReplyToDisplayName { get; set; }

	[XmlIgnore]
	public Attachment ExcelAttachment { get; set; }

	public DateTime QueueDate { get; set; }

	public int SendAttempts { get; set; }

	public EmailMessage(IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	private MailMessage GetMailMessage()
	{
		MailMessage message = new MailMessage();
		if (ToRecipients != null)
		{
			string[] toRecipients = ToRecipients;
			foreach (string address in toRecipients)
			{
				if (!string.IsNullOrEmpty(address))
				{
					string cleanedAddress = address.Replace(",", ".").Trim().Replace(" ", "");
					Log.DebugFormat("Adding TO address: <{0}>", cleanedAddress);
					message.To.Add(cleanedAddress);
				}
			}
		}
		if (CcRecipients != null)
		{
			string[] ccRecipients = CcRecipients;
			foreach (string address2 in ccRecipients)
			{
				if (!string.IsNullOrEmpty(address2))
				{
					Log.DebugFormat("Adding CC address: <{0}>", address2);
					message.CC.Add(address2);
				}
			}
		}
		if (BccRecipients != null)
		{
			string[] bccRecipients = BccRecipients;
			foreach (string address3 in bccRecipients)
			{
				if (!string.IsNullOrEmpty(address3))
				{
					Log.DebugFormat("Adding BCC address: <{0}>", address3);
					message.Bcc.Add(address3);
				}
			}
		}
		if (ExcelAttachment != null)
		{
			message.Attachments.Add(ExcelAttachment);
		}
		if (!string.IsNullOrEmpty(FromAddress))
		{
			Log.DebugFormat("Adding FROM address: <{0}>, '{1}'", FromAddress, FromDisplayName);
			message.From = (string.IsNullOrEmpty(FromDisplayName) ? new MailAddress(FromAddress) : new MailAddress(FromAddress, FromDisplayName));
		}
		else
		{
			message.From = new MailAddress(_r2UtilitiesSettings.DefaultFromAddress, _r2UtilitiesSettings.DefaultFromAddressName);
		}
		if (!string.IsNullOrEmpty(ReplyToAddress))
		{
			Log.DebugFormat("Adding REPLY-TO address: <{0}>, '{1}'", ReplyToAddress, ReplyToDisplayName);
			message.ReplyToList.Add(string.IsNullOrEmpty(ReplyToDisplayName) ? new MailAddress(ReplyToAddress) : new MailAddress(ReplyToAddress, ReplyToDisplayName));
		}
		message.Subject = Subject;
		message.Body = MessageBody;
		message.IsBodyHtml = IsBodyHtml;
		return message;
	}

	public bool Send()
	{
		try
		{
			Log.Debug(ToString());
			MailMessage message = GetMailMessage();
			List<MailAddress> badMailAddress = new List<MailAddress>();
			foreach (MailAddress email in message.To.Where((MailAddress mailAddress2) => !IsValidEmail(mailAddress2.Address)))
			{
				Log.ErrorFormat("Bad Email Address: {0}", email.Address);
				badMailAddress.Add(email);
			}
			if (badMailAddress.Count > 0)
			{
				foreach (MailAddress mailAddress in badMailAddress)
				{
					message.To.Remove(mailAddress);
					Log.DebugFormat("Removing Bad Email : {0}", mailAddress.Address);
				}
				if (message.To.Count == 0)
				{
					return false;
				}
			}
			using SmtpClient client = new SmtpClient();
			SendAttempts++;
			Log.DebugFormat("Host: {0}, SendAttempts: {1}, client.Timeout: {2}, client.Port: {3}, client.EnableSsl: {4}, client.DeliveryMethod: {5}", client.Host, SendAttempts, client.Timeout, client.Port, client.EnableSsl, client.DeliveryMethod);
			client.Send(message);
			Log.InfoFormat("Message successfully sent, subject: {0}", message.Subject);
			return true;
		}
		catch (Exception ex)
		{
			Log.Error(ex.Message, ex);
			return false;
		}
	}

	public bool IsValidEmail(string email)
	{
		Regex regex = new Regex("^(([^<>()[\\]\\\\.,;:\\s@\\\"]+(\\.[^<>()[\\]\\\\.,;:\\s@\\\"]+)*)|(\\\".+\\\"))@((\\[[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\])|(([a-zA-Z\\-0-9]+\\.)+[a-zA-Z]{2,}))$");
		return !string.IsNullOrWhiteSpace(email) && regex.IsMatch(email);
	}

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder("EmailMessageDate = [").AppendFormat("Subject: {0}", Subject).AppendLine().AppendFormat(", IsBodyHtml: {0}", IsBodyHtml)
			.AppendFormat(", QueueDate: {0}", QueueDate)
			.AppendFormat(", SendAttempts: {0}", SendAttempts)
			.AppendLine()
			.AppendFormat(", FromAddress: <{0}>", FromAddress)
			.AppendFormat(", FromDisplayName: {0}", FromDisplayName)
			.AppendFormat(", ReplyToAddress: <{0}>", ReplyToAddress)
			.AppendFormat(", ReplyToDisplayName: {0}", ReplyToDisplayName)
			.AppendFormat(", ExcelAttachment: {0}", ExcelAttachment != null)
			.AppendLine();
		if (ToRecipients == null)
		{
			sb.Append(", ToRecipients[]: null").AppendLine();
		}
		else
		{
			sb.AppendFormat(", ToRecipients[{0}]: <{1}>", ToRecipients.Length, string.Join(",", ToRecipients)).AppendLine();
		}
		if (CcRecipients == null)
		{
			sb.Append(", CcRecipients[]: null").AppendLine();
		}
		else
		{
			sb.AppendFormat(", CcRecipients[{0}]: <{1}>", CcRecipients.Length, string.Join(",", CcRecipients)).AppendLine();
		}
		if (BccRecipients == null)
		{
			sb.Append(", BccRecipients[]: null").AppendLine();
		}
		else
		{
			sb.AppendFormat(", BccRecipients[{0}]: <{1}>", BccRecipients.Length, string.Join(",", BccRecipients)).AppendLine();
		}
		if (!string.IsNullOrEmpty(MessageBody) && MessageBody.Length > 500)
		{
			sb.AppendFormat(", MessageBody: {0} ... TRUNCATED!", MessageBody.Substring(0, 500)).AppendLine();
		}
		else
		{
			sb.AppendFormat(", MessageBody: {0}", MessageBody).AppendLine();
		}
		sb.Append("]");
		return sb.ToString();
	}

	public string ToRecipientsToString()
	{
		return GetRecipientsAsString(ToRecipients);
	}

	public string CcRecipientsToString()
	{
		return GetRecipientsAsString(CcRecipients);
	}

	public string BccRecipientsToString()
	{
		return GetRecipientsAsString(BccRecipients);
	}

	private string GetRecipientsAsString(string[] recipients)
	{
		if (recipients == null || recipients.Length == 0)
		{
			return "null";
		}
		return string.Join(";", recipients);
	}
}
