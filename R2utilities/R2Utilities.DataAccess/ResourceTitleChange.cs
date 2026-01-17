using System;
using System.Data.SqlClient;
using System.Text;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourceTitleChange : FactoryBase, IDataEntity
{
	public int ResourceId { get; set; }

	public string Isbn { get; set; }

	public string Isbn13 { get; set; }

	public string Title { get; set; }

	public string SubTitle { get; set; }

	public string RittenhouseTitle { get; set; }

	public string RittenhouseSubTitle { get; set; }

	public string AlternateTitle { get; set; }

	public string AlternateSubTitle { get; set; }

	public bool IsRevert { get; set; }

	public ResourceTitleUpdateType UpdateType { get; set; }

	public void Populate(SqlDataReader reader)
	{
		ResourceId = GetInt32Value(reader, "iResourceId", 0);
		Isbn = GetStringValue(reader, "vchResourceIsbn");
		Isbn13 = GetStringValue(reader, "vchIsbn13");
		Title = GetStringValue(reader, "vchResourceTitle");
		SubTitle = GetStringValue(reader, "vchResourceSubTitle");
		RittenhouseTitle = GetStringValue(reader, "title");
		RittenhouseSubTitle = GetStringValue(reader, "subtitle");
		if (AreEqual(Title, RittenhouseTitle) && (AreEqual(SubTitle, RittenhouseSubTitle) || (string.IsNullOrWhiteSpace(SubTitle) && string.IsNullOrWhiteSpace(RittenhouseSubTitle))))
		{
			UpdateType = ResourceTitleUpdateType.Equal;
		}
		else if (!AreEqual(Title, RittenhouseTitle) && AreEqual(RittenhouseTitle, Combine(Title, SubTitle)) && !string.IsNullOrWhiteSpace(SubTitle))
		{
			UpdateType = ResourceTitleUpdateType.RittenhouseEqualR2TitleAndSub;
		}
		else if (AreEqual(Title, Combine(RittenhouseTitle, RittenhouseSubTitle)) && !string.IsNullOrWhiteSpace(RittenhouseSubTitle) && string.IsNullOrWhiteSpace(SubTitle))
		{
			UpdateType = ResourceTitleUpdateType.R2EqualRittenhouseTitleAndSub;
		}
		else if (string.IsNullOrWhiteSpace(RittenhouseTitle))
		{
			UpdateType = ResourceTitleUpdateType.NotExist;
		}
		else if (AreEqual(Title, RittenhouseTitle) && !AreEqual(SubTitle, RittenhouseSubTitle) && !string.IsNullOrWhiteSpace(SubTitle) && !string.IsNullOrWhiteSpace(RittenhouseSubTitle))
		{
			UpdateType = ResourceTitleUpdateType.DifferentSub;
		}
		else if (AreEqual(Title, RittenhouseTitle) && !AreEqual(SubTitle, RittenhouseSubTitle) && string.IsNullOrWhiteSpace(RittenhouseSubTitle))
		{
			UpdateType = ResourceTitleUpdateType.RittenhouseSubNull;
		}
		else if (AreEqual(Title, RittenhouseTitle) && !AreEqual(SubTitle, RittenhouseSubTitle) && string.IsNullOrWhiteSpace(SubTitle))
		{
			UpdateType = ResourceTitleUpdateType.R2SubNull;
		}
		else
		{
			UpdateType = ResourceTitleUpdateType.Other;
		}
	}

	public string GetNewTitle()
	{
		return string.IsNullOrWhiteSpace(AlternateTitle) ? RittenhouseTitle : AlternateTitle;
	}

	public string GetNewSubTitle()
	{
		return string.IsNullOrWhiteSpace(AlternateSubTitle) ? SubTitle : AlternateSubTitle;
	}

	private bool AreEqual(string value1, string value2)
	{
		return string.Equals(value1, value2, StringComparison.CurrentCultureIgnoreCase);
	}

	private string Combine(string title, string subtitle)
	{
		return title + ": " + subtitle;
	}

	public string ToDebugString()
	{
		return new StringBuilder().AppendFormat("RittenhouseResourceTitle = [ResourceId: {0}", ResourceId).AppendFormat(", Isbn: {0}", Isbn).AppendFormat(", Isbn13: {0}", Isbn13)
			.AppendFormat(", Title: {0}", Title)
			.AppendFormat(", SubTitle: {0}", SubTitle)
			.AppendFormat(", RittenhouseTitle: {0}", RittenhouseTitle)
			.AppendFormat(", RittenhouseSubTitle: {0}", RittenhouseSubTitle)
			.AppendFormat(", AlternateTitle (supplied title): {0}", AlternateTitle)
			.AppendFormat(", Type: {0}", UpdateType)
			.Append("]")
			.ToString();
	}
}
