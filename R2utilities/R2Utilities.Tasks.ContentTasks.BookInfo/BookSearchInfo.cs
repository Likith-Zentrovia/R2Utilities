using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using Common.Logging;
using R2Utilities.DataAccess;
using R2Utilities.Utilities;
using R2V2.Core.Resource.BookSearch;

namespace R2Utilities.Tasks.ContentTasks.BookInfo;

public class BookSearchInfo
{
	protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private readonly List<string> _glossaries = new List<string>();

	private readonly List<string> _practiceAreas = new List<string>();

	private readonly ResourceCore _resource;

	private readonly List<string> _specialties = new List<string>();

	private readonly string _xmlFullFileName;

	public string Title { get; private set; }

	public string SubTitle { get; private set; }

	public string Isbn10 { get; private set; }

	public string Isbn13 { get; private set; }

	public string EIsbn { get; private set; }

	public List<Editor> Editors { get; }

	public int CopyrightYear { get; private set; }

	public string CopyrightHolder { get; private set; }

	public string Publisher { get; private set; }

	public List<string> AssociatedPublishers { get; private set; }

	public Author PrimaryAuthor { get; private set; }

	public List<Author> OtherAuthors { get; }

	public string BookStatus { get; private set; }

	public string PracticeAreas => string.Join("; ", _practiceAreas.ToArray());

	public string Specialties => string.Join("; ", _specialties.ToArray());

	public string R2ReleaseDate { get; private set; }

	public bool IsDrugMonograph { get; private set; }

	public bool IsBrandonHill { get; private set; }

	public IEnumerable<string> Glossaries => _glossaries;

	public FileInfo BookXmlFileInfo { get; }

	public XmlDocument XmlDocument { get; }

	public BookSearchInfo(ResourceCore resource, DirectoryInfo directoryInfo)
	{
		Log.Debug("BookSearchInfo() >>");
		_resource = resource;
		OtherAuthors = new List<Author>();
		Editors = new List<Editor>();
		_xmlFullFileName = directoryInfo.FullName + "/book." + resource.Isbn.Trim() + ".xml";
		Log.DebugFormat("_xmlFullFileName: {0}", _xmlFullFileName);
		BookXmlFileInfo = new FileInfo(_xmlFullFileName);
		Log.DebugFormat("File size: {0:#,###} bytes", BookXmlFileInfo.Length);
		XmlDocument = new XmlDocument
		{
			PreserveWhitespace = false,
			XmlResolver = null
		};
		Log.Info("_xmlDoc created");
		XmlDocument.Load(_xmlFullFileName);
		Log.Info("_xmlDoc loaded");
		PopulateFromXml();
		PopulateFromResource();
		PopulateGlossaries();
		Log.Debug("BookSearchInfo() <<");
	}

	private void PopulateFromXml()
	{
		Log.Debug("PopulateFromXml() >>");
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		SetBookInfo(XmlDocument);
		stopwatch.Stop();
		Log.InfoFormat("PopulateFromXml() << --> runtime: {0:#,###} ms", stopwatch.ElapsedMilliseconds);
		Log.InfoFormat("Title: {0}, SubTitle: {1}", Title, SubTitle);
		Log.InfoFormat("CopyrightYear: {0}, CopyrightHolder: {1}", CopyrightYear, CopyrightHolder);
		Log.InfoFormat("PrimaryAuthor: {0}", PrimaryAuthor?.ToString() ?? "");
		Log.InfoFormat("Editors.Count: {0}, OtherAuthors.Count: {1}", Editors.Count, OtherAuthors.Count);
	}

