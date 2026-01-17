using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using R2Library.Data.ADO.R2Utility;
using R2Utilities.Tasks.ContentTasks.AhfsDrugMonograph;

namespace R2Utilities.Tasks.ContentTasks;

public class AhfsDrugMonographLoaderTask : TaskBase
{
	private readonly StringBuilder _results = new StringBuilder();

	public AhfsDrugMonographLoaderTask()
		: base("AhfsDrugMonographLoaderTask", "-AhfsDrugMonographLoaderTask", "x12", TaskGroup.Deprecated, "Loads the AHFS Drug Information data", enabled: false)
	{
	}

	public override void Run()
	{
		base.TaskResult.Information = "This task will load the AHFS Drug Information";
		TaskResultStep step = new TaskResultStep
		{
			Name = "AhfsDrugMonographLoaderTask",
			StartTime = DateTime.Now
		};
		base.TaskResult.AddStep(step);
		try
		{
			UpdateTaskResult();
			Process();
			step.Results = _results.ToString();
			step.CompletedSuccessfully = true;
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

	private void Process()
	{
		string path = "D:\\ClientsNoBackup\\Rittenhouse\\AHFS\\ahfs_20130916_xml\\";
		DirectoryInfo dirInfo = new DirectoryInfo(path);
		R2UtilitiesBase.Log.DebugFormat("exists: {0}, sourcePath: {1}", dirInfo.Exists, path);
		if (!dirInfo.Exists)
		{
			_results.AppendFormat("source directory does not exist, '{0}'", path).AppendLine();
			return;
		}
		FileInfo[] files = dirInfo.GetFiles("a*.xml");
		if (files.Length == 0)
		{
			R2UtilitiesBase.Log.WarnFormat("source directory is empty, '{0}'", path);
			_results.AppendFormat("source directory is empty, '{0}'", path).AppendLine();
			return;
		}
		AhfsDrugDataService ahfsDrugDataService = new AhfsDrugDataService();
		int fileCount = 0;
		int drugCount = 0;
		List<AhfsDrug> drugs = new List<AhfsDrug>();
		FileInfo[] array = files;
		foreach (FileInfo fileInfo in array)
		{
			fileCount++;
			R2UtilitiesBase.Log.InfoFormat(">>> {0} of {1} - {2}", fileCount, files.Length, fileInfo.Name);
			XmlDocument xmlDocument = new XmlDocument();
			string xmlPath = fileInfo.FullName;
			xmlDocument.Load(xmlPath);
			AhfsDrug drug = ParseDrug(xmlDocument);
			if (drug != null)
			{
				drugs.Add(drug);
				R2UtilitiesBase.Log.Debug(drug.ToDebugString());
				drugCount++;
				R2UtilitiesBase.Log.InfoFormat("Drug Count: {0} of {1}", drugCount, fileCount);
				drug.XmlFileName = fileInfo.Name;
				ahfsDrugDataService.Insert(drug);
			}
		}
	}

	private AhfsDrug ParseDrug(XmlDocument xmlDocument)
	{
		string fullTitle = GetXmlNodeInnerText(xmlDocument, "//dif/ahfs/ahfs-mono/intro-info/full-title");
		R2UtilitiesBase.Log.Info(fullTitle);
		if (!DoesFileContainDrugInfo(xmlDocument))
		{
			R2UtilitiesBase.Log.InfoFormat("Not a drug! - {0}", fullTitle);
			return null;
		}
		AhfsDrug drug = new AhfsDrug
		{
			UnitNumber = GetXmlNodeInnerText(xmlDocument, "//dif/ahfs/ahfs-mono/unit-num"),
			FullTitle = fullTitle,
			ShortTitle = GetXmlNodeInnerText(xmlDocument, "//dif/ahfs/ahfs-mono/intro-info/short-title")
		};
		SetPrintClassData(xmlDocument, drug);
		SetPrintTitles(xmlDocument, drug);
		SetSynonyms(xmlDocument, drug);
		SetGenericNames(xmlDocument, drug);
		SetChecmicalName(xmlDocument, drug);
		SetIntroduction(xmlDocument, drug);
		return drug;
	}

	private bool DoesFileContainDrugInfo(XmlDocument xmlDocument)
	{
		XmlNodeList nodes = xmlDocument.SelectNodes("//dif/ahfs/ahfs-mono/intro-info/drug-name-info");
		if (nodes == null || nodes.Count == 0)
		{
			return false;
		}
		return true;
	}

	private void SetPrintClassData(XmlDocument xmlDocument, AhfsDrug drug)
	{
		XmlNodeList nodes = xmlDocument.SelectNodes("//dif/ahfs/ahfs-mono/intro-info/print-class/class-num");
		if (nodes != null && nodes.Count != 1)
		{
			R2UtilitiesBase.Log.WarnFormat("Multiple node found, //dif/ahfs/ahfs-mono/intro-info/print-class/class-num, {0}", nodes.Count);
		}
		XmlNode classNumMode = xmlDocument.SelectSingleNode("//dif/ahfs/ahfs-mono/intro-info/print-class/class-num");
		if (classNumMode != null)
		{
			if (classNumMode.Attributes == null)
			{
				R2UtilitiesBase.Log.WarnFormat("node.Attributes is null - {0}", classNumMode.Name);
				return;
			}
			XmlAttribute classCode = classNumMode.Attributes["class-code-ref"];
			if (classCode != null)
			{
				drug.ClassNumber = classCode.Value;
			}
			else
			{
				R2UtilitiesBase.Log.Warn("class-code-ref is null");
			}
			XmlAttribute classText = classNumMode.Attributes["class-text"];
			if (classText != null)
			{
				drug.ClassText = classText.Value;
			}
			else
			{
				R2UtilitiesBase.Log.Warn("class-text is null");
			}
		}
		else
		{
			R2UtilitiesBase.Log.Warn("//dif/ahfs/ahfs-mono/intro-info/print-class/class-num is NULL");
		}
	}

	private void SetSynonyms(XmlDocument xmlDocument, AhfsDrug drug)
	{
		XmlNodeList nodes = xmlDocument.SelectNodes("//dif/ahfs/ahfs-mono/intro-info/synonym");
		if (nodes == null || nodes.Count <= 0)
		{
			return;
		}
		foreach (XmlNode node in nodes)
		{
			if (node.Attributes == null)
			{
				R2UtilitiesBase.Log.WarnFormat("node.Attributes is null - {0}", node.InnerText);
				continue;
			}
			XmlAttribute suppress = node.Attributes["suppress-from-index"];
			if (suppress != null && suppress.Value == "suppress")
			{
				R2UtilitiesBase.Log.InfoFormat("synonym suppressed - {0}", node.InnerText);
			}
			else
			{
				drug.AddSynonym(node.InnerText);
			}
		}
	}

	private void SetPrintTitles(XmlDocument xmlDocument, AhfsDrug drug)
	{
		XmlNodeList nodes = xmlDocument.SelectNodes("//dif/ahfs/ahfs-mono/intro-info/print-title");
		if (nodes != null && nodes.Count > 0)
		{
			foreach (XmlNode node in nodes)
			{
				drug.AddPrintName(node.InnerText);
			}
			return;
		}
		R2UtilitiesBase.Log.Warn("Node not found - //dif/ahfs/ahfs-mono/intro-info/print-title");
	}

	private void SetGenericNames(XmlDocument xmlDocument, AhfsDrug drug)
	{
		XmlNodeList nodes = xmlDocument.SelectNodes("//dif/ahfs/ahfs-mono/intro-info/drug-name-info/gen-name");
		if (nodes != null && nodes.Count > 0)
		{
			foreach (XmlNode node in nodes)
			{
				drug.AddGenericName(node.InnerText);
			}
			return;
		}
		R2UtilitiesBase.Log.Warn("Node not found - //dif/ahfs/ahfs-mono/intro-info/drug-name-info/gen-name");
	}

	private void SetChecmicalName(XmlDocument xmlDocument, AhfsDrug drug)
	{
		XmlNodeList nodes = xmlDocument.SelectNodes("//dif/ahfs/ahfs-mono/intro-info/drug-name-info/chem-name");
		if (nodes == null || nodes.Count <= 0)
		{
			return;
		}
		foreach (XmlNode node in nodes)
		{
			drug.AddChecmicalName(node.InnerText);
		}
	}

	private void SetIntroduction(XmlDocument xmlDocument, AhfsDrug drug)
	{
		StringBuilder intruduction = new StringBuilder();
		XmlNodeList nodes = xmlDocument.SelectNodes("//dif/ahfs/ahfs-mono/intro-desc/para");
		if (nodes != null && nodes.Count > 0)
		{
			foreach (XmlNode node in nodes)
			{
				intruduction.Append(node.OuterXml);
			}
		}
		else
		{
			R2UtilitiesBase.Log.Warn("Node not found - //dif/ahfs/ahfs-mono/intro-desc/para");
		}
		drug.Introduction = intruduction.ToString();
	}

	private string GetXmlNodeInnerText(XmlDocument xmlDocument, string xpath)
	{
		XmlNodeList nodes = xmlDocument.SelectNodes(xpath);
		if (nodes == null || nodes.Count == 0)
		{
			R2UtilitiesBase.Log.WarnFormat("Node not found - {0}", xpath);
			return null;
		}
		if (nodes.Count == 1)
		{
			return nodes[0].InnerText;
		}
		R2UtilitiesBase.Log.WarnFormat("Multiple Nodes Found, {0}, for {1}", nodes.Count, xpath);
		foreach (XmlNode node in nodes)
		{
			R2UtilitiesBase.Log.InfoFormat("{0} = {1}", xpath, node.InnerText);
		}
		return nodes[0].InnerText;
	}
}
