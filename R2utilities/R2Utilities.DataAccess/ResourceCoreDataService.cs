using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;
using R2Utilities.Tasks.ContentTasks.BookInfo;
using R2V2.Core.Resource;

namespace R2Utilities.DataAccess;

public class ResourceCoreDataService : DataServiceBase
{
	private const string UpdateNewEdtitions = "\n            update r\n            set    r.iNewEditResourceId = new.iResourceId, r.vchUpdaterId = 'UpdateNewEdtitions', r.dtLastUpdate = getdate()\n            from  tResource r\n             join [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n             join tResource new on pr.newAvailEd = new.vchResourceISBN\n            where (r.iNewEditResourceId is null or r.iNewEditResourceId <> new.iResourceId) and\n                  (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72) ";

	private const string UpdatePreviousEditions = "\n            update r\n            set    r.iPrevEditResourceID = prev.iResourceId, r.vchUpdaterId = 'UpdatePreviousEditions', r.dtLastUpdate = getdate()\n            from   tResource r\n             join [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n             join tResource prev on pr.previousEd = prev.vchResourceISBN\n            where (r.iPrevEditResourceID is null or r.iPrevEditResourceID <> prev.iResourceId) and\n                  (r.tiRecordStatus = 1 and prev.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and prev.iResourceStatusId <> 72) ";

	private const string ClearInvalidLatestEditions = "\n            update r\n            set    r.iLatestEditResourceId = null, r.vchUpdaterId = 'LastestEditionCleanUp', r.dtLastUpdate = getdate()\n            from   tresource r\n            where  (iNewEditResourceId = 0 or iNewEditResourceId is null) and r.iLatestEditResourceId is not null ";

	private const string ClearInvalidPreviousEditions = "\n            update r\n            set    r.iPrevEditResourceID = null, r.vchUpdaterId = 'LastestEditionCleanUp', r.dtLastUpdate = getdate()\n            from   tresource r\n            where  iPrevEditResourceID = 0 ";

	private const string GetNewEditions = "\n            select iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate, sum(LicenseCount) as LicenseCount, PreviousIsbn\n            from (\n                select new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice,\n                       new.dtRISReleaseDate, ISNULL(sum(irl.iLicenseCount), '0') as LicenseCount, r.vchResourceISBN as PreviousIsbn\n                from   tResource r\n                 join  [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n                 join  tResource new on pr.newAvailEd = new.vchResourceISBN\n                 join  tPublisher p on r.iPublisherId = p.iPublisherId\n                 join  tInstitutionResourceLicense irl on r.iResourceId = irl.iResourceId\n                where  r.iNewEditResourceId <> new.iResourceId\n                  and (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72)\n                group by new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice, new.dtRISReleaseDate, r.vchResourceISBN\n                union\n                select new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice,\n                       new.dtRISReleaseDate, 0 as LicenseCount, r.vchResourceISBN as PreviousIsbn\n                from   tResource r\n                 join  [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n                 join  tResource new on pr.newAvailEd = new.vchResourceISBN\n                 join  tPublisher p on r.iPublisherId = p.iPublisherId\n                 join  tInstitutionResourceLicense irl on r.iResourceId = irl.iResourceId\n                where  r.iNewEditResourceId <> new.iResourceId\n                  and  (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72)\n                group by new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice, new.dtRISReleaseDate, r.vchResourceISBN\n            ) as test\n            group by iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate, PreviousIsbn ";

	private const string ResourceSelect = "select r.iResourceId, r.vchResourceTitle, r.vchResourceSubTitle, r.vchResourceAuthors, r.dtRISReleaseDate, r.dtResourcePublicationDate, r.tiBrandonHillStatus\n                   , r.vchResourceISBN, r.vchResourceEdition, r.vchCopyRight, r.iPublisherId, r.iResourceStatusId, r.tiRecordStatus, r.tiDrugMonograph, r.vchResourceSortTitle\n                   , r.chrAlphaKey, r.vchIsbn10, r.vchIsbn13, r.vchEIsbn, p.vchPublisherName\n              from   dbo.tResource r\n               join  dbo.tPublisher p on p.iPublisherId = r.iPublisherId\n             ";

	private static readonly string GetAuthorInsert = new StringBuilder().Append("insert into {0} (iResourceId, vchFirstName, vchLastName, vchMiddleName, vchLineage, vchDegree, tiAuthorOrder) ").Append("values(@ResourceId, @FirstName, @LastName, @MiddleName, @Lineage, @Degree, @AuthorOrder); ").ToString();

	private static readonly string UpdateAutoArchive = new StringBuilder().Append("update tResource ").Append("set    iResourceStatusId = 7 ").Append("     , vchUpdaterId = 'AutoArchive' ")
		.Append("     , dtLastUpdate = '{1}' ")
		.Append("     , dtArchiveDate = '{1}' ")
		.Append("where iResourceId in ( ")
		.Append("   select r.iResourceId ")
		.Append("   from   tResource r ")
		.Append("   left join  [{0}].RittenhouseWeb.dbo.Product p on p.isbn10 = r.vchIsbn10 and p.productStatusId = 3 ")
		.Append("   left join  [{0}].RittenhouseWeb.dbo.Product p2 on p2.isbn13 = r.vchIsbn13 and p2.productStatusId = 3 ")
		.Append("   where  r.iResourceStatusId = 6 and (p.sku is not null or p2.sku is not null) and r.tiExcludeFromAutoArchive = 0 ")
		.Append("    ) ")
		.ToString();

