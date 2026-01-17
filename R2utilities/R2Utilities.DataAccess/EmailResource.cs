using System;
using System.Data.SqlClient;
using System.Text;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class EmailResource : FactoryBase, IDataEntity
{
	public int ResourceId { get; set; }

	public string Isbn { get; set; }

	public string Title { get; set; }

	public string PublisherName { get; set; }

	public decimal Price { get; set; }

	public int LicenseCount { get; set; }

	public DateTime? ReleaseDate { get; set; }

	public string Edition { get; set; }

	public string NewIsbn { get; set; }

	public string NewTitle { get; set; }

	public string NewEdition { get; set; }

	public void Populate(SqlDataReader reader)
	{
		Isbn = GetStringValue(reader, "vchResourceIsbn");
		Title = GetStringValue(reader, "vchResourceTitle");
		PublisherName = GetStringValue(reader, "vchPublisherName");
		Price = GetDecimalValue(reader, "decResourcePrice", 0m);
		LicenseCount = GetInt32Value(reader, "LicenseCount", 0);
		ReleaseDate = GetDateValueOrNull(reader, "dtRISReleaseDate");
		ResourceId = GetInt32Value(reader, "iResourceId", 0);
		Edition = GetStringValue(reader, "vchResourceEdition");
		NewIsbn = GetStringValue(reader, "NewIsbn");
		NewTitle = GetStringValue(reader, "NewTitle");
		NewEdition = GetStringValue(reader, "NewEdition");
	}

	public string ToEmailString(int counter)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendFormat("<div>{1}.{2}ISBN: {0}</div>", Isbn, counter, SmallSpacer());
		sb.AppendFormat("<div>{1}Title: {0}</div>", Title, LargeSpacer());
		sb.AppendFormat("<div>{1}Publisher Name: {0}</div>", PublisherName, LargeSpacer());
		sb.AppendFormat("<div>{1}Resource Price: {0}</div>", Price, LargeSpacer());
		sb.AppendFormat("<div>{1}License Count: {0}</div>", LicenseCount, LargeSpacer());
		sb.AppendFormat("<div>{1}Release Date: {0}</div>", (!ReleaseDate.HasValue) ? "null" : ReleaseDate.Value.ToShortDateString(), LargeSpacer());
		sb.AppendFormat("<div>{1}Edition: {0}</div>", Edition, LargeSpacer());
		sb.Append("<div>&nbsp;</div>");
		sb.AppendFormat("<div>{1}Latest ISBN: {0}</div>", NewIsbn, LargeSpacer());
		sb.AppendFormat("<div>{1}Latest Title : {0}</div>", NewTitle, LargeSpacer());
		sb.AppendFormat("<div>{1}Latest Edition: {0}</div>", NewEdition, LargeSpacer());
		sb.Append("<div>&nbsp;</div>");
		return sb.ToString();
	}

	private static string SmallSpacer()
	{
		return "<span>&nbsp;&nbsp;&nbsp;&nbsp;</span>";
	}

	private static string LargeSpacer()
	{
		return "<span>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span>";
	}
}
