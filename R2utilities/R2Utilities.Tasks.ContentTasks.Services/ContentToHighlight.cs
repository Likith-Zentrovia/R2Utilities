using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using R2Utilities.Utilities;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class ContentToHighlight : R2UtilitiesBase
{
	private string _fileName;

	public string OutputContent { get; set; }

	public string TempContent { get; set; }

	public string FileName
	{
		get
		{
			return _fileName;
		}
		set
		{
			_fileName = value;
			string[] parts = _fileName.Split('.');
			if (parts.Length > 3)
			{
				SectionId = parts[2];
				ChapterId = ((SectionId.Length >= 6) ? SectionId.Substring(0, 6) : SectionId);
			}
			else
			{
				SectionId = _fileName;
				ChapterId = _fileName;
			}
		}
	}

	public HashSet<string> Words { get; set; }

	public HashSet<string> Keywords { get; set; }

	public string ChapterId { get; private set; }

	public string SectionId { get; private set; }

	public bool IsIgnored => new string[1] { "toc." }.Any((string s) => FileName.StartsWith(s));

	public bool IsBook => new string[1] { "book." }.Any((string s) => FileName.StartsWith(s));

	public string ResourcePath { get; set; }

	public string OutputPath { get; set; }

	public string TempPath { get; set; }

	public string BackupPath { get; set; }

	public TermHighlightType TermHighlightType { get; set; }

	private string XPathToStrip => GetXPathToStrip();

	private Dictionary<string, string> EntityValues { get; } = new Dictionary<string, string>();

	public void Load(bool removeComments = false)
	{
		XmlDocument xmlDoc = new XmlDocument
		{
			XmlResolver = null,
			PreserveWhitespace = true
		};
		string text = File.ReadAllText(ResourcePath);
		text = ((!IsIgnored) ? PreFormat(text) : text);
		if (removeComments || XPathToStrip != null)
		{
			xmlDoc = LoadXmlDocument(xmlDoc, text);
			if (IsBook && removeComments)
			{
				xmlDoc = XmlHelper.RemoveComments(xmlDoc);
			}
			if (XPathToStrip != null)
			{
				xmlDoc = XmlHelper.StripTags(xmlDoc, XPathToStrip);
			}
			OutputContent = xmlDoc.OuterXml;
		}
		else
		{
			OutputContent = text;
		}
		Keywords = GetKeywords(xmlDoc);
	}

	public void WriteOutput()
	{
		OutputContent = ((!IsIgnored) ? PostFormat(OutputContent) : OutputContent);
		File.WriteAllText(OutputPath, OutputContent);
	}

	private string PreFormat(string content)
	{
		return content.Replace("<?lb?>", "<lb/>").Replace("<?lb ?>", "<lb />");
	}

	private string PostFormat(string content)
	{
		foreach (string entity in EntityValues.Keys)
		{
			string value = EntityValues[entity];
			content = content.Replace(value, entity);
		}
		return content.Replace("<lb/>", "<?lb?>").Replace("<lb />", "<?lb ?>");
	}

	private XmlDocument LoadXmlDocument(XmlDocument xmlDoc, string text)
	{
		EntityValues.Clear();
		bool isLoaded = false;
		while (!isLoaded)
		{
			try
			{
				xmlDoc.Load(new StringReader(text));
				isLoaded = true;
			}
			catch (XmlException ex)
			{
				if (!ex.Message.Contains("undeclared entity"))
				{
					throw;
				}
				text = ReplaceEntity(text, ex.Message.Split('\'')[1]);
			}
		}
		return xmlDoc;
	}

	private string ReplaceEntity(string text, string entityName)
	{
		string entity = "&" + entityName + ";";
		string value = WebUtility.HtmlDecode(entity);
		if (string.Equals(entity, value))
		{
			value = "??" + entityName + "??";
		}
		EntityValues.Add(entity, value);
		return text.Replace(entity, value);
	}

	private string GetXPathToStrip()
	{
		return TermHighlightType switch
		{
			TermHighlightType.Tabers => "//ulink[@type='tabers']", 
			TermHighlightType.IndexTerms => "//ulink[@type='disease' or @type='drug' or @type='drugsynonym' or @type='keywords']", 
			_ => throw new Exception($"ResourceToHighlight error - Unexpected TermHighlightType: {TermHighlightType}"), 
		};
	}

	private static HashSet<string> GetKeywords(XmlDocument xmlDoc)
	{
		return new HashSet<string>((from n in XmlHelper.GetXmlNodes(xmlDoc, "//risterm[../risrule[.='linkKeyword']]")
			select n.InnerText.ToLower()).Distinct());
	}
}
