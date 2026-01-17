using System;
using System.Data.SqlClient;
using System.Reflection;
using Common.Logging;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ResourceFile : FactoryBase, IDataEntity
{
	protected new static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);

	public int Id { get; set; }

	public int ResourceId { get; set; }

	public string FilenameFull { get; set; }

	public string FilenamePart1 { get; set; }

	public string FilenamePart3 { get; set; }

	public string Isbn { get; set; }

	public int DocumentId { get; set; }

	public string CreatedBy { get; set; }

	public DateTime CreationDate { get; set; }

	public string UpdatedBy { get; set; }

	public DateTime? LastUpdated { get; set; }

	public int StatusId { get; set; }

	public ResourceFile()
	{
	}

	public ResourceFile(int documentId, string filenameFull)
	{
		Populate(documentId, filenameFull, 0);
	}

	public ResourceFile(int documentId, string filenameFull, int resourceId)
	{
		Populate(documentId, filenameFull, resourceId);
	}

	public void Populate(SqlDataReader reader)
	{
		try
		{
			Id = GetInt32Value(reader, "iResourceFileId", -1);
			ResourceId = GetInt32Value(reader, "iResourceId", -1);
			FilenameFull = GetStringValue(reader, "vchFileNameFull");
			FilenamePart1 = GetStringValue(reader, "vchFileNamePart1");
			FilenamePart3 = GetStringValue(reader, "vchFileNamePart3");
			DocumentId = GetInt32Value(reader, "iDocumentId", -1);
			CreatedBy = GetStringValue(reader, "vchCreatorId");
			UpdatedBy = GetStringValue(reader, "vchUpdaterId");
			CreationDate = GetDateValue(reader, "dtCreationDate");
			LastUpdated = GetDateValueOrNull(reader, "dtLastUpdate");
			StatusId = GetInt32Value(reader, "tiRecordStatus", -1);
		}
		catch (Exception ex)
		{
			Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}

	private void Populate(int documentId, string filenameFull, int resourceId)
	{
		FilenameFull = filenameFull;
		string[] parts = filenameFull.Split('.');
		DocumentId = documentId;
		FilenamePart1 = parts[0];
		Isbn = parts[1];
		FilenamePart3 = ((parts.Length > 3) ? parts[2] : null);
		ResourceId = resourceId;
		CreatedBy = "R2Utilities";
		CreationDate = DateTime.Now;
		StatusId = 1;
	}
}
