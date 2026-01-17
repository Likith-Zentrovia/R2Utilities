using System;
using System.Data.SqlClient;
using System.Text;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class NewEditionResource : FactoryBase, IDataEntity
{
	public int Id { get; set; }

	public virtual string Title { get; set; }

	public virtual string Isbn { get; set; }

	public virtual string PublisherName { get; set; }

	public virtual DateTime? ReleaseDate { get; set; }

	public virtual decimal ResourcePrice { get; set; }

	public virtual int LicenseCount { get; set; }

	public virtual string PreviousIsbn { get; set; }

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Id = GetInt32Value(reader, "iResourceId", -1);
			Title = GetStringValue(reader, "vchResourceTitle");
			Isbn = GetStringValue(reader, "vchResourceISBN");
			PublisherName = GetStringValue(reader, "vchPublisherName");
			ReleaseDate = GetDateValueOrNull(reader, "dtRISReleaseDate");
			ResourcePrice = GetDecimalValue(reader, "decResourcePrice", 0m);
			LicenseCount = GetInt32Value(reader, "LicenseCount", 0);
			PreviousIsbn = GetStringValue(reader, "PreviousIsbn");
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}

	public string ToDebugString()
	{
		StringBuilder sb = new StringBuilder("NewEditionResource = [");
		sb.AppendFormat("Id: {0}", Id);
		sb.AppendFormat(", Title: {0}", Title);
		sb.AppendFormat(", Isbn: {0}", Isbn);
		sb.AppendFormat(", PublisherName: {0}", PublisherName);
		sb.AppendFormat(", ReleaseDate: {0}", (!ReleaseDate.HasValue) ? "null" : ReleaseDate.Value.ToShortDateString());
		sb.AppendFormat(", ResourcePrice: {0}", ResourcePrice);
		sb.AppendFormat(", LicenseCount: {0}", LicenseCount);
		sb.AppendFormat(", PreviousIsbn: {0}", PreviousIsbn);
		sb.Append("]");
		return sb.ToString();
	}

	public string ToEmailString()
	{
		StringBuilder sb = new StringBuilder("<div>NewEditionResource = [");
		sb.AppendFormat("Isbn: {0} ", Isbn);
		sb.AppendFormat("|| Title: {0} ", Title);
		sb.AppendFormat("|| Publisher: {0} ", PublisherName);
		sb.AppendFormat("|| Price: {0} ", ResourcePrice);
		sb.AppendFormat("|| LicenseCount: {0} ", LicenseCount);
		sb.AppendFormat("|| ReleaseDate: {0} ", (!ReleaseDate.HasValue) ? "null" : ReleaseDate.Value.ToShortDateString());
		sb.AppendFormat("|| PreviousIsbn: {0}", PreviousIsbn);
		sb.Append("]</div>");
		return sb.ToString();
	}

	public string ToEmailString(int counter)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendFormat("<div>{1}.{2}ISBN: {0}</div>", Isbn, counter, SmallSpacer());
		sb.AppendFormat("<div>{1}Title: {0}</div>", Title, LargeSpacer());
		sb.AppendFormat("<div>{1}Publisher Name: {0}</div>", PublisherName, LargeSpacer());
		sb.AppendFormat("<div>{1}Resource Price: {0}</div>", ResourcePrice, LargeSpacer());
		sb.AppendFormat("<div>{1}License Count: {0}</div>", LicenseCount, LargeSpacer());
		sb.AppendFormat("<div>{1}Release Date: {0}</div>", (!ReleaseDate.HasValue) ? "null" : ReleaseDate.Value.ToShortDateString(), LargeSpacer());
		sb.AppendFormat("<div>{1}Previous ISBN: {0}</div>", PreviousIsbn, LargeSpacer());
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