	private static readonly string GetAutoArchives = new StringBuilder().Append("select iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate, sum(LicenseCount) as LicenseCount ").Append("from (Select r.iResourceId, r.vchResourceISBN, r.vchResourceTitle, p.vchPublisherName, r.decResourcePrice, ").Append("       r.dtRISReleaseDate, ISNULL(sum(irl.iLicenseCount), '0') as LicenseCount ")
		.Append("   from tResource r ")
		.Append("   join tPublisher p on r.iPublisherId = p.iPublisherId ")
		.Append("   join tInstitutionResourceLicense irl on r.iResourceId = irl.iResourceId ")
		.Append("   join tInstitution i on irl.iInstitutionId = i.iInstitutionId and i.tiHouseAcct = 0 ")
		.Append("   where r.iResourceId in ( ")
		.Append("       select r.iResourceId ")
		.Append("          from   tResource r ")
		.Append("          left join  [{0}].RittenhouseWeb.dbo.Product p on p.isbn10 = r.vchIsbn10 and p.productStatusId = 3 ")
		.Append("          left join  [{0}].RittenhouseWeb.dbo.Product p2 on p2.isbn13 = r.vchIsbn13 and p2.productStatusId = 3 ")
		.Append("          where  r.iResourceStatusId = 6 and (p.sku is not null or p2.sku is not null) and r.tiExcludeFromAutoArchive = 0) ")
		.Append("group by r.iResourceId, r.vchResourceISBN, r.vchResourceTitle, p.vchPublisherName, r.decResourcePrice, r.dtRISReleaseDate ")
		.Append("union ")
		.Append("Select r.iResourceId, r.vchResourceISBN, r.vchResourceTitle, p.vchPublisherName, r.decResourcePrice, ")
		.Append("       r.dtRISReleaseDate, '0' as LicenseCount ")
		.Append("   from tResource r ")
		.Append("   join tPublisher p on r.iPublisherId = p.iPublisherId ")
		.Append("   where r.iResourceId in ( ")
		.Append("       select r.iResourceId ")
		.Append("          from   tResource r ")
		.Append("          left join  [{0}].RittenhouseWeb.dbo.Product p on p.isbn10 = r.vchIsbn10 and p.productStatusId = 3 ")
		.Append("          left join  [{0}].RittenhouseWeb.dbo.Product p2 on p2.isbn13 = r.vchIsbn13 and p2.productStatusId = 3 ")
		.Append("          where  r.iResourceStatusId = 6 and (p.sku is not null or p2.sku is not null) and r.tiExcludeFromAutoArchive = 0) ")
		.Append("group by r.iResourceId, r.vchResourceISBN, r.vchResourceTitle, p.vchPublisherName, r.decResourcePrice, r.dtRISReleaseDate ")
		.Append(") as test ")
		.Append("group by iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate ")
		.Append("order by LicenseCount ")
		.ToString();

	private static readonly string UpdateResourceAffiliationSql = new StringBuilder().Append(" UPDATE tResource ").Append(" SET    vchAffiliation = p.affiliation, tiAffiliationUpdatedByPrelude = 1, vchUpdaterId = 'AffiliationUpdate', dtLastUpdate = getdate()  ").Append(" FROM   tResource r ")
		.Append(" INNER JOIN [{0}].RittenhouseWeb.dbo.Product p on r.vchIsbn10 = p.isbn10 ")
		.Append(" WHERE  r.vchAffiliation <> p.affiliation")
		.Append(" or (r.vchAffiliation is null and p.affiliation is not null)")
		.ToString();

	public ResourceCore GetResourceByIsbn(string isbn, bool excludeForthcoming)
	{
		StringBuilder sql = new StringBuilder("select r.iResourceId, r.vchResourceTitle, r.vchResourceSubTitle, r.vchResourceAuthors, r.dtRISReleaseDate, r.dtResourcePublicationDate, r.tiBrandonHillStatus\n                   , r.vchResourceISBN, r.vchResourceEdition, r.vchCopyRight, r.iPublisherId, r.iResourceStatusId, r.tiRecordStatus, r.tiDrugMonograph, r.vchResourceSortTitle\n                   , r.chrAlphaKey, r.vchIsbn10, r.vchIsbn13, r.vchEIsbn, p.vchPublisherName\n              from   dbo.tResource r\n               join  dbo.tPublisher p on p.iPublisherId = r.iPublisherId\n             ").Append("where  r.vchResourceISBN = @Isbn ").AppendFormat("  and   r.tiRecordStatus = 1 and r.iResourceStatusId in (6,7{0}) ", excludeForthcoming ? "" : ",8").Append("order by r.iResourceId desc ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("Isbn", isbn)
		};
		ResourceCore resource = GetFirstEntity<ResourceCore>(sql.ToString(), parameters, logSql: true);
		ResourcePracticeAreaDataService resourcePracticeAreaDataService = new ResourcePracticeAreaDataService();
		resource.PracticeAreas = resourcePracticeAreaDataService.GetResourcePracticeArea(resource.Id);
		ResourceSpecialtyDataService resourceSpecialtyDataService = new ResourceSpecialtyDataService();
		resource.Specialties = resourceSpecialtyDataService.GetResourceSpecialty(resource.Id);
		ResourcePublisherDataService resourcePublisherDataService = new ResourcePublisherDataService();
		IList<ResourcePublisher> associatedPublishers = resourcePublisherDataService.GetResourcePubslihers(resource.Id);
		if (associatedPublishers != null)
		{
			resource.PublisherName = associatedPublishers.FirstOrDefault((ResourcePublisher x) => !x.ParentPublisherId.HasValue)?.PublisherName;
			IEnumerable<ResourcePublisher> associatedPublishersFound = associatedPublishers.Where((ResourcePublisher x) => x.ParentPublisherId.HasValue);
			ResourcePublisher[] resourcePublishers = (associatedPublishersFound as ResourcePublisher[]) ?? associatedPublishersFound.ToArray();
			resource.AssociatedPublishers = (resourcePublishers.Any() ? resourcePublishers.ToList() : null);
		}
		return resource;
	}

	public IList<ResourceCore> GetResourcesAll(bool orderByDescending)
	{
		StringBuilder sql = new StringBuilder("select r.iResourceId, r.vchResourceTitle, r.vchResourceSubTitle, r.vchResourceAuthors, r.dtRISReleaseDate, r.dtResourcePublicationDate, r.tiBrandonHillStatus\n                   , r.vchResourceISBN, r.vchResourceEdition, r.vchCopyRight, r.iPublisherId, r.iResourceStatusId, r.tiRecordStatus, r.tiDrugMonograph, r.vchResourceSortTitle\n                   , r.chrAlphaKey, r.vchIsbn10, r.vchIsbn13, r.vchEIsbn, p.vchPublisherName\n              from   dbo.tResource r\n               join  dbo.tPublisher p on p.iPublisherId = r.iPublisherId\n             ").AppendFormat("order by r.iResourceId {0};", orderByDescending ? "desc" : "");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		return GetEntityList<ResourceCore>(sql.ToString(), parameters, logSql: true);
	}

