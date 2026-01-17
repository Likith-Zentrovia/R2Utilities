using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.DataAccess.Tabers;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Utilities;
using R2V2.Extensions;

namespace R2Utilities.Tasks.ContentTasks;

public class LoadTabersDictionaryTask : TaskBase
{
	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly TabersDataService _tabersDataService;

	private const string Isbn = "080362977X";

	private const string ContentLocation = "\\\\technonase1\\technotects\\Clients\\Rittenhouse\\R2v2\\Content\\Dev";

	private readonly string Section = string.Format("\\\\technonase1\\technotects\\Clients\\Rittenhouse\\R2v2\\Content\\Dev\\html\\{0}\\sect1.{0}.{{0}}.html", "080362977X");

	private readonly string Toc = string.Format("\\\\technonase1\\technotects\\Clients\\Rittenhouse\\R2v2\\Content\\Dev\\xml\\{0}\\toc.{0}.xml", "080362977X");

	public LoadTabersDictionaryTask(IR2UtilitiesSettings r2UtilitiesSettings, TabersDataService tabersDataService)
		: base("LoadTabersDictionaryTask", "-LoadTabersDictionaryTask", "07", TaskGroup.ContentLoading, "Task to load Tabers Dictionary terms", enabled: true)
	{
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_tabersDataService = tabersDataService;
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will load the Taber's Dictionary database from the Taber's Source XML.";
		TaskResultStep step = new TaskResultStep
		{
			Name = "LoadTabersDictionaryTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		UpdateTaskResult();
		try
		{
			LoadTabersContent();
		}
		catch (Exception ex)
		{
			R2UtilitiesBase.Log.Error(ex.Message, ex);
			step.CompletedSuccessfully = false;
			step.Results = ex.Message;
			throw;
		}
		finally
		{
			step.EndTime = DateTime.Now;
			UpdateTaskResult();
		}
	}

	private IEnumerable<FileInfo> GetXmlFileInfo()
	{
		string tabersXmlPath = _r2UtilitiesSettings.TabersXmlPath;
		DirectoryInfo directoryInfo = new DirectoryInfo(tabersXmlPath);
		return directoryInfo.EnumerateFiles();
	}

	private void LoadTabers()
	{
		IEnumerable<FileInfo> xmlFileInfo = GetXmlFileInfo();
		foreach (FileInfo fileInfo in xmlFileInfo)
		{
			XmlDocument xmlDocument = new XmlDocument();
			string xmlPath = fileInfo.FullName;
			xmlDocument.Load(xmlPath);
			LoadTerms(xmlDocument);
		}
	}

	private void LoadTerms(XmlNode document)
	{
		XmlNodeList mainEntries = document.SelectNodes("//mainentry");
		if (mainEntries == null)
		{
			return;
		}
		foreach (XmlNode mainEntry in mainEntries)
		{
			LoadTerm(mainEntry);
		}
	}

	private void LoadTerm(XmlNode mainEntry)
	{
		int mainEntryKey = LoadMainEntry(mainEntry);
		LoadSpecialty(mainEntry, mainEntryKey);
		LoadSenses(mainEntry, mainEntryKey);
		LoadVariants(mainEntry, mainEntryKey);
		LoadPronounces(mainEntry, mainEntryKey);
		LoadPlurals(mainEntry, mainEntryKey);
		LoadEtymologies(mainEntry, mainEntryKey);
		LoadSubentries(mainEntry, mainEntryKey);
		LoadAbbrevXentries(mainEntry, mainEntryKey, null);
		LoadXentries(mainEntry, mainEntryKey, null);
	}

	private int LoadMainEntry(XmlNode mainEntry)
	{
		string currency = XmlHelper.GetAttributeValue(mainEntry, "currency");
		DateTime? dateRevised = XmlHelper.GetAttributeValue(mainEntry, "date-revised").TryParseExact<DateTime>("ddd MMM dd HH:mm:ss yyyy");
		string name = mainEntry.Attributes["dbname"].IfNotNull((XmlAttribute a) => a.InnerXml);
		int? editionAdded = XmlHelper.GetAttributeValue(mainEntry, "edition-added").TryParse<int>();
		string letter = XmlHelper.GetAttributeValue(mainEntry, "letter");
		string output = XmlHelper.GetAttributeValue(mainEntry, "output");
		int? sortOrder = XmlHelper.GetAttributeValue(mainEntry, "sortorder").TryParse<int>();
		string spaceSaver = XmlHelper.GetAttributeValue(mainEntry, "spacesaver");
		string xlinkType = XmlHelper.GetAttributeValue(mainEntry, "xlink:type");
		string biography = mainEntry.SelectSingleNode("biography").IfNotNull((XmlNode node) => node.InnerXml);
		string abbrev = mainEntry.SelectSingleNode("abbrev").IfNotNull((XmlNode node) => node.InnerXml);
		string symb = mainEntry.SelectSingleNode("symb").IfNotNull((XmlNode node) => node.InnerXml);
		XmlNode orthoDisp = mainEntry.SelectSingleNode("ortho-disp");
		int orthoDispKey = LoadOrthoDisp(orthoDisp, null);
		return _tabersDataService.InsertMainEntry(currency, dateRevised, name, editionAdded, letter, output, sortOrder, spaceSaver, xlinkType, orthoDispKey, biography, abbrev, symb);
	}

	private int LoadOrthoDisp(XmlNode orthoDisp, int? pluralKey)
	{
		string orthoDispId = XmlHelper.GetAttributeValue(orthoDisp, "id");
		string orthoDispText = orthoDisp.InnerXml;
		return _tabersDataService.InsertOrthoDisp(orthoDispId, orthoDispText, pluralKey);
	}

	private void LoadSpecialty(XmlNode currentNode, int mainEntryKey)
	{
		XmlNode specialty = currentNode.SelectSingleNode("specialty");
		if (specialty != null)
		{
			XmlNode primary1 = specialty.SelectSingleNode("primary1");
			string primary1Code = XmlHelper.GetAttributeValue(primary1, "code");
			_tabersDataService.InsertSpecialty(mainEntryKey, primary1Code);
		}
	}

	private void LoadSenses(XmlNode currentNode, int mainEntryKey)
	{
		XmlNodeList senses = currentNode.SelectNodes("sense");
		if (senses == null)
		{
			return;
		}
		foreach (XmlNode sense in senses)
		{
			string definition = sense.SelectSingleNode("definition").IfNotNull((XmlNode node) => node.InnerXml);
			int senseKey = _tabersDataService.InsertSense(mainEntryKey, definition);
			LoadDefExp(sense, senseKey);
		}
	}

	private void LoadVariants(XmlNode currentNode, int mainEntryKey)
	{
		XmlNodeList variants = currentNode.SelectNodes("variant");
		if (variants == null)
		{
			return;
		}
		foreach (XmlNode variant in variants)
		{
			XmlNode orthoDisp = variant.SelectSingleNode("ortho-disp");
			int orthoDispKey = LoadOrthoDisp(orthoDisp, null);
			_tabersDataService.InsertVariant(mainEntryKey, orthoDispKey);
		}
	}

	private void LoadPronounces(XmlNode currentNode, int mainEntryKey)
	{
		XmlNodeList pronounces = currentNode.SelectNodes("pronounce");
		if (pronounces == null)
		{
			return;
		}
		foreach (XmlNode pronounce in pronounces)
		{
			XmlNode audio = pronounce.SelectSingleNode("audio");
			if (audio != null)
			{
				pronounce.RemoveChild(audio);
			}
			string pronounceText = pronounce.InnerXml;
			string audioFile = audio.IfNotNull((XmlNode a) => a.Attributes["file"].Value);
			_tabersDataService.InsertPronounce(mainEntryKey, pronounceText, audioFile);
		}
	}

	private void LoadPlurals(XmlNode currentNode, int mainEntryKey)
	{
		XmlNodeList plurals = currentNode.SelectNodes("plural");
		if (plurals == null)
		{
			return;
		}
		foreach (XmlNode plural in plurals)
		{
			XmlNodeList orthoDisps = plural.SelectNodes("ortho-disp");
			int pluralKey = _tabersDataService.InsertPlural(mainEntryKey);
			if (orthoDisps == null)
			{
				break;
			}
			foreach (XmlNode orthoDisp in orthoDisps)
			{
				LoadOrthoDisp(orthoDisp, pluralKey);
			}
		}
	}

	private void LoadEtymologies(XmlNode currentNode, int mainEntryKey)
	{
		XmlNodeList etymologies = currentNode.SelectNodes("etymology");
		if (etymologies == null)
		{
			return;
		}
		foreach (XmlNode etymology in etymologies)
		{
			string etymologyText = etymology.InnerXml;
			_tabersDataService.InsertEtymology(mainEntryKey, etymologyText);
		}
	}

	private void LoadSubentries(XmlNode currentNode, int mainEntryKey)
	{
		XmlNodeList subentries = currentNode.SelectNodes("subentry");
		if (subentries == null)
		{
			return;
		}
		foreach (XmlNode subentry in subentries)
		{
			string currency = XmlHelper.GetAttributeValue(subentry, "currency");
			DateTime? dateRevised = XmlHelper.GetAttributeValue(subentry, "date-revised").TryParseExact<DateTime>("ddd MMM dd HH:mm:ss yyyy");
			string name = subentry.Attributes["dbname"].IfNotNull((XmlAttribute a) => a.InnerXml);
			int? editionAdded = XmlHelper.GetAttributeValue(subentry, "edition-added").TryParse<int>();
			string output = XmlHelper.GetAttributeValue(subentry, "output");
			string spaceSaver = XmlHelper.GetAttributeValue(subentry, "spacesaver");
			string xlinkType = XmlHelper.GetAttributeValue(subentry, "xlink:type");
			int subentryKey = _tabersDataService.InsertSubentry(mainEntryKey, currency, dateRevised, name, editionAdded, output, spaceSaver, xlinkType);
			LoadAbbrevXentries(subentry, null, subentryKey);
			LoadXentries(subentry, null, subentryKey);
		}
	}

	private void LoadAbbrevXentries(XmlNode currentNode, int? mainEntryKey, int? subentryKey)
	{
		XmlNodeList abbrevXentries = currentNode.SelectNodes("abbrev-xentry");
		if (abbrevXentries == null)
		{
			return;
		}
		foreach (XmlNode abbrevXentry in abbrevXentries)
		{
			string xlinkHref = XmlHelper.GetAttributeValue(abbrevXentry, "xlink:href");
			string abbrevXentryText = abbrevXentry.InnerXml;
			_tabersDataService.InsertAbbrevXentry(mainEntryKey, subentryKey, xlinkHref, abbrevXentryText);
		}
	}

	private void LoadXentries(XmlNode currentNode, int? mainEntryKey, int? subentryKey)
	{
		XmlNodeList xentries = currentNode.SelectNodes("xentry");
		if (xentries == null)
		{
			return;
		}
		foreach (XmlNode xentry in xentries)
		{
			string xlinkHref = XmlHelper.GetAttributeValue(xentry, "xlink:href");
			string xentryText = xentry.InnerXml;
			_tabersDataService.InsertXentry(mainEntryKey, subentryKey, xlinkHref, xentryText);
		}
	}

	private void LoadDefExp(XmlNode currentNode, int senseKey)
	{
		XmlNode defExp = currentNode.SelectSingleNode("defexp");
		if (defExp != null)
		{
			string output = XmlHelper.GetAttributeValue(defExp, "output");
			string defExpText = defExp.InnerXml;
			_tabersDataService.InsertDefExp(senseKey, output, defExpText);
		}
	}

	private void LoadTabersContent()
	{
		XDocument doc = XDocument.Load(Toc);
		IEnumerable<XElement> tocEntries = from e in doc.Descendants("tocchap").Descendants("tocentry")
			where e.Parent.Name == "toclevel1"
			select e;
		foreach (XElement tocEntry in tocEntries)
		{
			string term = tocEntry.Value.Trim();
			if (!(term == ""))
			{
				string sectionId = tocEntry.Attribute("linkend").Value;
				string content = TermContent(sectionId);
				if (content != null)
				{
					_tabersDataService.InsertTermContent(term, content, sectionId);
				}
			}
		}
	}

	private string TermContent(string termSectionId)
	{
		string file = SectionFile(termSectionId);
		XDocument doc;
		try
		{
			doc = XDocument.Load(file);
		}
		catch
		{
			string fileContent = File.ReadAllText(file);
			fileContent = EncodeUnencodedAmpersands(fileContent);
			doc = XDocument.Load(new StringReader(fileContent));
		}
		return TermContent(doc);
	}

	private static string TermContent(XContainer doc)
	{
		string header = doc.Descendants("h2").First((XElement e) => e.Parent.Name == "body").ToString();
		string definition = doc.Descendants("p").First((XElement e) => e.Parent.Name == "body").ToString();
		return header + definition;
	}

	private string SectionFile(string id)
	{
		return string.Format(Section, id);
	}

	private static string EncodeUnencodedAmpersands(string text)
	{
		return Regex.Replace(text, "\n\t\t\t\t\t# Match & that is not part of an HTML entity.\n\t\t\t\t\t&                  # Match literal &.\n\t\t\t\t\t(?!                # But only if it is NOT...\n\t\t\t\t\t\\w+;               # an alphanumeric entity,\n\t\t\t\t\t| \\#[0-9]+;        # or a decimal entity,\n\t\t\t\t\t| \\#x[0-9A-F]+;    # or a hexadecimal entity.\n\t\t\t\t\t)                  # End negative lookahead.", "&amp;", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
	}
}
