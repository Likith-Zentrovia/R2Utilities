using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Common.Logging;

namespace R2Utilities.Utilities;

public class XmlHelper
{
	protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);

	public static string GetXmlNodeValue(XmlDocument xmlDoc, string xpath)
	{
		if (xmlDoc.DocumentElement != null)
		{
			XmlNodeList xmlNodeList = xmlDoc.DocumentElement.SelectNodes(xpath);
			if (xmlNodeList == null)
			{
				return string.Empty;
			}
			if (xmlNodeList.Count == 0)
			{
				return string.Empty;
			}
			if (xmlNodeList.Count > 1)
			{
				return string.Empty;
			}
			string value = xmlNodeList[0].InnerText;
			return value.Replace("\r", "").Replace("\n", "").Replace("\t", "");
		}
		return "";
	}

	public static string GetXmlNodeValue(XmlNode xmlNode)
	{
		if (xmlNode == null)
		{
			return "";
		}
		string value = xmlNode.InnerText;
		return value.Replace("\r", "").Replace("\n", "").Replace("\t", "");
	}

	public static XmlNode GetXmlNode(XmlDocument xmlDoc, string xpath)
	{
		if (xmlDoc.DocumentElement != null)
		{
			XmlNodeList xmlNodeList = xmlDoc.DocumentElement.SelectNodes(xpath);
			if (xmlNodeList == null)
			{
				return null;
			}
			if (xmlNodeList.Count == 0)
			{
				return null;
			}
			if (xmlNodeList.Count > 1)
			{
				return null;
			}
			return xmlNodeList[0];
		}
		return null;
	}

	public static List<XmlNode> GetXmlNodes(XmlDocument xmlDoc, string xpath)
	{
		List<XmlNode> nodes = new List<XmlNode>();
		if (xmlDoc.DocumentElement != null)
		{
			XmlNodeList xmlNodeList = xmlDoc.DocumentElement.SelectNodes(xpath);
			if (xmlNodeList != null)
			{
				nodes.AddRange(xmlNodeList.Cast<XmlNode>());
			}
		}
		return nodes;
	}

	public static void AppendXmlNode(XmlDocument xmlDoc, XmlNode parentNode, string nodeName, string nodeValue)
	{
		if (!string.IsNullOrEmpty(nodeValue))
		{
			XmlNode childNode = xmlDoc.CreateNode(XmlNodeType.Element, nodeName, null);
			childNode.InnerText = nodeValue;
			parentNode.AppendChild(childNode);
		}
	}

	public static string GetAttributeValue(XmlNode node, string name)
	{
		if (node.Attributes == null)
		{
			return string.Empty;
		}
		XmlAttribute attribute = node.Attributes[name];
		if (attribute == null)
		{
			return string.Empty;
		}
		return attribute.Value;
	}

	public static XmlDocument StripTags(XmlDocument xmlDoc, string xpathToStrip)
	{
		XmlNodeList xmlNodeList = xmlDoc.SelectNodes(xpathToStrip);
		if (xmlNodeList == null)
		{
			return xmlDoc;
		}
		List<XmlNode> listXmlNode = (from XmlNode n in xmlNodeList
			where !n.IsReadOnly
			orderby n.InnerXml.Length
			select n).ToList();
		foreach (XmlNode xmlNode in listXmlNode)
		{
			XmlDocumentFragment xmlFragment = xmlDoc.CreateDocumentFragment();
			xmlFragment.InnerXml = xmlNode.InnerXml;
			if (xmlNode.ParentNode == null)
			{
				xmlDoc.ReplaceChild(xmlFragment, xmlNode);
			}
			else
			{
				xmlNode.ParentNode.ReplaceChild(xmlFragment, xmlNode);
			}
		}
		return xmlDoc;
	}

	public static XmlDocument RemoveComments(XmlDocument xmlDoc)
	{
		XmlNodeList xmlNodeList = xmlDoc.SelectNodes("//comment()");
		if (xmlNodeList == null)
		{
			return xmlDoc;
		}
		foreach (XmlNode xmlNode in xmlNodeList)
		{
			if (xmlNode.ParentNode == null)
			{
				xmlDoc.RemoveChild(xmlNode);
			}
			else
			{
				xmlNode.ParentNode.RemoveChild(xmlNode);
			}
		}
		return xmlDoc;
	}
}