	public IList<ResourceCore> GetResources(int minResourceId, int maxResourceId, int maxResourceCount, bool orderByDescending)
	{
		StringBuilder sql = new StringBuilder("select r.iResourceId, r.vchResourceTitle, r.vchResourceSubTitle, r.vchResourceAuthors, r.dtRISReleaseDate, r.dtResourcePublicationDate, r.tiBrandonHillStatus\n                   , r.vchResourceISBN, r.vchResourceEdition, r.vchCopyRight, r.iPublisherId, r.iResourceStatusId, r.tiRecordStatus, r.tiDrugMonograph, r.vchResourceSortTitle\n                   , r.chrAlphaKey, r.vchIsbn10, r.vchIsbn13, r.vchEIsbn, p.vchPublisherName\n              from   dbo.tResource r\n               join  dbo.tPublisher p on p.iPublisherId = r.iPublisherId\n             ".Replace("select r.iResourceId", $"select top {maxResourceCount} r.iResourceId")).Append("where r.iResourceId >= @MinResourceId and r.iResourceId <= @MaxResourceId ").AppendFormat("order by r.iResourceId {0};", orderByDescending ? "desc" : "");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MinResourceId", minResourceId),
			new Int32Parameter("MaxResourceId", maxResourceId)
		};
		return GetEntityList<ResourceCore>(sql.ToString(), parameters, logSql: true);
	}

	public IList<ResourceCore> GetResourcesByIsbns(string[] isbns, bool orderByDescending)
	{
		StringBuilder sql = new StringBuilder("select r.iResourceId, r.vchResourceTitle, r.vchResourceSubTitle, r.vchResourceAuthors, r.dtRISReleaseDate, r.dtResourcePublicationDate, r.tiBrandonHillStatus\n                   , r.vchResourceISBN, r.vchResourceEdition, r.vchCopyRight, r.iPublisherId, r.iResourceStatusId, r.tiRecordStatus, r.tiDrugMonograph, r.vchResourceSortTitle\n                   , r.chrAlphaKey, r.vchIsbn10, r.vchIsbn13, r.vchEIsbn, p.vchPublisherName\n              from   dbo.tResource r\n               join  dbo.tPublisher p on p.iPublisherId = r.iPublisherId\n             ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		for (int i = 0; i < isbns.Length; i++)
		{
			string paramName = $"ISBN_{i}";
			sql.AppendFormat("{0} r.vchResourceISBN = @{1} ", (i == 0) ? "where" : "or", paramName);
			parameters.Add(new StringParameter(paramName, isbns[i]));
		}
		return GetEntityList<ResourceCore>(sql.ToString(), parameters, logSql: true);
	}

	public List<ResourceCore> GetActiveAndArchivedResources(bool orderByDescending, int minResourceId, int maxResourceId, int maxRecords, string[] isbns)
	{
		StringBuilder sql = new StringBuilder().Append("select r.iResourceId, r.vchResourceTitle, r.vchResourceSubTitle, r.vchResourceAuthors, r.dtRISReleaseDate, r.dtResourcePublicationDate, r.tiBrandonHillStatus\n                   , r.vchResourceISBN, r.vchResourceEdition, r.vchCopyRight, r.iPublisherId, r.iResourceStatusId, r.tiRecordStatus, r.tiDrugMonograph, r.vchResourceSortTitle\n                   , r.chrAlphaKey, r.vchIsbn10, r.vchIsbn13, r.vchEIsbn, p.vchPublisherName\n              from   dbo.tResource r\n               join  dbo.tPublisher p on p.iPublisherId = r.iPublisherId\n             ".Replace("select r.iResourceId,", $"select top {maxRecords} r.iResourceId,")).Append("where  r.iResourceStatusId in (6,7) and r.tiRecordStatus = 1 ").Append("  and  r.iResourceId between @MinResourceId and @MaxResourceId ");
		if (isbns != null && isbns.Length != 0)
		{
			sql.Append("  and  r.vchResourceISBN in (");
			for (int i = 0; i < isbns.Length; i++)
			{
				sql.AppendFormat("{0}'{1}'", (i == 0) ? string.Empty : ",", isbns[i]);
			}
			sql.Append(") ");
		}
		sql.AppendFormat("order by r.iResourceId {0};", orderByDescending ? "desc" : "");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MinResourceId", minResourceId),
			new Int32Parameter("MaxResourceId", maxResourceId)
		};
		return GetEntityList<ResourceCore>(sql.ToString(), parameters, logSql: true);
	}

	public int InsertAuthor(int resourceId, int order, Author author, string tableName)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId),
			new Int32Parameter("AuthorOrder", order),
			new StringParameter("FirstName", author.FirstName),
			new StringParameter("LastName", author.LastName),
			new StringParameter("MiddleName", author.MiddleInitial),
			new StringParameter("Lineage", author.Lineage),
			new StringParameter("Degree", author.Degrees)
		};
		string insert = string.Format(GetAuthorInsert, tableName);
		return ExecuteInsertStatementReturnIdentity(insert, parameters.ToArray(), logSql: false);
	}

	public int DeleteResourceAuthors(int resourceId, string tableName)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		string delete = "delete from " + tableName + " where iResourceId = @ResourceId ";
		int rowCount = ExecuteUpdateStatement(delete, parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("delete row count: {0}", rowCount);
		return rowCount;
	}

	public int UpdateNewResourceFields(int resourceId, string sortTitle, string alphaChar, string updateId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("SortTitle", sortTitle),
			new StringParameter("AlphaChar", alphaChar),
			new StringParameter("UpdateId", updateId),
			new Int32Parameter("ResourceId", resourceId)
		};
		StringBuilder update = new StringBuilder().Append("update tResource ").Append("set    vchResourceSortTitle = @SortTitle ").Append("     , chrAlphaKey = @AlphaChar ")
			.Append("     , vchIsbn10 = (select i.vchIsbn from tResourceIsbn i where i.iResourceId = @ResourceId and i.iResourceIsbnTypeId = 1) ")
			.Append("     , vchIsbn13 = (select i.vchIsbn from tResourceIsbn i where i.iResourceId = @ResourceId and i.iResourceIsbnTypeId = 2) ")
			.Append("     , vchEIsbn = (select i.vchIsbn from tResourceIsbn i where i.iResourceId = @ResourceId and i.iResourceIsbnTypeId = 3) ")
			.Append("     , vchUpdaterId = @UpdateId ")
			.Append("     , dtLastUpdate = getdate() ")
			.Append("     , vchResourceEdition = rtrim(ltrim(vchResourceEdition)) ")
			.Append("where iResourceId = @ResourceId ");
		int rowCount = ExecuteUpdateStatement(update.ToString(), parameters.ToArray(), logSql: true);
		FactoryBase.Log.DebugFormat("update row count: {0}", rowCount);
		return rowCount;
	}

	public int UpdateResourceSortAuthor(int resourceId, string sortAuthor, string updateId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("SortAuthor", sortAuthor),
			new StringParameter("UpdateId", updateId),
			new Int32Parameter("ResourceId", resourceId)
		};
		StringBuilder update = new StringBuilder().Append("update tResource ").Append("set    vchResourceSortAuthor = @SortAuthor ").Append("     , vchUpdaterId = @UpdateId ")
			.Append("     , dtLastUpdate = getdate() ")
			.Append("where iResourceId = @ResourceId ");
		int rowCount = ExecuteUpdateStatement(update.ToString(), parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("update row count: {0}", rowCount);
		return rowCount;
	}

	public int SetResourceStatus(int resourceId, ResourceStatus resourceStatus, string updateId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("StatusId", (int)resourceStatus),
			new StringParameter("UpdateId", updateId),
			new Int32Parameter("ResourceId", resourceId)
		};
		StringBuilder update = new StringBuilder().Append("update tResource ").Append("set    iResourceStatusId = @StatusId ").Append("     , vchUpdaterId = @UpdateId ")
			.Append("     , dtLastUpdate = getdate() ")
			.Append("where iResourceId = @ResourceId ");
		int rowCount = ExecuteUpdateStatement(update.ToString(), parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("update row count: {0}", rowCount);
		return rowCount;
	}

	public int SetResourceTabersStatus(int resourceId, bool resourceTabersStatus, string updateId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("TabersStatus", resourceTabersStatus ? 1 : 0),
			new StringParameter("UpdateId", updateId),
			new Int32Parameter("ResourceId", resourceId)
		};
		StringBuilder update = new StringBuilder().Append("update tResource ").Append("set    tiTabersStatus = @TabersStatus ").Append("     , vchUpdaterId = @UpdateId ")
			.Append("     , dtLastUpdate = getdate() ")
			.Append("where iResourceId = @ResourceId ");
		int rowCount = ExecuteUpdateStatement(update.ToString(), parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("update row count: {0}", rowCount);
		return rowCount;
	}

	public int AutoArchiveResources(string linkedServerName)
	{
		DateTime date = DateTime.Now;
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		string update = string.Format(string.IsNullOrWhiteSpace(linkedServerName) ? UpdateAutoArchive.Replace("[{0}].", "") : UpdateAutoArchive, linkedServerName, date);
		FactoryBase.Log.Debug(update);
		int rowCount = ExecuteUpdateStatement(update, parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("update row count: {0}", rowCount);
		string sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select iResourceId, {0}, 'AutoArchiveResources', getdate() ", 1).Append(" , ' [iResourceStatusId changed from Active(6) to Archived(7)]' ")
			.AppendFormat(" from tResource where  dtLastUpdate = '{0}' and vchUpdaterId = 'AutoArchive'", date)
			.ToString();
		int inserted = ExecuteInsertStatementReturnRowCount(sql, parameters.ToArray(), logSql: false);
		return rowCount;
	}

	public List<ArchiveResource> GetAutoArchiveResources(string linkedServerName)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		string sql = (string.IsNullOrWhiteSpace(linkedServerName) ? GetAutoArchives.Replace("[{0}].", "") : string.Format(GetAutoArchives, linkedServerName));
		FactoryBase.Log.Debug(sql);
		List<ArchiveResource> archiveResources = GetEntityList<ArchiveResource>(sql, parameters, logSql: true);
		FactoryBase.Log.DebugFormat("update row count: {0}", archiveResources.Count);
		return archiveResources;
	}

	public int UpdateResourceEditions(string linkedServerName)
	{
		int totalRowCount = 0;
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		string update = (string.IsNullOrWhiteSpace(linkedServerName) ? "\n            update r\n            set    r.iNewEditResourceId = new.iResourceId, r.vchUpdaterId = 'UpdateNewEdtitions', r.dtLastUpdate = getdate()\n            from  tResource r\n             join [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n             join tResource new on pr.newAvailEd = new.vchResourceISBN\n            where (r.iNewEditResourceId is null or r.iNewEditResourceId <> new.iResourceId) and\n                  (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72) ".Replace("[{0}].", "") : $"\n            update r\n            set    r.iNewEditResourceId = new.iResourceId, r.vchUpdaterId = 'UpdateNewEdtitions', r.dtLastUpdate = getdate()\n            from  tResource r\n             join [{linkedServerName}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n             join tResource new on pr.newAvailEd = new.vchResourceISBN\n            where (r.iNewEditResourceId is null or r.iNewEditResourceId <> new.iResourceId) and\n                  (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72) ");
		FactoryBase.Log.Debug(update);
		int rowCount = ExecuteUpdateStatement(update, parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("update New Editions row count: {0}", rowCount);
		totalRowCount += rowCount;
		update = (string.IsNullOrWhiteSpace(linkedServerName) ? "\n            update r\n            set    r.iPrevEditResourceID = prev.iResourceId, r.vchUpdaterId = 'UpdatePreviousEditions', r.dtLastUpdate = getdate()\n            from   tResource r\n             join [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n             join tResource prev on pr.previousEd = prev.vchResourceISBN\n            where (r.iPrevEditResourceID is null or r.iPrevEditResourceID <> prev.iResourceId) and\n                  (r.tiRecordStatus = 1 and prev.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and prev.iResourceStatusId <> 72) ".Replace("[{0}].", "") : $"\n            update r\n            set    r.iPrevEditResourceID = prev.iResourceId, r.vchUpdaterId = 'UpdatePreviousEditions', r.dtLastUpdate = getdate()\n            from   tResource r\n             join [{linkedServerName}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n             join tResource prev on pr.previousEd = prev.vchResourceISBN\n            where (r.iPrevEditResourceID is null or r.iPrevEditResourceID <> prev.iResourceId) and\n                  (r.tiRecordStatus = 1 and prev.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and prev.iResourceStatusId <> 72) ");
		FactoryBase.Log.Debug(update);
		rowCount = ExecuteUpdateStatement(update, parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("update Previous Editions row count: {0}", rowCount);
		totalRowCount += rowCount;
		ExecuteUpdateStatement("\n            update r\n            set    r.iLatestEditResourceId = null, r.vchUpdaterId = 'LastestEditionCleanUp', r.dtLastUpdate = getdate()\n            from   tresource r\n            where  (iNewEditResourceId = 0 or iNewEditResourceId is null) and r.iLatestEditResourceId is not null ", parameters.ToArray(), logSql: false);
		ExecuteUpdateStatement("\n            update r\n            set    r.iPrevEditResourceID = null, r.vchUpdaterId = 'LastestEditionCleanUp', r.dtLastUpdate = getdate()\n            from   tresource r\n            where  iPrevEditResourceID = 0 ", parameters.ToArray(), logSql: false);
		string sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select r.iResourceId, {0}, 'UpdateNewEdtitions', getdate(), ' [iNewEditResourceId changed from ' ", 1).Append(" + cast(isnull(r.iNewEditResourceId, 0) as varchar(10)) + ' to '+ cast(isnull(new.iResourceId, 0) as varchar(10)) + ']' ")
			.Append(" from  tResource r ")
			.AppendFormat(" join {0}[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku ", string.IsNullOrWhiteSpace(linkedServerName) ? "" : ("[" + linkedServerName + "]."))
			.Append(" join tResource new on pr.newAvailEd = new.vchResourceISBN ")
			.Append(" where (r.iNewEditResourceId is null or r.iNewEditResourceId <> new.iResourceId) and ")
			.Append(" (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 ")
			.Append(" and new.iResourceStatusId <> 72) ")
			.ToString();
		int inserted = ExecuteInsertStatementReturnRowCount(sql, parameters.ToArray(), logSql: false);
		return totalRowCount;
	}

	public List<NewEditionResource> GetNewEditionResources(string linkedServerName)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		string sql = (string.IsNullOrWhiteSpace(linkedServerName) ? "\n            select iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate, sum(LicenseCount) as LicenseCount, PreviousIsbn\n            from (\n                select new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice,\n                       new.dtRISReleaseDate, ISNULL(sum(irl.iLicenseCount), '0') as LicenseCount, r.vchResourceISBN as PreviousIsbn\n                from   tResource r\n                 join  [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n                 join  tResource new on pr.newAvailEd = new.vchResourceISBN\n                 join  tPublisher p on r.iPublisherId = p.iPublisherId\n                 join  tInstitutionResourceLicense irl on r.iResourceId = irl.iResourceId\n                where  r.iNewEditResourceId <> new.iResourceId\n                  and (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72)\n                group by new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice, new.dtRISReleaseDate, r.vchResourceISBN\n                union\n                select new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice,\n                       new.dtRISReleaseDate, 0 as LicenseCount, r.vchResourceISBN as PreviousIsbn\n                from   tResource r\n                 join  [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n                 join  tResource new on pr.newAvailEd = new.vchResourceISBN\n                 join  tPublisher p on r.iPublisherId = p.iPublisherId\n                 join  tInstitutionResourceLicense irl on r.iResourceId = irl.iResourceId\n                where  r.iNewEditResourceId <> new.iResourceId\n                  and  (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72)\n                group by new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice, new.dtRISReleaseDate, r.vchResourceISBN\n            ) as test\n            group by iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate, PreviousIsbn ".Replace("[{0}].", "") : string.Format("\n            select iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate, sum(LicenseCount) as LicenseCount, PreviousIsbn\n            from (\n                select new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice,\n                       new.dtRISReleaseDate, ISNULL(sum(irl.iLicenseCount), '0') as LicenseCount, r.vchResourceISBN as PreviousIsbn\n                from   tResource r\n                 join  [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n                 join  tResource new on pr.newAvailEd = new.vchResourceISBN\n                 join  tPublisher p on r.iPublisherId = p.iPublisherId\n                 join  tInstitutionResourceLicense irl on r.iResourceId = irl.iResourceId\n                where  r.iNewEditResourceId <> new.iResourceId\n                  and (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72)\n                group by new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice, new.dtRISReleaseDate, r.vchResourceISBN\n                union\n                select new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice,\n                       new.dtRISReleaseDate, 0 as LicenseCount, r.vchResourceISBN as PreviousIsbn\n                from   tResource r\n                 join  [{0}].[PreludeData].[dbo].[Product] pr on r.vchResourceISBN = pr.sku\n                 join  tResource new on pr.newAvailEd = new.vchResourceISBN\n                 join  tPublisher p on r.iPublisherId = p.iPublisherId\n                 join  tInstitutionResourceLicense irl on r.iResourceId = irl.iResourceId\n                where  r.iNewEditResourceId <> new.iResourceId\n                  and  (r.tiRecordStatus = 1 and new.tiRecordStatus = 1 and r.iResourceStatusId <> 72 and new.iResourceStatusId <> 72)\n                group by new.iResourceId, new.vchResourceISBN, new.vchResourceTitle, p.vchPublisherName, new.decResourcePrice, new.dtRISReleaseDate, r.vchResourceISBN\n            ) as test\n            group by iResourceId, vchResourceISBN, vchResourceTitle, vchPublisherName, decResourcePrice, dtRISReleaseDate, PreviousIsbn ", linkedServerName));
		FactoryBase.Log.Debug(sql);
		List<NewEditionResource> newEditionResource = GetEntityList<NewEditionResource>(sql, parameters, logSql: true);
		FactoryBase.Log.DebugFormat("update row count: {0}", newEditionResource.Count);
		return newEditionResource;
	}

	public int UpdateResourceAffiliation(string linkedServerName)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		string sql = new StringBuilder().Append("INSERT INTO tResourceAudit ").AppendFormat("select r.iResourceId, {0}, 'UpdateResourceAffiliation', getdate(), ' [vchAffiliation changed from ''' ", 1).Append(" + vchAffiliation + ''' to '''+  p.affiliation + ''']' ")
			.Append("from  tResource r ")
			.AppendFormat(" INNER JOIN {0}RittenhouseWeb.dbo.Product p on r.vchIsbn10 = p.isbn10 ", string.IsNullOrWhiteSpace(linkedServerName) ? "" : ("[" + linkedServerName + "]."))
			.Append("WHERE  vchAffiliation <> p.affiliation")
			.ToString();
		int inserted = ExecuteInsertStatementReturnRowCount(sql, parameters.ToArray(), logSql: false);
		sql = (string.IsNullOrWhiteSpace(linkedServerName) ? UpdateResourceAffiliationSql.Replace("[{0}].", "") : string.Format(UpdateResourceAffiliationSql, linkedServerName));
		FactoryBase.Log.Debug(sql);
		int rowCount = ExecuteUpdateStatement(sql, parameters.ToArray(), logSql: false);
		FactoryBase.Log.DebugFormat("update row count: {0}", rowCount);
		return (rowCount > 0) ? rowCount : 0;
	}

	public List<EmailResource> ProcessResourceLatestEditions()
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		string sql = "select iResourceId, iPrevEditResourceId, vchResourceIsbn from tResource where iNewEditResourceId is null and (tiRecordStatus = 1 and iResourceStatusId <> 72)";
		List<ResourceEdition> latestEditions = GetEntityList<ResourceEdition>(sql, null, logSql: false);
		foreach (ResourceEdition resourceEdition in latestEditions)
		{
			sql = "select iResourceId, vchResourceIsbn, iPrevEditResourceId, iLatestEditResourceId from tResource where iNewEditResourceId = @NewEdititonResourceId and (tiRecordStatus = 1 and iResourceStatusId <> 72)";
			parameters = new List<ISqlCommandParameter>
			{
				new Int32Parameter("NewEdititonResourceId", resourceEdition.ResourceId)
			};
			List<ChildResourceEdition> prevEditions = GetEntityList<ChildResourceEdition>(sql, parameters, logSql: false);
			if (prevEditions.Any())
			{
				SetChildEditions(resourceEdition, prevEditions);
			}
		}
		List<ResourceEdition> latestEditionsToUpdate = latestEditions.Where((ResourceEdition x) => x.ResourcesToSetLatestEdition != null && x.ResourcesToSetLatestEdition.Any()).ToList();
		List<int> latestEditionResouresUpdated = new List<int>();
		DateTime currentDateTime = DateTime.Now;
		int lastEdtitionSetCount = 0;
		foreach (ResourceEdition resourceEdition2 in latestEditionsToUpdate)
		{
			StringBuilder resourceIdSql = new StringBuilder();
			foreach (ChildResourceEdition childEdition in resourceEdition2.ResourcesToSetLatestEdition)
			{
				resourceIdSql.AppendFormat("{0},", childEdition.ResourceId);
				latestEditionResouresUpdated.Add(childEdition.ResourceId);
			}
			if (resourceIdSql.Length > 0)
			{
				sql = new StringBuilder().Append(" INSERT INTO tResourceAudit ").AppendFormat(" select iResourceId, {0}, 'Set iLatestEditResourceId', getdate(), ' [iLatestEditResourceId changed from ' + cast(isnull(iLatestEditResourceId, 0) as varchar(10)) + ' to 1]' ", 1).AppendFormat(" from tResource where iResourceId in ({0})", resourceIdSql.ToString(0, resourceIdSql.Length - 1))
					.ToString();
				int inserted = ExecuteInsertStatementReturnRowCount(sql, parameters.ToArray(), logSql: false);
				sql = string.Format("update tResource set iLatestEditResourceId = {0}, vchUpdaterId = 'Set iLatestEditResourceId', dtLastUpdate = '{2}' where iResourceId in ({1})", resourceEdition2.ResourceId, resourceIdSql.ToString(0, resourceIdSql.Length - 1), currentDateTime);
				lastEdtitionSetCount += ExecuteUpdateStatement(sql, parameters.ToArray(), logSql: false);
			}
		}
		sql = "\n                select r.vchResourceISBN, r.vchResourceTitle, r.vchResourceEdition, p.vchPublisherName, r.decResourcePrice\n                , sum(irl.iLicenseCount) as LicenseCount, r.dtRISReleaseDate, r.iResourceId\n                , newr.vchResourceISBN as NewIsbn, newr.vchResourceTitle as NewTitle, newr.vchResourceEdition as NewEdition\n                from tResource r\n                join tresource newR on r.iLatestEditResourceId = newR.iResourceId\n                join tPublisher p on r.iPublisherId = p.iPublisherId\n                left join tInstitutionResourceLicense irl on irl.iResourceId = r.iResourceId\n                where r.vchUpdaterId = 'Set iLatestEditResourceId' and r.dtLastUpdate = '{0}'\n                group by r.vchResourceISBN, r.vchResourceTitle, r.vchResourceEdition, p.vchPublisherName, r.decResourcePrice\n                , r.dtRISReleaseDate, r.iResourceId, newr.vchResourceISBN, newr.vchResourceTitle , newr.vchResourceEdition\n                ";
		List<EmailResource> emailResources = null;
		if (lastEdtitionSetCount > 0)
		{
			string sqlQuery = string.Format(sql, currentDateTime);
			emailResources = GetEntityList<EmailResource>(sqlQuery, parameters, logSql: false);
			if (emailResources.Any())
			{
				return emailResources.OrderBy((EmailResource x) => x.NewIsbn).ToList();
			}
		}
		return emailResources;
	}

	private void SetChildEditions(ResourceEdition resourceEdition, List<ChildResourceEdition> childResourceEditions)
	{
		resourceEdition.ResourcesToSetLatestEdition = new List<ChildResourceEdition>();
		if (childResourceEditions.Any((ChildResourceEdition y) => y.LatestEditResourceId != resourceEdition.ResourceId))
		{
			resourceEdition.ResourcesToSetLatestEdition.AddRange(childResourceEditions.Where((ChildResourceEdition y) => y.LatestEditResourceId != resourceEdition.ResourceId));
			foreach (ChildResourceEdition childResourceEdition in childResourceEditions)
			{
				FactoryBase.Log.DebugFormat("ChildResourceEdition ResourceId: {0} CurrentLatestEditResourceId: {1} NewLatestEditResourceId: {2}", childResourceEdition.ResourceId, childResourceEdition.LatestEditResourceId, resourceEdition.ResourceId);
			}
		}
		string sql = new StringBuilder().Append(" select iResourceId, vchResourceIsbn, iPrevEditResourceId, iLatestEditResourceId from tResource ").Append(" where iNewEditResourceId = @NewEdititonResourceId and (tiRecordStatus = 1 and iResourceStatusId <> 72) ").Append(" and (iLatestEditResourceId is null or iLatestEditResourceId <> @LatestEditResourceId) ")
			.ToString();
		if (!childResourceEditions.Any())
		{
			return;
		}
		List<ChildResourceEdition> tempChildEditions = new List<ChildResourceEdition>();
		foreach (ChildResourceEdition childResourceEdition2 in childResourceEditions)
		{
			List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
			{
				new Int32Parameter("NewEdititonResourceId", childResourceEdition2.ResourceId),
				new Int32Parameter("LatestEditResourceId", resourceEdition.ResourceId)
			};
			List<ChildResourceEdition> prevEditions = GetEntityList<ChildResourceEdition>(sql, parameters, logSql: false);
			if (!prevEditions.Any())
			{
				continue;
			}
			tempChildEditions.AddRange(prevEditions);
			foreach (ChildResourceEdition prevEdition in prevEditions)
			{
				FactoryBase.Log.DebugFormat("ChildResourceEdition ResourceId: {0} CurrentLatestEditResourceId: {1} NewLatestEditResourceId: {2}", prevEdition.ResourceId, prevEdition.LatestEditResourceId, resourceEdition.ResourceId);
			}
		}
		if (tempChildEditions.Any())
		{
			resourceEdition.ResourcesToSetLatestEdition.AddRange(tempChildEditions);
		}
	}

	public void UpdateInstitutionConsortia(string linkedServerName)
	{
		string linkedServer = (string.IsNullOrWhiteSpace(linkedServerName) ? "" : ("[" + linkedServerName + "]."));
		string sql = new StringBuilder().Append("update tInstitution ").Append("set vchConsortia = c.consort ").Append("from tInstitution i ")
			.AppendFormat("join {0}[PreludeData].[dbo].[Customer] c on i.vchInstitutionAcctNum = c.accountNumber ", linkedServer)
			.Append("where c.consort is not null ")
			.ToString();
		FactoryBase.Log.Debug(sql);
		int rowCount = ExecuteUpdateStatement(sql, new List<ISqlCommandParameter>(), logSql: false);
		FactoryBase.Log.DebugFormat("UpdateInstitutionConsortia -- update row count: {0}", rowCount);
	}

	public void UpdateInstitutionTerritory(string linkedServerName)
	{
		string updateSql = string.Format("\nupdate tInstitution\nset iTerritoryId = ct.iTerritoryId\n, vchUpdaterId = 'UpdateInstitutionTerritory'\n, dtLastUpdate = GETDATE()\nfrom tInstitution i\njoin tTerritory t on t.iTerritoryId = i.iTerritoryId\njoin {0}[PreludeData].[dbo].Customer c on c.accountNumber = i.vchInstitutionAcctNum\njoin tTerritory ct on ct.vchTerritoryCode = c.territory\nwhere t.vchTerritoryCode <> ct.vchTerritoryCode\n", string.IsNullOrWhiteSpace(linkedServerName) ? "" : ("[" + linkedServerName + "]."));
		FactoryBase.Log.Debug(updateSql);
		int updateRowCount = ExecuteUpdateStatement(updateSql, new List<ISqlCommandParameter>(), logSql: false);
		FactoryBase.Log.DebugFormat("UpdateInstitutionTerritory -- update row count: {0}", updateRowCount);
	}

	public int UpdateEisbns(List<OnixEisbn> onixEisbns)
	{
		int takeCount = 25;
		int i = 0;
		int totalUpdated = 0;
		while (true)
		{
			OnixEisbn[] items = onixEisbns.Skip(i * takeCount).Take(takeCount).ToArray();
			if (items.Length == 0)
			{
				break;
			}
			StringBuilder sql = new StringBuilder();
			OnixEisbn[] array = items;
			foreach (OnixEisbn onixEisbn in array)
			{
				string eIsbn = onixEisbn.EIsbn13 ?? onixEisbn.EIsbn10;
				if (!string.IsNullOrWhiteSpace(eIsbn))
				{
					sql.Append(" Insert into tResourceAudit([iResourceId],[tiResourceAuditTypeId],[vchCreatorId],[dtCreationDate],[vchEventDescription]) ");
					sql.Append(" select iResourceId, 1, 'UpdateWithOnixDataTask', GETDATE(), ");
					sql.AppendFormat(" case when vchEIsbn is null then 'Adding eIsbn from ONIX' else 'Change vchEIsbn from ' + vchEIsbn + ' to {0}' end ", eIsbn);
					sql.AppendFormat(" from tResource where vchIsbn13 = '{1}' and (vchEIsbn is null or vchEIsbn <> '{0}'); ", eIsbn, onixEisbn.Isbn13);
					sql.AppendFormat(" Update tResource set vchEIsbn = '{0}', vchUpdaterId = 'UpdateWithOnixDataTask', dtLastUpdate = getdate() where vchisbn13 = '{1}' and (vchEIsbn is null or vchEIsbn <> '{0}'); ", eIsbn, onixEisbn.Isbn13);
				}
			}
			totalUpdated += ExecuteUpdateStatement(sql.ToString(), new List<ISqlCommandParameter>(), logSql: false);
			i++;
		}
		return totalUpdated;
	}

	public List<ResourceTitleChange> GetRittenhouseTitles(string linkedServerName, Dictionary<string, string> isbnAndTitles)
	{
		StringBuilder resourceIsbns = new StringBuilder();
		foreach (KeyValuePair<string, string> isbnAndTitle2 in isbnAndTitles)
		{
			resourceIsbns.AppendFormat("{1}'{0}'", isbnAndTitle2.Key, (resourceIsbns.Length == 0) ? "" : ",");
		}
		string sql = "\nselect p.title, p.subtitle, r.vchResourceTitle, r.vchResourceSubTitle, r.iResourceId, r.vchResourceIsbn, r.vchIsbn13\nfrom tResource r\nleft join [PreludeData]..Product p on r.vchResourceIsbn = p.sku\nwhere r.vchResourceIsbn in ({0})\norder by r.iResourceId desc\n";
		sql = string.Format(sql, resourceIsbns);
		List<ResourceTitleChange> rittenhouseResourceTitles = GetEntityList<ResourceTitleChange>(sql, new List<ISqlCommandParameter>(), logSql: false);
		foreach (ResourceTitleChange rittenhouseResourceTitle in rittenhouseResourceTitles)
		{
			rittenhouseResourceTitle.AlternateTitle = isbnAndTitles[rittenhouseResourceTitle.Isbn];
		}
		return rittenhouseResourceTitles;
	}

	public List<ResourceTitleChange> GetRittenhouseTitles(string linkedServerName, int minResourceId, int maxResourceId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MinResourceId", minResourceId),
			new Int32Parameter("MaxResourceId", maxResourceId)
		};
		string sql = "\nselect ltrim(rtrim(p.title)) as title, ltrim(rtrim(p.subtitle)) as subtitle, ltrim(rtrim(r.vchResourceTitle)) as vchResourceTitle\n, ltrim(rtrim(r.vchResourceSubTitle)) as vchResourceSubTitle, r.iResourceId, r.vchResourceIsbn, r.vchIsbn13\nfrom tResource r\nleft join [RittenhouseWeb]..Product p on r.vchResourceISBN = p.isbn10\nwhere r.iResourceStatusId in (6,7)\nand r.iResourceId >= @MinResourceId\nand r.iResourceId <= @MaxResourceId\nand r.tiRecordStatus = 1\norder by r.iResourceId desc\n";
		return GetEntityList<ResourceTitleChange>(sql, parameters, logSql: false);
	}

	public bool UpdateResourceTitle(ResourceTitleChange resourceTitleChange, string r2UtilitiesDatabaseName)
	{
		try
		{
			List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
			{
				new Int32Parameter("ResourceId", resourceTitleChange.ResourceId)
			};
			string transformQueueInsert = new StringBuilder().Append("insert into " + r2UtilitiesDatabaseName + "..TransformQueue (resourceId, isbn, status, dateAdded) ").Append(" select r.iResourceId, '" + resourceTitleChange.Isbn + "', 'A', GETDATE() ").Append(" from tResource r ")
				.Append(" left join " + r2UtilitiesDatabaseName + "..TransformQueue tq on r.iResourceId = tq.resourceId and tq.status = 'A' ")
				.Append($" where r.iResourceId = {resourceTitleChange.ResourceId} and tq.transformQueueId is null ")
				.ToString();
			StringBuilder auditInsert = new StringBuilder().Append(" Insert into tResourceAudit([iResourceId],[tiResourceAuditTypeId],[vchCreatorId],[dtCreationDate],[vchEventDescription]) ").Append(" select iResourceId, 1, 'UpdateTitleTask', GETDATE(), ").AppendFormat(" '{1} title from ' + ISNULL(vchResourceTitle, '') + ' to {0} | ", resourceTitleChange.GetNewTitle().Replace("'", "''"), resourceTitleChange.IsRevert ? "Reverted" : "UpUpdateddates")
				.AppendFormat("RittenhouseTitle: {0}", resourceTitleChange.RittenhouseTitle?.Replace("'", "''") ?? "");
			if (resourceTitleChange.IsRevert)
			{
				if (resourceTitleChange.SubTitle != resourceTitleChange.GetNewSubTitle())
				{
					auditInsert.AppendFormat(" | Updated Subtitle from ' + ISNULL(vchResourceSubTitle, '') + ' to {0}", resourceTitleChange.GetNewSubTitle()?.Replace("'", "''"));
				}
			}
			else if (resourceTitleChange.UpdateType == ResourceTitleUpdateType.RittenhouseEqualR2TitleAndSub)
			{
				auditInsert.Append(" | Removed Subtitle");
			}
			auditInsert.Append("' from tResource where iResourceId = @ResourceId ; ");
			StringBuilder resourceUpdate = new StringBuilder().AppendFormat(" Update tResource set vchResourceTitle = '{0}', vchUpdaterId = 'UpdateTitleTask', dtLastUpdate = getdate() ", resourceTitleChange.GetNewTitle().Replace("'", "''").Replace("&amp;", "&"));
			if (resourceTitleChange.IsRevert)
			{
				if (resourceTitleChange.SubTitle != resourceTitleChange.GetNewSubTitle())
				{
					resourceUpdate.AppendFormat(", vchResourceSubTitle = '{0}' ", resourceTitleChange.GetNewSubTitle()?.Replace("'", "''").Replace("&amp;", "&"));
				}
			}
			else if (resourceTitleChange.UpdateType == ResourceTitleUpdateType.RittenhouseEqualR2TitleAndSub)
			{
				resourceUpdate.Append(", vchResourceSubTitle = null ");
			}
			resourceUpdate.Append(" where iResourceId = @ResourceId; ");
			string sql = new StringBuilder().Append(transformQueueInsert).Append(auditInsert).Append(resourceUpdate)
				.ToString();
			return ExecuteUpdateStatement(sql, parameters, logSql: false) > 0;
		}
		catch (Exception ex)
		{
			FactoryBase.Log.Error(ex.Message, ex);
		}
		return false;
	}

	public int InsertDataIntoConfigSettings(string[] configurationSettingInserts, bool deleteFirst)
	{
		if (deleteFirst)
		{
			ExecuteUpdateStatement("truncate table tConfigurationSetting;", new List<ISqlCommandParameter>(), logSql: false);
		}
		StringBuilder sql = new StringBuilder();
		int insertCounter = 0;
		int counter = 0;
		foreach (string item in configurationSettingInserts)
		{
			counter++;
			sql.Append(item);
			if (counter == 50)
			{
				insertCounter += ExecuteInsertStatementReturnRowCount(sql.ToString(), null, logSql: true);
				sql = new StringBuilder();
				counter = 0;
			}
		}
		if (counter != 0)
		{
			insertCounter += ExecuteInsertStatementReturnRowCount(sql.ToString(), null, logSql: true);
		}
		return insertCounter;
	}

	public List<ResourcePriceUpdateItem> GetResourcePriceUpdates()
	{
		string sql = "\n select rpu.*\n from tResourcePriceUpdate rpu\n join tResource r on r.tiRecordStatus = 1 and r.vchIsbn10 = rpu.vchResourceISBN or r.vchIsbn13 = rpu.vchResourceISBN\n where CONVERT(date, dtUpdateDate) <= CONVERT(date, getdate()) and rpu.tiRecordStatus = 1 and rpu.dtLastUpdate is null\n";
		return GetEntityList<ResourcePriceUpdateItem>(sql, new List<ISqlCommandParameter>(), logSql: false);
	}

	public int UpdateResourcePrice(ResourcePriceUpdateItem resourcePriceUpdateItem)
	{
		string sql = $"\n Insert into tResourceAudit([iResourceId],[tiResourceAuditTypeId],[vchCreatorId],[dtCreationDate],[vchEventDescription])\n Select r.iResourceId, 1, 'PriceUpdateTask', GETDATE(), 'Updated ListPrice from ' + convert(varchar(100), r.decResourcePrice) + ' to ' + convert(varchar(100), rpu.decResourcePrice)\n from tResource r join tResourcePriceUpdate rpu on r.vchIsbn10 = rpu.vchResourceISBN or r.vchIsbn13 = rpu.vchResourceISBN\n where r.decResourcePrice <> rpu.decResourcePrice and rpu.iResourcePriceUpdateId = {resourcePriceUpdateItem.Id}\n\n update tResource\n set decResourcePrice = rpu.decResourcePrice,\n dtLastUpdate = GETDATE(),\n vchUpdaterId = 'PriceUpdateTask'\n from tResource r\n join tResourcePriceUpdate rpu on r.vchIsbn10 = rpu.vchResourceISBN or r.vchIsbn13 = rpu.vchResourceISBN\n where  r.decResourcePrice <> rpu.decResourcePrice and  rpu.iResourcePriceUpdateId = {resourcePriceUpdateItem.Id};\n\n update tResourcePriceUpdate\n set dtLastUpdate = GETDATE(),\n vchUpdaterId = 'PriceUpdateTask'\n where iResourcePriceUpdateId  = {resourcePriceUpdateItem.Id};\n";
		return ExecuteUpdateStatement(sql, new List<ISqlCommandParameter>(), logSql: false);
	}

	public int InsertYbpResources()
	{
		string sql = "\nInsert into rittenhouse..R2Library_Resources ([sku], [EAN_13], [eISBN], [list_price], [status])\nselect r.vchResourceISBN, r.vchIsbn13, r.vchEIsbn, round(CAST(r.decResourcePrice * 100 AS float), 0), r.iResourceStatusId\nfrom tResource r\nwhere r.tiRecordStatus = 1 and r.iResourceStatusId <> 72\n";
		return ExecuteUpdateStatement(sql, new List<ISqlCommandParameter>(), logSql: false);
	}

	public int TruncateYbpResources()
	{
		string sql = "truncate table rittenhouse..R2Library_Resources";
		return ExecuteUpdateStatement(sql, new List<ISqlCommandParameter>(), logSql: false);
	}
}