	private void PopulateFromResource()
	{
		BookStatus = ((_resource.StatusId == 6) ? "Active" : ((_resource.StatusId == 7) ? "Archive" : ((_resource.StatusId == 8) ? "Pre-Order" : "Inactive")));
		foreach (ResourcePracticeArea practiceArea in _resource.PracticeAreas)
		{
			string code = practiceArea.Code;
			if (!_practiceAreas.Contains(code))
			{
				_practiceAreas.Add(code);
			}
		}
		foreach (ResourceSpecialty specialty in _resource.Specialties)
		{
			string code2 = specialty.Code;
			if (!_specialties.Contains(code2))
			{
				_specialties.Add(code2);
			}
		}
		IsBrandonHill = _resource.BrandonHillStatus == 1;
		IsDrugMonograph = _resource.DrugMonograph == 1;
		R2ReleaseDate = $"{_resource.ReleaseDate:yyyy/MM/dd}";
		Isbn10 = _resource.Isbn10;
		Isbn13 = _resource.Isbn13;
		EIsbn = _resource.EIsbn;
		Publisher = _resource.PublisherName;
		if (_resource.AssociatedPublishers != null && _resource.AssociatedPublishers.Any())
		{
			AssociatedPublishers = _resource.AssociatedPublishers.Select((ResourcePublisher x) => x.PublisherName).ToList();
		}
	}

	private void SetBookInfo(XmlDocument xmlDoc)
	{
		XmlNode xmlNode = XmlHelper.GetXmlNode(xmlDoc, "//book/bookinfo");
		if (xmlNode == null)
		{
			Log.ErrorFormat("RESOURCE DOES NOT CONTAIN BOOKINFO NODE: ISBN: {0}, {1}", Isbn10, Isbn13);
			return;
		}
		List<XmlNode> editorNodes = new List<XmlNode>();
		List<XmlNode> authorNodes = new List<XmlNode>();
		foreach (XmlNode childNode in xmlNode.ChildNodes)
		{
			switch (childNode.Name)
			{
			case "title":
				Title = XmlHelper.GetXmlNodeValue(childNode);
				Log.DebugFormat("Title: {0}", Title);
				break;
			case "subtitle":
				SubTitle = XmlHelper.GetXmlNodeValue(childNode);
				Log.DebugFormat("SubTitle: {0}", SubTitle);
				break;
			case "copyright":
				SetCopyright(childNode);
				break;
			case "primaryauthor":
				SetPrimaryAuthor(childNode);
				break;
			case "editor":
				editorNodes.Add(childNode);
				break;
			case "authorgroup":
				foreach (XmlNode authorNode in childNode.ChildNodes)
				{
					if (authorNode.Name == "author")
					{
						authorNodes.Add(authorNode);
					}
				}
				break;
			}
		}
		SetEditor(editorNodes);
		SetOtherAuthors(authorNodes);
	}

	private void SetEditor(List<XmlNode> editorNodes)
	{
		Editor previousEditor = null;
		foreach (XmlNode editorNode in editorNodes)
		{
			Editor editor = ParseEditor(editorNode);
			if (previousEditor != null && string.IsNullOrEmpty(editor.LastName))
			{
				previousEditor.Affiliations.AddRange(editor.Affiliations);
				continue;
			}
			Editors.Add(editor);
			previousEditor = editor;
		}
		Log.DebugFormat("Editors.Count(): {0}", Editors.Count());
	}

	private void SetCopyright(XmlNode copyrightNode)
	{
		if (copyrightNode != null)
		{
			foreach (XmlNode childNode in copyrightNode.ChildNodes)
			{
				if (childNode.Name == "year")
				{
					string innerText = childNode.InnerText.Replace(",", "").Replace("-", "").Replace(".", "")
						.Replace("c", "")
						.Replace("and", "")
						.Replace("&", "")
						.Replace(" ", "");
					int.TryParse(innerText, out var year);
					if (year > 2100)
					{
						int year2 = year / 10000;
						int year3 = year % 10000;
						year = ((year2 > year3) ? year2 : year3);
					}
					if (CopyrightYear < year)
					{
						CopyrightYear = year;
					}
					if (CopyrightYear == 0)
					{
						Log.WarnFormat("INVALID YEAR VALUE: {0}, innerText: {1}", childNode.InnerText, innerText);
					}
				}
				else if (childNode.Name == "holder")
				{
					CopyrightHolder = childNode.InnerText;
				}
				else
				{
					Log.WarnFormat("COPYRIGHT ELEMENT NOT SUPPORTED: {0}", childNode.Name);
				}
			}
		}
		else
		{
			Log.WarnFormat("RESOURCE DOES NOT CONTAIN COPYRIGHT INFORMATION: ISBN: {0}, {1}", Isbn10, Isbn13);
		}
		Log.DebugFormat("CopyrightYear: {0}, CopyrightHolder: {1}", CopyrightYear, CopyrightHolder);
	}

