using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using log4net;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Infrastructure.Settings;
using R2Utilities.Utilities;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Tasks.ContentTasks.Services;

public class TocXmlService
{
	private static readonly ILog Log = LogManager.GetLogger(typeof(TocXmlService));

	private readonly IContentSettings _contentSettings;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	public TocXmlService(IContentSettings contentSettings, IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_contentSettings = contentSettings;
		_r2UtilitiesSettings = r2UtilitiesSettings;
	}

	public TaskResultStep UpdateTocXml(string isbn, TaskResult taskResult, int resourceId)
	{
		Log.InfoFormat(">+++> STEP - Update toc.xml for ISBN: {0}", isbn);
		TaskResultStep step = new TaskResultStep
		{
			Name = $"Update toc.xml for ISBN: {isbn}, Id: {resourceId}",
			StartTime = DateTime.Now
		};
		taskResult.AddStep(step);
		List<string> warningMessages = new List<string>();
		int bookXmlNodesCount = 0;
		int tocXmlNodesCount = 0;
		int tocXmlNodesUpdated = 0;
		string errorMessage = null;
		try
		{
			string resourceXmlDirectory = Path.Combine(_contentSettings.ContentLocation, isbn);
			Log.DebugFormat("resourceXmlDirectory: {0}", resourceXmlDirectory);
			if (!Directory.Exists(resourceXmlDirectory))
			{
				errorMessage = "ERROR - directory does not exist: " + resourceXmlDirectory;
				return step;
			}
			string bookXml = Path.Combine(resourceXmlDirectory, "book." + isbn + ".xml");
			string bookXmlNoComments = Path.Combine(resourceXmlDirectory, "book." + isbn + ".no.comments.xml");
			Log.DebugFormat("bookXml: {0}", bookXml);
			if (!File.Exists(bookXml))
			{
				errorMessage = "ERROR - book.xml does not exist: " + bookXml;
				return step;
			}
			string tocXml = Path.Combine(resourceXmlDirectory, "toc." + isbn + ".xml");
			Log.DebugFormat("tocXml: {0}", tocXml);
			FileInfo tocXmlfileInfo = new FileInfo(tocXml);
			if (!tocXmlfileInfo.Exists)
			{
				errorMessage = "ERROR - toc.xml does not exist: " + tocXml;
				return step;
			}
			string tocXmlBackup = Path.Combine(resourceXmlDirectory, $"toc.{isbn}.xml.{tocXmlfileInfo.CreationTime:yyyyMMdd-HHmmss}.backup");
			Log.DebugFormat("tocXmlBackup: {0}", tocXmlBackup);
			XmlReaderSettings settings = new XmlReaderSettings
			{
				IgnoreComments = true,
				XmlResolver = null,
				DtdProcessing = DtdProcessing.Parse
			};
			XmlReader reader = XmlReader.Create(bookXml, settings);
			XmlDocument bookXmlDoc = new XmlDocument
			{
				PreserveWhitespace = false,
				XmlResolver = null
			};
			bookXmlDoc.Load(reader);
			XmlDocument tocXmlDoc = new XmlDocument
			{
				PreserveWhitespace = false,
				XmlResolver = null
			};
			tocXmlDoc.Load(tocXml);
			List<XmlNode> bookXmlChapterNodes = XmlHelper.GetXmlNodes(bookXmlDoc, "//chapter");
			List<XmlNode> tocXmlTocEntryNodes = XmlHelper.GetXmlNodes(tocXmlDoc, "//tocentry");
			Log.DebugFormat("bookXmlChapterNodes.Count: {0}, tocXmlTocEntryNodes.Count: {1}", bookXmlChapterNodes.Count, tocXmlTocEntryNodes.Count);
			bookXmlNodesCount = bookXmlChapterNodes.Count;
			tocXmlNodesCount = tocXmlTocEntryNodes.Count;
			foreach (XmlNode bookXmlChapterNode in bookXmlChapterNodes)
			{
				if (bookXmlChapterNode.Attributes == null || bookXmlChapterNode.Attributes["id"] == null || string.IsNullOrWhiteSpace(bookXmlChapterNode.Attributes["id"].Value))
				{
					warningMessages.Add("WARNING! - id attribute is missing - " + bookXmlChapterNode.Name + " = " + bookXmlChapterNode.InnerText);
					continue;
				}
				string id = bookXmlChapterNode.Attributes["id"].Value;
				if (bookXmlChapterNode.Attributes["label"] == null || string.IsNullOrWhiteSpace(bookXmlChapterNode.Attributes["label"].Value))
				{
					Log.InfoFormat("label is null or missing for id: {0}", id);
					continue;
				}
				string label = bookXmlChapterNode.Attributes["label"].Value;
				Log.DebugFormat("id: {0}, label: {1}", id, label);
				if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))
				{
					warningMessages.Add("WARNING! - id or label is empty - id: " + id + ", label: " + label);
					continue;
				}
				XmlNode tocXmlNode = null;
				foreach (XmlNode xmlNode in tocXmlTocEntryNodes)
				{
					if (xmlNode.Attributes != null && xmlNode.Attributes["linkend"].Value.Equals(id))
					{
						tocXmlNode = xmlNode;
					}
				}
				if (tocXmlNode == null)
				{
					warningMessages.Add("WARNING! - id not found in toc.xml - id: " + id + ", label: " + label);
					continue;
				}
				string titlePrefix = label + ": ";
				if (tocXmlNode.InnerText.StartsWith(titlePrefix))
				{
					warningMessages.Add("WARNING! - text already contains label - id: " + id + ", label: " + label + ", text: " + tocXmlNode.InnerText);
				}
				else
				{
					tocXmlNode.InnerText = label + ": " + tocXmlNode.InnerText;
					tocXmlNodesUpdated++;
				}
			}
			if (tocXmlNodesUpdated > 0)
			{
				if (!File.Exists(tocXmlBackup))
				{
					Log.InfoFormat("renamed file {0} to {1}", tocXmlfileInfo.Name, tocXmlBackup);
					tocXmlfileInfo.MoveTo(tocXmlBackup);
				}
				tocXmlDoc.Save(tocXml);
				bookXmlDoc.Save(bookXmlNoComments);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex.Message, ex);
			errorMessage = "Exception - " + ex.Message;
		}
		finally
		{
			StringBuilder results = new StringBuilder().AppendFormat("{0} toc.xml nodes updated, {1} toc.xml nodes, {2} book.xml nodes", tocXmlNodesUpdated, tocXmlNodesCount, bookXmlNodesCount).AppendLine().AppendFormat("<a href=\"{0}\">{0}</a>", _r2UtilitiesSettings.ResourceValidationBaseUrl + isbn)
				.AppendLine();
			if (!string.IsNullOrEmpty(errorMessage))
			{
				results.AppendLine(errorMessage);
			}
			foreach (string warningMessage in warningMessages)
			{
				results.AppendLine(warningMessage);
			}
			step.Results = results.ToString();
			step.HasWarnings = warningMessages.Count > 0;
			step.CompletedSuccessfully = string.IsNullOrEmpty(errorMessage);
			step.EndTime = DateTime.Now;
		}
		return step;
	}
}
