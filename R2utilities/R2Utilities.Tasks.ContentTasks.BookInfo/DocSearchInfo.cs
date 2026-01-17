using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using R2Utilities.Utilities;

namespace R2Utilities.Tasks.ContentTasks.BookInfo;

public class DocSearchInfo : R2UtilitiesBase
{
	public BookSearchInfo BookSearchInfo { get; }

	public string XmlFullFilePath { get; }

	public string FilePrefix { get; }

	public string MetaTags { get; private set; }

	public DocSearchInfo(BookSearchInfo bookSearchInfo, string xmlFullFilePath, string filePrefix)
	{
		BookSearchInfo = bookSearchInfo;
		XmlFullFilePath = xmlFullFilePath;
		FilePrefix = filePrefix;
		PopulateMetaTags();
	}

	private void PopulateMetaTags()
	{
		Stopwatch modifyHtmlStopwatch = new Stopwatch();
		modifyHtmlStopwatch.Start();
		XmlDocument xmlDoc = new XmlDocument
		{
			PreserveWhitespace = false,
			XmlResolver = null
		};
		xmlDoc.Load(XmlFullFilePath);
		string indexTerms = GetRisIndexTerms(xmlDoc);
		string primaryAuthor = BookSearchInfo.PrimaryAuthor.GetFullName(lastNameFirst: true);
		string sectionId = GetSectionId(xmlDoc);
		string sectionTitle = GetSectionTitle(xmlDoc);
		string chapterTitle = GetChapterTitle(xmlDoc);
		string chapterId = GetChapterId(xmlDoc);
		string chapterNumber = GetChapterNumber(xmlDoc);
		StringBuilder metaTags = new StringBuilder().AppendLine("<!-- r2v2 meta tags - start -->");
		AppendMetaTag(metaTags, "r2SectionId", sectionId, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2SectionTitle", sectionTitle, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2ChapterTitle", chapterTitle, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2ChapterId", chapterId, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2ChapterNumber", chapterNumber, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2DrugMonograph", BookSearchInfo.IsDrugMonograph ? "DrugMonograph" : null, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2BrandonHill", BookSearchInfo.IsBrandonHill ? "BrandonHill" : null, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2ReleaseDate", BookSearchInfo.R2ReleaseDate, includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2CopyrightYear", $"{BookSearchInfo.CopyrightYear}", includeIfEmpty: false);
		AppendMetaTag(metaTags, "r2BookTitle", BookSearchInfo.Title, includeIfEmpty: true);
		AppendMetaTag(metaTags, "r2PrimaryAuthor", primaryAuthor, includeIfEmpty: true);
		AppendMetaTag(metaTags, "r2Publisher", BookSearchInfo.Publisher, includeIfEmpty: true);
		if (BookSearchInfo.AssociatedPublishers != null)
		{
			foreach (string publisher in BookSearchInfo.AssociatedPublishers)
			{
				AppendMetaTag(metaTags, "r2AssociatedPublisher", publisher, includeIfEmpty: true);
			}
		}
		AppendMetaTag(metaTags, "r2PracticeArea", BookSearchInfo.PracticeAreas, includeIfEmpty: true);
		AppendMetaTag(metaTags, "r2Specialty", BookSearchInfo.Specialties, includeIfEmpty: true);
		AppendMetaTag(metaTags, "r2BookStatus", BookSearchInfo.BookStatus, includeIfEmpty: true);
		AppendMetaTag(metaTags, "r2IndexTerms", indexTerms, includeIfEmpty: true);
		AppendMetaTag(metaTags, "r2Isbn10", BookSearchInfo.Isbn10, includeIfEmpty: true);
		AppendMetaTag(metaTags, "r2Isbn13", BookSearchInfo.Isbn13, includeIfEmpty: true);
		if (!string.IsNullOrWhiteSpace(BookSearchInfo.EIsbn))
		{
			AppendMetaTag(metaTags, "r2EIsbn", BookSearchInfo.EIsbn, includeIfEmpty: true);
		}
		metaTags.AppendLine("<!-- r2v2 meta tags - end -->");
		MetaTags = metaTags.ToString();
	}

	private void AppendMetaTag(StringBuilder buffer, string name, string content, bool includeIfEmpty)
	{
		if (includeIfEmpty || !string.IsNullOrEmpty(content))
		{
			buffer.AppendFormat("<meta name=\"{0}\" content=\"{1}\" />", name, content).AppendLine();
		}
	}

	private string GetRisIndexTerms(XmlDocument xmlDoc)
	{
		List<XmlNode> ristermNodes = XmlHelper.GetXmlNodes(xmlDoc, "//risindex/risterm");
		if (ristermNodes == null)
		{
			return string.Empty;
		}
		StringBuilder terms = new StringBuilder();
		foreach (XmlNode ristermNode in ristermNodes)
		{
			terms.AppendFormat("{0}{1}", (terms.Length > 0) ? ", " : string.Empty, ristermNode.InnerText.Trim());
		}
		return terms.ToString();
	}

	private string GetSectionId(XmlDocument xmlDoc)
	{
		XmlNode sectionNode = XmlHelper.GetXmlNode(xmlDoc, "//" + FilePrefix);
		if (sectionNode == null)
		{
			return string.Empty;
		}
		return XmlHelper.GetAttributeValue(sectionNode, "id");
	}

	private string GetSectionTitle(XmlDocument xmlDoc)
	{
		XmlNode sectionTitleNode = XmlHelper.GetXmlNode(xmlDoc, "//" + FilePrefix + "/title");
		string sectionTitle = ((sectionTitleNode == null) ? string.Empty : sectionTitleNode.InnerText);
		if (string.IsNullOrEmpty(sectionTitle))
		{
			R2UtilitiesBase.Log.DebugFormat("EMPTY SECTION TITLE - {0}", XmlFullFilePath);
		}
		return sectionTitle;
	}

	private string GetChapterTitle(XmlDocument xmlDoc)
	{
		XmlNode chapterTitleNode = XmlHelper.GetXmlNode(xmlDoc, "//risinfo/chaptertitle");
		string chapterTitle = ((chapterTitleNode == null) ? string.Empty : chapterTitleNode.InnerText);
		if (string.IsNullOrEmpty(chapterTitle))
		{
			R2UtilitiesBase.Log.DebugFormat("EMPTY CHAPTER TITLE - {0}", XmlFullFilePath);
		}
		return chapterTitle;
	}

	private string GetChapterId(XmlDocument xmlDoc)
	{
		XmlNode chapterIdNode = XmlHelper.GetXmlNode(xmlDoc, "//risinfo/chapterid");
		string chapterId = ((chapterIdNode == null) ? string.Empty : chapterIdNode.InnerText);
		if (string.IsNullOrEmpty(chapterId))
		{
			R2UtilitiesBase.Log.DebugFormat("EMPTY CHAPTER ID - {0}", XmlFullFilePath);
		}
		return chapterId;
	}

	private string GetChapterNumber(XmlDocument xmlDoc)
	{
		XmlNode chapterNumberNode = XmlHelper.GetXmlNode(xmlDoc, "//risinfo/chapternumber");
		string chapterNuber = ((chapterNumberNode == null) ? string.Empty : chapterNumberNode.InnerText);
		if (string.IsNullOrEmpty(chapterNuber))
		{
			R2UtilitiesBase.Log.DebugFormat("EMPTY CHAPTER NUMBER - {0}", XmlFullFilePath);
		}
		return chapterNuber;
	}
}