	private void SetPrimaryAuthor(XmlNode node)
	{
		PrimaryAuthor = new Author();
		if (node != null)
		{
			PrimaryAuthor = ParseAuthor(node);
			Log.DebugFormat("PrimaryAuthor.LastName: {0}, PrimaryAuthor.FirstName: {1}", PrimaryAuthor.LastName, PrimaryAuthor.FirstName);
		}
		else
		{
			Log.WarnFormat("RESOURCE DOES NOT CONTAIN PRIMARY AUTHOR INFORMATION: ISBN: {0}, {1}", Isbn10, Isbn13);
		}
	}

	private void SetOtherAuthors(List<XmlNode> authorNodes)
	{
		foreach (XmlNode authorNode in authorNodes)
		{
			OtherAuthors.Add(ParseAuthor(authorNode));
		}
	}

	private Author ParseAuthor(XmlNode node)
	{
		Author author = new Author();
		XmlNode childNode = node.FirstChild;
		if (childNode != null && childNode.Name == "personname")
		{
			XmlNodeList authorNodes = childNode.ChildNodes;
			foreach (XmlNode authorNode in authorNodes)
			{
				switch (authorNode.Name)
				{
				case "firstname":
					author.FirstName = authorNode.InnerText;
					break;
				case "degree":
					author.Degrees = authorNode.InnerText;
					break;
				case "surname":
					author.LastName = authorNode.InnerText;
					break;
				case "lineage":
					author.Lineage = authorNode.InnerText;
					break;
				case "othername":
				{
					XmlAttribute role = authorNode.Attributes["role"];
					if (role == null)
					{
						Log.WarnFormat("othername role attribute is null, assume mi");
						author.MiddleInitial = authorNode.InnerText;
					}
					else if (role.Value != "mi")
					{
						Log.WarnFormat("Invalid othername role attribute: {0}", role.Value);
					}
					else
					{
						author.MiddleInitial = authorNode.InnerText;
					}
					break;
				}
				default:
					Log.WarnFormat("AUTHOR NODE NOT SUPPORTED, node name: {0}", authorNode.Name);
					break;
				}
			}
		}
		else if (childNode != null)
		{
			Log.WarnFormat("AUTHOR (personname level) NODE NOT SUPPORTED, node name: {0}", childNode.Name);
		}
		return author;
	}

