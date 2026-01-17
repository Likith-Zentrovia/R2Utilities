using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourceCore : FactoryBase, IDataEntity
{
	public int Id { get; set; }

	public virtual string Title { get; set; }

	public virtual string SubTitle { get; set; }

	public virtual string Authors { get; set; }

	public virtual string PublisherName { get; set; }

	public virtual DateTime? ReleaseDate { get; set; }

	public virtual DateTime? PublicationDate { get; set; }

	public virtual short BrandonHillStatus { get; set; }

	public virtual string Isbn { get; set; }

	public virtual string Edition { get; set; }

	public virtual string CopyRight { get; set; }

	public virtual int PublisherId { get; set; }

	public virtual int StatusId { get; set; }

	public virtual short RecordStatus { get; set; }

	public virtual int DrugMonograph { get; set; }

	public virtual string Isbn10 { get; set; }

	public virtual string Isbn13 { get; set; }

	public virtual string EIsbn { get; set; }

	public virtual string SortTitle { get; set; }

	public virtual string AlphaKey { get; set; }

	public virtual IList<ResourceSpecialty> Specialties { get; set; }

	public virtual IList<ResourcePracticeArea> PracticeAreas { get; set; }

	public virtual IList<ResourcePublisher> AssociatedPublishers { get; set; }

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Id = GetInt32Value(reader, "iResourceId", -1);
			Title = GetStringValue(reader, "vchResourceTitle");
			SubTitle = GetStringValue(reader, "vchResourceSubTitle");
			Authors = GetStringValue(reader, "vchResourceAuthors");
			ReleaseDate = GetDateValueOrNull(reader, "dtRISReleaseDate");
			PublicationDate = GetDateValueOrNull(reader, "dtResourcePublicationDate");
			BrandonHillStatus = GetByteValue(reader, "tiBrandonHillStatus", 0);
			Isbn = GetStringValue(reader, "vchResourceISBN");
			Edition = GetStringValue(reader, "vchResourceEdition");
			CopyRight = GetStringValue(reader, "vchCopyRight");
			PublisherId = GetInt32Value(reader, "iPublisherId", -1);
			StatusId = GetInt32Value(reader, "iResourceStatusId", 0);
			RecordStatus = GetByteValue(reader, "tiRecordStatus", 0);
			DrugMonograph = GetInt32Value(reader, "tiDrugMonograph", 0);
			PublisherName = GetStringValue(reader, "vchPublisherName");
			Isbn10 = GetStringValue(reader, "vchIsbn10");
			Isbn13 = GetStringValue(reader, "vchIsbn13");
			EIsbn = GetStringValue(reader, "vchEIsbn");
			SortTitle = GetStringValue(reader, "vchResourceSortTitle");
			AlphaKey = GetStringValue(reader, "chrAlphaKey");
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}

	public static string GetQueryFields(string tablePrefix)
	{
		StringBuilder fields = new StringBuilder();
		fields.AppendFormat("{0}.iResourceId, {0}.vchResourceTitle, {0}.vchResourceSubTitle, {0}.vchResourceAuthors ", tablePrefix);
		fields.AppendFormat(", {0}.dtRISReleaseDate, {0}.dtResourcePublicationDate, {0}.tiBrandonHillStatus, {0}.tiBrandonHillStatus ", tablePrefix);
		fields.AppendFormat(", {0}.vchResourceISBN, {0}.vchResourceEdition, {0}.vchCopyRight, {0}.iPublisherId ", tablePrefix);
		fields.AppendFormat(", {0}.iResourceStatusId, {0}.tiRecordStatus, {0}.tiDrugMonograph, {0}.vchPublisherName ", tablePrefix);
		fields.AppendFormat(", {0}.vchIsbn10, {0}.vchIsbn13, {0}.vchEIsbn, {0}.vchResourceSortTitle, {0}.chrAlphaKey ", tablePrefix);
		return fields.ToString();
	}

	public string ToDebugString()
	{
		StringBuilder sb = new StringBuilder("ResourceCore = [");
		sb.AppendFormat("Id: {0}", Id);
		sb.AppendFormat(", Isbn: {0}", Isbn);
		sb.AppendFormat(", Title: {0}", Title);
		sb.AppendFormat(", SubTitle: {0}", SubTitle);
		sb.AppendFormat(", Authors: {0}", Authors);
		sb.AppendFormat(", PublisherName: {0}", PublisherName);
		sb.AppendFormat(", ReleaseDate: {0}", (!ReleaseDate.HasValue) ? "null" : ReleaseDate.Value.ToShortDateString());
		sb.AppendFormat(", PublicationDate: {0}", (!PublicationDate.HasValue) ? "null" : PublicationDate.Value.ToShortDateString());
		sb.AppendFormat(", BrandonHillStatus: {0}", BrandonHillStatus);
		sb.AppendFormat(", Isbn: {0}", Isbn);
		sb.AppendFormat(", Edition: {0}", Edition);
		sb.AppendFormat(", CopyRight: {0}", CopyRight);
		sb.AppendFormat(", PublisherId: {0}", PublisherId);
		sb.AppendFormat(", StatusId: {0}", StatusId);
		sb.AppendFormat(", RecordStatus: {0}", RecordStatus);
		sb.AppendFormat(", DrugMonograph: {0}", DrugMonograph);
		sb.AppendFormat(", Isbn10: {0}", Isbn10);
		sb.AppendFormat(", Isbn13: {0}", Isbn13);
		sb.AppendFormat(", EIsbn: {0}", EIsbn);
		sb.AppendFormat(", SortTitle: {0}", SortTitle);
		sb.AppendFormat(", AlphaKey: {0}", AlphaKey);
		sb.AppendLine().Append("\tSpecialties = ");
		if (Specialties != null)
		{
			foreach (ResourceSpecialty resourceSpecialty in Specialties)
			{
				sb.AppendFormat("[Id: {0}, Name: {1}], ", resourceSpecialty.Id, resourceSpecialty.Name);
			}
			sb.AppendLine();
		}
		else
		{
			sb.AppendLine("null, ");
		}
		sb.Append("\tPracticeAreas = ");
		if (PracticeAreas != null)
		{
			foreach (ResourcePracticeArea resourcePracticeArea in PracticeAreas)
			{
				sb.AppendFormat("[Id: {0}, Name: {1}], ", resourcePracticeArea.Id, resourcePracticeArea.Name);
			}
			sb.AppendLine();
		}
		else
		{
			sb.AppendLine("null, ");
		}
		sb.Append("]");
		return sb.ToString();
	}
}
