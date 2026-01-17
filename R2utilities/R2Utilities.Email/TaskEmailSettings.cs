using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Common.Logging;

namespace R2Utilities.Email;

public class TaskEmailSettings
{
	protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);

	private readonly string _emailConfigDirectory;

	public string TaskKey { get; set; }

	public EmailConfiguration SuccessEmailConfig { get; }

	public EmailConfiguration ErrorEmailConfig { get; }

	public EmailConfiguration TaskEmailConfig { get; }

	public TaskEmailSettings(string taskKey, string emailConfigDirectory)
	{
		_emailConfigDirectory = emailConfigDirectory;
		TaskKey = taskKey;
		if (!Directory.Exists(_emailConfigDirectory))
		{
			Log.ErrorFormat("Email Config directory NOT found, _emailConfigDirectory: {0}", _emailConfigDirectory);
		}
		SuccessEmailConfig = new EmailConfiguration
		{
			Type = "Success"
		};
		ErrorEmailConfig = new EmailConfiguration
		{
			Type = "Error"
		};
		TaskEmailConfig = new EmailConfiguration
		{
			Type = "Task"
		};
		PopulateEmailConfigurations("Default", SuccessEmailConfig);
		PopulateEmailConfigurations("Default", ErrorEmailConfig);
		PopulateEmailConfigurations("Default", TaskEmailConfig);
		PopulateEmailConfigurations(taskKey, SuccessEmailConfig);
		PopulateEmailConfigurations(taskKey, ErrorEmailConfig);
		PopulateEmailConfigurations(taskKey, TaskEmailConfig);
	}

	private void PopulateEmailConfigurations(string taskKey, EmailConfiguration emailConfiguration)
	{
		Log.DebugFormat("EmailConfigDirectory: {0}", _emailConfigDirectory);
		string xmlFilename = _emailConfigDirectory + "\\" + taskKey + ".xml";
		Log.DebugFormat("xmlFilename: {0}", xmlFilename);
		if (!File.Exists(xmlFilename))
		{
			return;
		}
		XmlDocument xmlDoc = new XmlDocument();
		xmlDoc.Load(xmlFilename);
		if (xmlDoc.DocumentElement == null)
		{
			return;
		}
		XmlNodeList xmlNodes = xmlDoc.DocumentElement.SelectNodes("/RittenhouseWebLoader/EmailConfigurations/EmailConfiguration");
		if (xmlNodes == null)
		{
			return;
		}
		foreach (XmlNode xmlNode in xmlNodes)
		{
			Log.DebugFormat("xmlNode: {0}", xmlNode.Name);
			if (xmlNode.Attributes == null)
			{
				throw new Exception("Invalid Email Config XML in " + xmlFilename);
			}
			XmlAttribute type = xmlNode.Attributes["type"];
			XmlAttribute send = xmlNode.Attributes["send"];
			if (type.Value == emailConfiguration.Type)
			{
				PopulateEmailAddresses(emailConfiguration, xmlNode);
				emailConfiguration.Send = send.Value.ToLower() == "true";
			}
		}
	}

	private static void PopulateEmailAddresses(EmailConfiguration emailConfiguration, XmlNode emailConfigNode)
	{
		List<string> toEmailAddresses = new List<string>();
		List<string> ccEmailAddresses = new List<string>();
		List<string> bccEmailAddresses = new List<string>();
		XmlNodeList childNodes = emailConfigNode.ChildNodes;
		foreach (XmlNode childNode in childNodes)
		{
			if (childNode.Name == "ToAddresses")
			{
				XmlNodeList emailAddressNodes = childNode.ChildNodes;
				foreach (XmlNode emailAddressNode in emailAddressNodes)
				{
					if (emailAddressNode.Name == "EmailAddress")
					{
						Log.DebugFormat("emailAddressNode: {0}", emailAddressNode.InnerText);
						toEmailAddresses.Add(emailAddressNode.InnerText);
					}
				}
			}
			if (childNode.Name == "CcAddresses")
			{
				XmlNodeList emailAddressNodes2 = childNode.ChildNodes;
				foreach (XmlNode emailAddressNode2 in emailAddressNodes2)
				{
					if (emailAddressNode2.Name == "EmailAddress")
					{
						Log.DebugFormat("emailAddressNode: {0}", emailAddressNode2.InnerText);
						ccEmailAddresses.Add(emailAddressNode2.InnerText);
					}
				}
			}
			if (!(childNode.Name == "BccAddresses"))
			{
				continue;
			}
			XmlNodeList emailAddressNodes3 = childNode.ChildNodes;
			foreach (XmlNode emailAddressNode3 in emailAddressNodes3)
			{
				if (emailAddressNode3.Name == "EmailAddress")
				{
					Log.DebugFormat("emailAddressNode: {0}", emailAddressNode3.InnerText);
					bccEmailAddresses.Add(emailAddressNode3.InnerText);
				}
			}
		}
		emailConfiguration.ToAddresses.Clear();
		if (toEmailAddresses.Count > 0)
		{
			emailConfiguration.ToAddresses.AddRange(toEmailAddresses);
		}
		emailConfiguration.CcAddresses.Clear();
		if (ccEmailAddresses.Count > 0)
		{
			emailConfiguration.CcAddresses.AddRange(ccEmailAddresses);
		}
		emailConfiguration.BccAddresses.Clear();
		if (bccEmailAddresses.Count > 0)
		{
			emailConfiguration.BccAddresses.AddRange(bccEmailAddresses);
		}
	}
}