	private Editor ParseEditor(XmlNode node)
	{
		Editor editor = new Editor();
		XmlNodeList editorNodes = ((node.ChildNodes.Count == 1 && node.ChildNodes[0].Name == "personname") ? node.ChildNodes[0].ChildNodes : node.ChildNodes);
		foreach (XmlNode editorNode in editorNodes)
		{
			switch (editorNode.Name)
			{
			case "firstname":
				editor.FirstName = editorNode.InnerText;
				break;
			case "degree":
				editor.Degrees = editorNode.InnerText;
				break;
			case "surname":
				editor.LastName = editorNode.InnerText;
				break;
			case "lineage":
				editor.Lineage = editorNode.InnerText;
				break;
			case "othername":
			{
				XmlAttribute role = editorNode.Attributes["role"];
				if (role == null)
				{
					Log.WarnFormat("othername role attribute is null, assume mi");
					editor.MiddleInitial = editorNode.InnerText;
				}
				else if (role.Value != "mi")
				{
					Log.WarnFormat("Invalid othername role attribute: {0}", role.Value);
				}
				else
				{
					editor.MiddleInitial = editorNode.InnerText;
				}
				break;
			}
			case "affiliation":
			{
				EditorAffiliation editorAffiliation = new EditorAffiliation();
				editor.Affiliations.Add(editorAffiliation);
				XmlNodeList affiliationChildNodes = editorNode.ChildNodes;
				foreach (XmlNode affiliationChildNode in affiliationChildNodes)
				{
					if (affiliationChildNode.Name == "jobtitle")
					{
						editorAffiliation.JobTitle = affiliationChildNode.InnerText;
						continue;
					}
					if (affiliationChildNode.Name == "orgname")
					{
						editorAffiliation.OrganizationName = affiliationChildNode.InnerText;
						continue;
					}
					Log.WarnFormat("EDITOR AFFILIATION ELEMENT NOT SUPPORTED, {0} = {1}", affiliationChildNode.Name, affiliationChildNode.InnerText);
				}
				break;
			}
			default:
				Log.WarnFormat("EDITOR NODE NOT SUPPORTED, node name: {0}", editorNode.Name);
				break;
			}
		}
		return editor;
	}

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder("BookSearchInfo = [").AppendFormat("Isbn10 = {0}", Isbn10).AppendFormat(", Isbn13 = {0}", Isbn13).AppendFormat(", Title = {0}", Title)
			.AppendFormat(", SubTitle = {0}", SubTitle)
			.AppendFormat(", CopyrightYear = {0}", CopyrightYear)
			.AppendFormat(", CopyrightHolder = {0}", CopyrightHolder)
			.AppendFormat(", BookStatus = {0}", BookStatus)
			.AppendFormat(", PracticeAreas = [{0}]", PracticeAreas)
			.AppendFormat(", Specialties = [{0}]", Specialties)
			.AppendFormat(", R2ReleaseDate = {0}", R2ReleaseDate)
			.AppendFormat(", IsDrugMonograph = {0}", IsDrugMonograph)
			.AppendFormat(", IsBrandonHill = {0}", IsBrandonHill)
			.AppendFormat(", EIsbn = {0}", EIsbn)
			.AppendLine()
			.AppendFormat("\t, PrimaryAuthor = {0}", PrimaryAuthor);
		foreach (Author author in OtherAuthors)
		{
			sb.AppendLine().Append("\t, OtherAuthors: ").Append(author);
		}
		foreach (Editor editor in Editors)
		{
			sb.AppendLine().Append("\t, Editors: ").Append(editor);
		}
		sb.AppendFormat(", Publisher = {0}", Publisher);
		if (AssociatedPublishers != null)
		{
			foreach (string publisher in AssociatedPublishers)
			{
				sb.AppendLine().Append("\t, AssociatedPublishers: ").Append(publisher);
			}
		}
		sb.Append("]");
		return sb.ToString();
	}

	public BookSearchResource ToBookSearchResource(string contentPath, string isbn)
	{
		return new BookSearchResource(contentPath)
		{
			Isbn = isbn,
			Isbn10 = Isbn10,
			Isbn13 = Isbn13,
			EIsbn = EIsbn,
			Title = Title,
			SubTitle = SubTitle,
			PracticeAreas = _practiceAreas,
			Specialties = _specialties,
			CopyRight = ((CopyrightYear > 0) ? $"{CopyrightYear}" : string.Empty),
			R2ReleaseDate = R2ReleaseDate,
			IsDrugMonograph = IsDrugMonograph,
			IsBrandonHill = IsBrandonHill,
			StatusString = BookStatus,
			CopyRightHolder = CopyrightHolder,
			ConsolidatedPublisherNames = AssociatedPublishers,
			PrimaryAuthor = PrimaryAuthor?.GetFullName(lastNameFirst: true),
			Authors = OtherAuthors?.Select((Author x) => x.GetFullName(lastNameFirst: false)).ToList(),
			Editors = Editors?.Select((Editor x) => x.GetXmlElementValue()).ToList()
		};
	}

	private void AppendXmlNode(XmlNode parentNode, string nodeName, string nodeValue)
	{
		XmlHelper.AppendXmlNode(XmlDocument, parentNode, nodeName, nodeValue);
	}

	private void PopulateGlossaries()
	{
		IList<XmlNode> nodes = XmlHelper.GetXmlNodes(XmlDocument, "//book/glossary");
		if (nodes == null || !nodes.Any())
		{
			return;
		}
		foreach (XmlNode xmlNode in nodes)
		{
			XmlAttribute attribute = xmlNode.Attributes["id"];
			if (attribute != null)
			{
				Log.DebugFormat("glossary: {0}", attribute.Value);
				_glossaries.Add(attribute.Value);
			}
		}
	}
}
