using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using NHibernate.Linq;
using NHibernate.Transform;
using NHibernate.Util;
using R2Utilities.Infrastructure.Settings;
using R2V2.Core.Admin;
using R2V2.Core.Authentication;
using R2V2.Core.CollectionManagement;
using R2V2.Core.CollectionManagement.PatronDrivenAcquisition;
using R2V2.Core.Institution;
using R2V2.Core.Recommendations;
using R2V2.Core.Reports;
using R2V2.Core.Resource;
using R2V2.Core.Resource.Discipline;
using R2V2.Core.Territory;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.UnitOfWork;

namespace R2Utilities.DataAccess;

public class EmailTaskService : R2UtilitiesBase
{
	private static readonly string CartItemInsert = new StringBuilder().Append("INSERT INTO [dbo].[tCartItem] ").Append("([iCartId], [iResourceId], [iNumberOfLicenses], [vchCreatorId], [dtCreationDate], [tiRecordStatus], [decListPrice] ").Append(", [decDiscountPrice], [tiInclude], [tiAgree], [tiLicenseOriginalSourceId], [dtAddedByNewEdition]) ")
		.Append("VALUES")
		.Append("({0}, {1}, 1, 'NewEditionTask', getdate(), 1, {2} ")
		.Append(", {3}, 1, 0, 1, getdate());")
		.ToString();

	private static readonly string CartInsert = new StringBuilder().Append("INSERT INTO [dbo].[tCart] ").Append("([iInstitutionId], [tiProcessed], [vchCreatorId], [dtCreationDate] ").Append(", [tiRecordStatus], [decInstDiscount], [decPromotionDiscount]) ")
		.Append("VALUES({0}, 0, 'NewEditionTask', getdate() ")
		.Append(", 1, {1}, 0.00); ")
		.Append("SELECT SCOPE_IDENTITY()")
		.ToString();

	private readonly IQueryable<Cart> _carts;

	private readonly IQueryable<IFeaturedTitle> _featuredTitles;

	private readonly IQueryable<InstitutionResourceLicense> _institutionResourceLicenses;

	private readonly IQueryable<Institution> _institutions;

	private readonly ILog<EmailTaskService> _log;

	private readonly IR2UtilitiesSettings _r2UtilitiesSettings;

	private readonly IQueryable<Recommendation> _recommendations;

	private readonly ReportService _reportService;

	private readonly ResourceDiscountService _resourceDiscountService;

	private readonly IQueryable<IResource> _resources;

	private readonly IQueryable<SpecialResource> _specialResources;

	private readonly IQueryable<Specialty> _specialties;

	private readonly IUnitOfWork _unitOfWork;

	private readonly IQueryable<User> _users;

	private readonly IQueryable<UserTerritory> _userTerritories;

	private Dictionary<string, bool> IsbnDictionary { get; set; }

	public EmailTaskService(IQueryable<User> users, IQueryable<Cart> carts, IQueryable<IResource> resources, IQueryable<InstitutionResourceLicense> institutionResourceLicenses, ReportService reportService, IUnitOfWork unitOfWork, ILog<EmailTaskService> log, IR2UtilitiesSettings r2UtilitiesSettings, IQueryable<UserTerritory> userTerritories, IQueryable<Institution> institutions, IQueryable<FeaturedTitle> featuredTitles, IQueryable<SpecialResource> specialResources, IQueryable<Recommendation> recommendations, IQueryable<Specialty> specialties, ResourceDiscountService resourceDiscountService)
	{
		_users = users;
		_carts = carts;
		_resources = resources;
		_institutionResourceLicenses = institutionResourceLicenses;
		_reportService = reportService;
		_unitOfWork = unitOfWork;
		_log = log;
		_r2UtilitiesSettings = r2UtilitiesSettings;
		_userTerritories = userTerritories;
		_institutions = institutions;
		_featuredTitles = featuredTitles;
		_specialResources = specialResources;
		_recommendations = recommendations;
		_specialties = specialties;
		_resourceDiscountService = resourceDiscountService;
	}

	public List<User> GetUsersForNewResourceEmail()
	{
		return (from x in GetBaseUsers()
			where x.RecordStatus && (x.ExpirationDate == null || x.ExpirationDate > DateTime.Now) && x.Institution.RecordStatus && (x.Institution.AccountStatusId == 1 || (x.Institution.AccountStatusId == 2 && x.Institution.Trial.EndDate > DateTime.Now))
			select x).HasOptionSelected(UserOptionCode.NewResource).ToList();
	}

	public List<User> GetUsersForPurchasedForthcomingEmail()
	{
		return (from x in GetBaseUsers()
			where (int)x.Role.Code == 1 && x.RecordStatus && (x.ExpirationDate == null || x.ExpirationDate > DateTime.Now) && x.Institution.RecordStatus && (x.Institution.AccountStatusId == 1 || (x.Institution.AccountStatusId == 2 && x.Institution.Trial.EndDate > DateTime.Now))
			select x).HasOptionSelected(UserOptionCode.ForthcomingPurchase).ToList();
	}

	public List<User> GetUsersForTurnawayEmail()
	{
		return (from x in GetBaseUsers()
			where (int)x.Role.Code == 1 && x.RecordStatus && (x.ExpirationDate == null || x.ExpirationDate > DateTime.Now) && x.Institution.RecordStatus && (x.Institution.AccountStatusId == 1 || (x.Institution.AccountStatusId == 2 && x.Institution.Trial.EndDate > DateTime.Now))
			select x).HasOptionSelected(UserOptionCode.AccessDenied).ToList();
	}

	public List<User> GetUsersForNewEditionEmail()
	{
		return (from x in GetBaseUsers()
			where x.RecordStatus && (x.ExpirationDate == null || x.ExpirationDate > DateTime.Now) && x.Institution.RecordStatus && (x.Institution.AccountStatusId == 1 || (x.Institution.AccountStatusId == 2 && x.Institution.Trial.EndDate > DateTime.Now))
			select x).HasOptionSelected(UserOptionCode.NewEdition).ToList();
	}

	public List<User> GetUsersForDctUpdateEmails(int practiceAreaId)
	{
		return practiceAreaId switch
		{
			1 => (from x in GetBaseUsers()
				where x.RecordStatus && (x.ExpirationDate == null || x.ExpirationDate > DateTime.Now) && x.Institution.RecordStatus && (x.Institution.AccountStatusId == 1 || (x.Institution.AccountStatusId == 2 && x.Institution.Trial.EndDate > DateTime.Now))
				select x).HasOptionSelected(UserOptionCode.DctMedical).ToList(), 
			2 => (from x in GetBaseUsers()
				where x.RecordStatus && (x.ExpirationDate == null || x.ExpirationDate > DateTime.Now) && x.Institution.RecordStatus && (x.Institution.AccountStatusId == 1 || (x.Institution.AccountStatusId == 2 && x.Institution.Trial.EndDate > DateTime.Now))
				select x).HasOptionSelected(UserOptionCode.DctNursing).ToList(), 
			3 => (from x in GetBaseUsers()
				where x.RecordStatus && (x.ExpirationDate == null || x.ExpirationDate > DateTime.Now) && x.Institution.RecordStatus && (x.Institution.AccountStatusId == 1 || (x.Institution.AccountStatusId == 2 && x.Institution.Trial.EndDate > DateTime.Now))
				select x).HasOptionSelected(UserOptionCode.DctAlliedHealth).ToList(), 
			_ => null, 
		};
	}

	public Cart GetCartForShoppingCartTask(int cartId)
	{
		return _carts.FirstOrDefault((Cart x) => x.Id == cartId);
	}

	public List<Cart> GetSavedCartsForInstitution(int institutionId)
	{
		return _carts.Where((Cart x) => x.InstitutionId == institutionId && !x.Processed && (int)x.CartType == 2).ToList();
	}

	public AdminInstitution GetAdminInstitution(int institutionId)
	{
		Institution institution = _institutions.FirstOrDefault((Institution x) => x.Id == institutionId);
		return new AdminInstitution(institution);
	}

	public IResource GetResource(int resourceId)
	{
		return _resources.FirstOrDefault((IResource x) => x.Id == resourceId);
	}

	public List<TurnawayResource> GetInstitutionTurnaways(string reportDatabaseName, string r2DatabaseName)
	{
		List<TurnawayResource> turnawayResources = _reportService.GetTurnawayResources2(reportDatabaseName, r2DatabaseName);
		foreach (TurnawayResource turnawayResource in turnawayResources)
		{
			turnawayResource.Resource = GetResource(turnawayResource.ResourceId);
		}
		return turnawayResources;
	}

	public Dictionary<string, bool> GetResourceEmailResources(string emailType)
	{
		string sql = new StringBuilder().Append("select r.vchResourceISBN ").Append(", Cast((case when re.id is null then 0 else 1 end)as tinyint) as Found ").Append("from tResource r ")
			.AppendFormat("left outer join [{0}].[dbo].[ResourceEmails] re on r.vchResourceISBN = re.resourceIsbn ", _r2UtilitiesSettings.R2UtilitiesDatabaseName)
			.Append("where r.iResourceStatusId = 6 and r.tiRecordStatus = 1 ")
			.AppendFormat("and (re.id is null or re.{0} is null) ", emailType)
			.Append("order by r.iResourceId asc ")
			.ToString();
		IList query = _unitOfWork.Session.CreateSQLQuery(sql).List();
		return query.Cast<object[]>().ToDictionary((object[] pair) => pair[0].ToString(), (object[] pair) => pair[1].ToString() == "1");
	}

	private List<string> GetDctUpdateResources(int practiceAreaId)
	{
		string sql = new StringBuilder().Append(" select r.vchResourceISBN ").Append(" from tResource r ").Append(" join tPublisher p on r.iPublisherId = r.iPublisherId and p.tiRecordStatus = 1 ")
			.Append(" join tResourcePracticeArea rpa on r.iResourceId = rpa.iResourceId and rpa.tiRecordStatus = 1 ")
			.AppendFormat(" join {0}..ResourceEmails re on r.vchResourceISBN = re.resourceISBN ", _r2UtilitiesSettings.R2UtilitiesDatabaseName)
			.AppendFormat(" where rpa.iPracticeAreaId = {0} ", practiceAreaId)
			.AppendFormat(" and re.dateNewResourceEmail between (GETDATE()-{0}) and GETDATE() ", _r2UtilitiesSettings.DctUpdateEmailStartDaysAgo)
			.Append(" and r.iDCTStatusId in (158,159) ")
			.Append(" group by r.vchResourceISBN ")
			.ToString();
		IList query = _unitOfWork.Session.CreateSQLQuery(sql).List();
		return query.Cast<string>().ToList();
	}

	private void UpdateInsertResourceEmailResources(string emailType)
	{
		if (IsbnDictionary == null)
		{
			IsbnDictionary = GetResourceEmailResources(emailType);
		}
		string insertSql = string.Format("Insert INTO [{1}].[dbo].[ResourceEmails] ([resourceISBN], [{0}]) VALUES ('[ISBN]', GetDate())", emailType, _r2UtilitiesSettings.R2UtilitiesDatabaseName);
		string updateSql = string.Format("Update [{1}].[dbo].[ResourceEmails] set {0} = GetDate() Where [resourceISBN] = '[ISBN]'", emailType, _r2UtilitiesSettings.R2UtilitiesDatabaseName);
		StringBuilder sql = new StringBuilder();
		foreach (KeyValuePair<string, bool> pair in IsbnDictionary.Where((KeyValuePair<string, bool> x) => x.Value))
		{
			sql.Append((updateSql + "; ").Replace("[ISBN]", pair.Key));
		}
		foreach (KeyValuePair<string, bool> pair2 in IsbnDictionary.Where((KeyValuePair<string, bool> x) => !x.Value))
		{
			sql.Append((insertSql + "; ").Replace("[ISBN]", pair2.Key));
		}
		if (sql.Length > 0)
		{
			_unitOfWork.Session.CreateSQLQuery(sql.ToString()).List();
			_log.DebugFormat("UpdateInsertResourceEmailResources {0}", emailType);
			_log.DebugFormat("sql : [[{0}]]", sql.ToString());
		}
	}

	public List<IResource> GetNewResourceEmailResources()
	{
		try
		{
			if (IsbnDictionary == null || IsbnDictionary.Count == 0)
			{
				IsbnDictionary = GetResourceEmailResources("dateNewResourceEmail");
			}
			List<IResource> resources = _resources.Where((IResource x) => IsbnDictionary.Keys.Contains(x.Isbn)).ToList();
			return resources.OrderBy(delegate(IResource x)
			{
				ISpecialty specialty = x.Specialties.OrderBy((ISpecialty y) => y.Name).FirstOrDefault();
				return (specialty == null) ? null : ((x.Specialties != null) ? specialty.Name : "aaaaa");
			}).ToList();
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	public List<IResource> GetDctUpdateResourcesForEmail(int practiceAreaId)
	{
		List<string> isbns = GetDctUpdateResources(practiceAreaId);
		IQueryable<IResource> query = _resources.Where((IResource r) => isbns.Contains(r.Isbn));
		return query.ToList();
	}

	public List<IResource> GetPurchasedResourceEmailResources(int institutionId)
	{
		try
		{
			if (IsbnDictionary == null)
			{
				IsbnDictionary = GetResourceEmailResources("datePurchasedEmail");
			}
			IQueryable<IResource> query = from irl in _institutionResourceLicenses
				join r in _resources on irl.ResourceId equals r.Id
				where irl.InstitutionId == institutionId && IsbnDictionary.Keys.Contains(r.Isbn) && irl.LicenseTypeId != 3
				select r;
			List<IResource> resources = query.ToList();
			return resources.OrderBy(delegate(IResource x)
			{
				ISpecialty specialty = x.Specialties.OrderBy((ISpecialty y) => y.Name).FirstOrDefault();
				return (specialty == null) ? null : ((x.Specialties != null) ? specialty.Name : "z");
			}).ToList();
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	public List<IResource> GetNewEditionResourceEmailResources(int institutionId)
	{
		try
		{
			if (IsbnDictionary == null)
			{
				IsbnDictionary = GetResourceEmailResources("dateNewEditionEmail");
			}
			IQueryable<IResource> query = from r in _resources
				join oldR in _resources on r.Id equals oldR.LatestEditResourceId
				join irl in _institutionResourceLicenses on oldR.Id equals irl.ResourceId
				where irl.InstitutionId == institutionId && IsbnDictionary.Keys.Contains(r.Isbn) && irl.LicenseTypeId == 1 && irl.OriginalSourceId == 1
				select r;
			List<IResource> resources = query.Distinct().ToList();
			List<IResource> resourcesWithoutLicense = new List<IResource>();
			IQueryable<InstitutionResourceLicense> institutionResourceLicenses = _institutionResourceLicenses.Where((InstitutionResourceLicense x) => x.InstitutionId == institutionId);
			foreach (IResource resource in resources)
			{
				InstitutionResourceLicense license = institutionResourceLicenses.FirstOrDefault((InstitutionResourceLicense x) => x.ResourceId == resource.Id && (x.LicenseTypeId == 1 || x.LicenseTypeId == 3));
				if (license == null)
				{
					resourcesWithoutLicense.Add(resource);
				}
			}
			List<IResource> resourcesWithoutLicenseNotInCart = new List<IResource>();
			Cart cart = _carts.FetchMany((Cart x) => x.CartItems).SingleOrDefault((Cart x) => x.InstitutionId == institutionId && !x.Processed && (int)x.CartType == 1);
			if (cart != null)
			{
				foreach (IResource resource2 in resourcesWithoutLicense)
				{
					CartItem cartItem = cart.CartItems.FirstOrDefault((CartItem x) => x.ResourceId == resource2.Id);
					if (cartItem == null)
					{
						resourcesWithoutLicenseNotInCart.Add(resource2);
					}
				}
			}
			return (!resourcesWithoutLicenseNotInCart.Any()) ? null : resourcesWithoutLicenseNotInCart.OrderBy(delegate(IResource x)
			{
				ISpecialty specialty = x.Specialties.OrderBy((ISpecialty y) => y.Name).FirstOrDefault();
				return (specialty == null) ? null : ((x.Specialties != null) ? specialty.Name : "z");
			}).ToList();
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	public void UpdateNewResourceEmailResources()
	{
		UpdateInsertResourceEmailResources("dateNewResourceEmail");
	}

	public void UpdatePurchasedResourceEmailResources()
	{
		UpdateInsertResourceEmailResources("datePurchasedEmail");
	}

	public void UpdateNewEditionResourceEmailResources()
	{
		UpdateInsertResourceEmailResources("dateNewEditionEmail");
	}

	public List<int> GetPdaAddedToCartUserIds()
	{
		string sql = new StringBuilder().Append(" select u.iUserId ").Append(PdaAddedTables()).Append(" group by  u.iUserId ")
			.ToString();
		IList query = _unitOfWork.Session.CreateSQLQuery(sql).List();
		return query.Cast<int>().ToList();
	}

	public List<PdaResource> GetPdaAddedToCartResources(int userId)
	{
		string sql = new StringBuilder().Append("select r.iResourceId as [ResourceId], irl.dtCreationDate as [AddedDate], irl.dtPdaAddedToCartDate as [AddedToCartDate]").Append(" , ci.vchSpecialText as [PromotionText], ci.decDiscountPrice as [DiscountPrice], ci.decListPrice as [ListPrice]  ").Append(PdaAddedTables())
			.AppendFormat(" and u.iUserId = {0} ", userId)
			.Append("group by r.iResourceId, irl.dtPdaAddedToCartDate, irl.dtCreationDate, ci.vchSpecialText, ci.decDiscountPrice, ci.decListPrice ")
			.ToString();
		IList<PdaResource> pdaResources = GetEntityList<PdaResource>(sql, null, logSql: true);
		return pdaResources.ToList();
	}

	public int InsertPdaAddedToCartResourceEmail(int userId)
	{
		string sql = new StringBuilder().AppendFormat("Insert Into {0}..PdaResourceEmails (userid, resourceIsbn, dateEmailSent, type) ", _r2UtilitiesSettings.R2UtilitiesDatabaseName).Append("select u.iUserId, r.vchResourceISBN, getdate(), '1' ").Append(PdaAddedTables())
			.AppendFormat(" and u.iUserId = {0} ", userId)
			.Append("group by u.iUserId, r.vchResourceISBN ")
			.ToString();
		return ExecuteInsertStatementReturnRowCount(sql, null, logSql: true);
	}

	private string PdaAddedTables()
	{
		string formatted = DateTime.Now.AddDays(-_r2UtilitiesSettings.PdaAddedToCartNumberOfDaysAgo).ToShortDateString() + " 00:00:00";
		return new StringBuilder().Append("           from tUser u ").Append("           join tUserOptionValue uov on u.iUserId = uov.iUserId and uov.tiRecordStatus = 1 and uov.vchUserOptionValue = '1' ").Append("           join tUserOption uo on uov.iUserOptionId = uo.iUserOptionId and uo.tiRecordStatus = 1")
			.Append("           join tUserOptionRole uor on uo.iUserOptionId = uor.iUserOptionId and u.iRoleId = uor.iRoleId and uor.tiRecordStatus = 1")
			.Append("           join tInstitution i on u.iInstitutionId = i.iInstitutionId and i.tiRecordStatus = 1 ")
			.Append("           join tCart c on u.iInstitutionId = c.iInstitutionId and c.tiProcessed = 0 ")
			.AppendFormat("     join tCartItem ci on c.iCartId = ci.iCartId and ci.tiRecordStatus = 1 and ci.tiLicenseOriginalSourceId = {0} ", 2)
			.Append("           join tResource r on ci.iResourceId = r.iResourceId ")
			.AppendFormat("     join tInstitutionResourceLicense irl on i.iInstitutionId = irl.iInstitutionId and irl.tiRecordStatus = 1 and irl.tiLicenseOriginalSourceId = {0} and r.iResourceId = irl.iResourceId", 2)
			.AppendFormat("         left outer join {0}..PdaResourceEmails pre on r.vchResourceISBN = pre.resourceIsbn and pre.type = 1 and pre.userId = u.iUserId ", _r2UtilitiesSettings.R2UtilitiesDatabaseName)
			.AppendFormat("     where irl.dtPdaAddedToCartDate > '{0}' and uov.iUserOptionId = {1}  and pre.id is null", formatted, 8)
			.Append("           and u.tiRecordStatus = 1 and i.tiRecordStatus = 1 and (u.dtExpirationDate is null or u.dtExpirationDate > getdate()) ")
			.AppendFormat("     and (i.iInstitutionAcctStatusId = {0} or (i.iInstitutionAcctStatusId = {1} and i.dtTrialAcctEnd > GETDATE() ) ) ", 1, 2)
			.ToString();
	}

	public List<int> GetPdaRemovedFromCartUserIds()
	{
		string sql = new StringBuilder().Append(" select u.iUserId ").Append(PdaRemovedTables()).Append(" group by  u.iUserId ")
			.ToString();
		IList query = _unitOfWork.Session.CreateSQLQuery(sql).List();
		return query.Cast<int>().ToList();
	}

	public List<PdaResource> GetPdaRemovedFromCartResources(int userId)
	{
		string sql = new StringBuilder().Append("select r.iResourceId as [ResourceId], irl.dtCreationDate as [AddedDate], irl.dtPdaAddedToCartDate as [AddedToCartDate] ").Append(" , ci.vchSpecialText as [PromotionText], ci.decDiscountPrice as [DiscountPrice], ci.decListPrice as [ListPrice]  ").Append(PdaRemovedTables())
			.AppendFormat(" and u.iUserId = {0} ", userId)
			.Append("group by r.iResourceId, irl.dtPdaAddedToCartDate, irl.dtCreationDate, ci.vchSpecialText, ci.decDiscountPrice, ci.decListPrice ")
			.ToString();
		IList<PdaResource> pdaResources = GetEntityList<PdaResource>(sql, null, logSql: true);
		return pdaResources.ToList();
	}

	public int InsertPdaRemovedFromCartResourceEmail(int userId)
	{
		string sql = new StringBuilder().AppendFormat("Insert Into [{0}]..PdaResourceEmails (userid, resourceIsbn, dateEmailSent, type) ", _r2UtilitiesSettings.R2UtilitiesDatabaseName).Append("select u.iUserId, r.vchResourceISBN, getdate(), '2' ").Append(PdaRemovedTables())
			.AppendFormat(" and u.iUserId = {0} ", userId)
			.Append("group by u.iUserId, r.vchResourceISBN ")
			.ToString();
		return ExecuteInsertStatementReturnRowCount(sql, null, logSql: true);
	}

	private string PdaRemovedTables()
	{
		return new StringBuilder().Append("           from tUser u ").Append("           join tUserOptionValue uov on u.iUserId = uov.iUserId and uov.tiRecordStatus = 1 and uov.vchUserOptionValue = '1' ").Append("           join tUserOption uo on uov.iUserOptionId = uo.iUserOptionId and uo.tiRecordStatus = 1")
			.Append("           join tUserOptionRole uor on uo.iUserOptionId = uor.iUserOptionId and u.iRoleId = uor.iRoleId and uor.tiRecordStatus = 1")
			.Append("           join tInstitution i on u.iInstitutionId = i.iInstitutionId and i.tiRecordStatus = 1 ")
			.Append("           join tCart c on i.iInstitutionId = c.iInstitutionId and c.tiProcessed = 0 ")
			.AppendFormat("     join tCartItem ci on c.iCartId = ci.iCartId and ci.tiRecordStatus = 1 and ci.tiLicenseOriginalSourceId = {0} ", 2)
			.AppendFormat("     join tInstitutionResourceLicense irl on i.iInstitutionId = irl.iInstitutionId and irl.tiRecordStatus = 1 and irl.tiLicenseOriginalSourceId = {0} ", 2)
			.Append("           join tResource r on irl.iResourceId = r.iResourceId ")
			.AppendFormat("     left outer join {0}..PdaResourceEmails pre on r.vchResourceISBN = pre.resourceIsbn and pre.type = 2 ", _r2UtilitiesSettings.R2UtilitiesDatabaseName)
			.AppendFormat("     where dateadd(day, {0}, irl.dtPdaAddedToCartDate) <= GETDATE() and uov.iUserOptionId = {1}  and pre.id is null", _r2UtilitiesSettings.PdaRemovedFromCartNumberOfDays, 8)
			.Append("           and u.tiRecordStatus = 1 and i.tiRecordStatus = 1 and (u.dtExpirationDate is null or u.dtExpirationDate > getdate()) ")
			.AppendFormat("     and (i.iInstitutionAcctStatusId = {0} or (i.iInstitutionAcctStatusId = {1} and i.dtTrialAcctEnd > GETDATE() ) ) ", 1, 2)
			.ToString();
	}

	public IEnumerable<PdaHistoryResource> GetPdaHistoryResources(int institutionId)
	{
		Institution institution = _institutions.FirstOrDefault((Institution x) => x.Id == institutionId);
		AdminInstitution adminInstitution = new AdminInstitution(institution);
		List<PdaHistoryResource> collectionManagementResources = new List<PdaHistoryResource>();
		List<IResource> resources = GetPdaHistoryInstitutionResources(adminInstitution);
		collectionManagementResources.AddRange(resources.Select((IResource resource2) => new PdaHistoryResource(resource2)));
		List<Cart> savedCarts = GetSavedCartsForInstitution(institutionId);
		foreach (PdaHistoryResource pdaHistoryResource in collectionManagementResources)
		{
			PdaHistoryResource resource = pdaHistoryResource;
			List<License> resourceLicenses = adminInstitution.Licenses.Where((License x) => x.ResourceId == resource.Resource.Id).ToList();
			if (!resourceLicenses.Any())
			{
				continue;
			}
			License resourceLicense = resourceLicenses.First();
			if (resourceLicense == null)
			{
				continue;
			}
			pdaHistoryResource.LicenseType = resourceLicense.LicenseType;
			if (resourceLicense.LicenseType == LicenseType.Purchased)
			{
				pdaHistoryResource.LicenseCount = resourceLicense.LicenseCount;
				pdaHistoryResource.FirstPurchaseDate = resourceLicense.FirstPurchaseDate;
				if (resourceLicense.OriginalSource == LicenseOriginalSource.Pda)
				{
					pdaHistoryResource.PdaAddedDate = resourceLicense.PdaAddedDate;
					pdaHistoryResource.PdaAddedToCartDate = resourceLicense.PdaAddedToCartDate;
					pdaHistoryResource.PdaCartDeletedDate = resourceLicense.PdaCartDeletedDate;
					pdaHistoryResource.PdaCartDeletedByName = resourceLicense.PdaCartDeletedByName;
					pdaHistoryResource.PdaViewCount = resourceLicense.PdaViewCount;
					pdaHistoryResource.PdaMaxViews = resourceLicense.PdaMaxViews;
					pdaHistoryResource.ResourceNotSaleableDate = resource.Resource.NotSaleableDate;
				}
			}
			else if (resourceLicense.LicenseType == LicenseType.Pda)
			{
				pdaHistoryResource.LicenseCount = 0;
				pdaHistoryResource.PdaAddedDate = resourceLicense.PdaAddedDate;
				pdaHistoryResource.PdaAddedToCartDate = resourceLicense.PdaAddedToCartDate;
				DateTime? pdaCartDeletedDate = resourceLicense.PdaCartDeletedDate;
				string pdaCartDeletedByName = resourceLicense.PdaCartDeletedByName;
				if (!pdaCartDeletedDate.HasValue && resourceLicense.PdaAddedToCartDate.HasValue && resourceLicense.PdaAddedToCartDate.Value.AddMonths(1) < DateTime.Now)
				{
					pdaCartDeletedDate = resourceLicense.PdaAddedToCartDate.Value.AddMonths(1);
					if (string.IsNullOrWhiteSpace(pdaCartDeletedByName))
					{
						pdaCartDeletedByName = "Expired and automatically removed from cart";
					}
				}
				pdaHistoryResource.PdaCartDeletedDate = pdaCartDeletedDate;
				pdaHistoryResource.PdaCartDeletedByName = pdaCartDeletedByName;
				pdaHistoryResource.PdaViewCount = resourceLicense.PdaViewCount;
				pdaHistoryResource.PdaMaxViews = resourceLicense.PdaMaxViews;
				pdaHistoryResource.ResourceNotSaleableDate = resource.Resource.NotSaleableDate;
				foreach (Cart cart in savedCarts)
				{
					CartItem cartItem = cart.CartItems.FirstOrDefault((CartItem x) => x.ResourceId == pdaHistoryResource.ResourceId);
					if (cartItem != null)
					{
						DateTime savedDate = cart.ConvertDate.GetValueOrDefault();
						pdaHistoryResource.DateOrNameCartWasSaved = ((savedDate == DateTime.MinValue) ? cart.CartName : cart.ConvertDate.GetValueOrDefault().ToShortDateString());
						pdaHistoryResource.PdaCartDeletedByName = null;
						pdaHistoryResource.PdaCartDeletedDate = null;
					}
				}
			}
			pdaHistoryResource.OriginalSource = resourceLicense.OriginalSource;
		}
		return collectionManagementResources;
	}

	private static string PdaHistoryTables()
	{
		return new StringBuilder().Append("           from tUser u ").Append("           join tUserOptionValue uov on u.iUserId = uov.iUserId and uov.tiRecordStatus = 1 and uov.vchUserOptionValue = '1' ").Append("           join tUserOption uo on uov.iUserOptionId = uo.iUserOptionId and uo.tiRecordStatus = 1")
			.Append("           join tUserOptionRole uor on uo.iUserOptionId = uor.iUserOptionId and u.iRoleId = uor.iRoleId and uor.tiRecordStatus = 1")
			.Append("           join tInstitution i on u.iInstitutionId = i.iInstitutionId and i.tiRecordStatus = 1 ")
			.AppendFormat("     join tInstitutionResourceLicense irl on u.iInstitutionId = irl.iInstitutionId and irl.tiRecordStatus = 1 and irl.tiLicenseOriginalSourceId = {0} ", 2)
			.Append("           join tResource r on irl.iResourceId = r.iResourceId  and r.tiRecordStatus = 1")
			.Append("     where u.tiRecordStatus = 1 and i.tiRecordStatus = 1 and (u.dtExpirationDate is null or u.dtExpirationDate > getdate()) ")
			.AppendFormat("     and uov.iUserOptionId = {2} and (i.iInstitutionAcctStatusId = {0} or (i.iInstitutionAcctStatusId = {1} and i.dtTrialAcctEnd > GETDATE() ) ) ", 1, 2, 9)
			.ToString();
	}

	public List<IResource> GetResources(List<int> resourceIds)
	{
		List<IResource> resources = _resources.Where((IResource x) => resourceIds.Contains(x.Id)).ToList();
		return resources.OrderBy(delegate(IResource x)
		{
			ISpecialty specialty = x.Specialties.OrderBy((ISpecialty y) => y.Name).FirstOrDefault();
			return (specialty == null) ? null : ((x.Specialties != null) ? specialty.Name : "zzzz");
		}).ToList();
	}

	public List<IResource> GetResources()
	{
		return _resources.ToList();
	}

	public List<IFeaturedTitle> GetFeaturedTitles(DateTime currentDate, int count = 0)
	{
		IOrderedQueryable<IFeaturedTitle> featuredTitles = from x in _featuredTitles
			where x.StartDate < currentDate && x.EndDate > currentDate
			orderby x.EndDate descending
			select x;
		if (count > 0 && featuredTitles.Count() > count)
		{
			return featuredTitles.Take(count).ToList();
		}
		return featuredTitles.ToList();
	}

	public List<SpecialResource> GetSpecials(DateTime currentDate, int count = 0)
	{
		IOrderedQueryable<SpecialResource> specialResources = from x in _specialResources
			where x.Discount.Special.StartDate <= currentDate && x.Discount.Special.EndDate >= currentDate && x.RecordStatus && x.Discount.RecordStatus
			orderby x.Discount.Special.EndDate descending
			select x;
		if (count > 0 && specialResources.Count() > count)
		{
			return specialResources.Take(count).ToList();
		}
		return specialResources.ToList();
	}

	public List<Recommendation> GetRecommendations(int institutionId, int count)
	{
		IOrderedQueryable<Recommendation> query = from x in _recommendations.Fetch((Recommendation x) => x.RecommendedByUser).ThenFetch((IUser d) => d.Department).Fetch((Recommendation x) => x.PurchasedByUser)
				.ThenFetch((IUser d) => d.Department)
				.Fetch((Recommendation x) => x.AddedToCartByUser)
				.ThenFetch((IUser d) => d.Department)
				.Fetch((Recommendation x) => x.DeletedByUser)
				.ThenFetch((IUser d) => d.Department)
			where x.InstitutionId == institutionId
			where x.DeletedDate == null
			where x.RecordStatus
			where x.AddedToCartDate == null
			orderby x.CreationDate descending
			select x;
		if (count > 0 && query.Count() > count)
		{
			return query.Take(count).ToList();
		}
		return query.ToList();
	}

	public List<Institution> GetDashboardInstitutions()
	{
		IQueryable<int?> institutionIds = (from y in GetBaseUsers().HasOptionSelected(UserOptionCode.Dashboard)
			select y.InstitutionId).Distinct();
		return _institutions.Where((Institution x) => x.AccountStatusId == 1 && institutionIds.Contains(x.Id)).ToList();
	}

	public List<User> GetDashboardUsers(int institutionId)
	{
		return (from x in GetBaseUsers()
			where x.InstitutionId == (int?)institutionId && (int)x.Role.Code == 1
			select x).HasOptionSelected(UserOptionCode.Dashboard).ToList();
	}

	public IEnumerable<IResource> GetPdaInstitutionResources(AdminInstitution adminInstitution)
	{
		return from r in _resources
			join irl in _institutionResourceLicenses on r.Id equals irl.ResourceId
			where irl.InstitutionId == adminInstitution.Id && irl.LicenseTypeId == 3
			select r;
	}

	public List<IResource> GetPdaHistoryInstitutionResources(AdminInstitution adminInstitution)
	{
		List<InstitutionResourceLicense> licenses = _institutionResourceLicenses.Where((InstitutionResourceLicense x) => x.InstitutionId == adminInstitution.Id && x.OriginalSourceId == 2).ToList();
		List<int> resourceIds = licenses.Select((InstitutionResourceLicense x) => x.ResourceId).ToList();
		return GetPdaHistoryInstitutionResourcesByBatching(resourceIds);
	}

	private List<IResource> GetPdaHistoryInstitutionResourcesByBatching(List<int> resourceIds, int batchSize = 500)
	{
		List<IResource> resources = new List<IResource>();
		for (int i = 0; i < resourceIds.Count; i += batchSize)
		{
			List<int> batchIds = resourceIds.Skip(i).Take(batchSize).ToList();
			List<IResource> batchResources = _resources.Where((IResource x) => batchIds.Contains(x.Id)).ToList();
			resources.AddRange(batchResources);
		}
		return resources;
	}

	public User GetUser(int userId)
	{
		return GetBaseUsers().FirstOrDefault((User x) => x.Id == userId);
	}

	public List<User> GetTerritoryOwners(int territoryId)
	{
		return (from y in _userTerritories
			where y.TerritoryId == territoryId
			select y.User).ToList();
	}

	public List<int> GetArchivedEmailUserIds()
	{
		string sql = new StringBuilder().Append(" select u.iUserId ").Append(GetArchivedTables()).Append(" group by  u.iUserId ")
			.ToString();
		IList query = _unitOfWork.Session.CreateSQLQuery(sql).List();
		return query.Cast<int>().ToList();
	}

	public List<int> GetArchivedEmailResourceIds(int userId)
	{
		string sql = new StringBuilder().Append(" Select r.iResourceId ").Append(GetArchivedTables()).AppendFormat("and u.iUserId = {0}", userId)
			.Append(" group by  r.iResourceId ")
			.ToString();
		IList query = _unitOfWork.Session.CreateSQLQuery(sql).List();
		return query.Cast<int>().ToList();
	}

	private string GetArchivedTables()
	{
		return new StringBuilder().Append("           from tUser u ").Append("           join tUserOptionValue uov on u.iUserId = uov.iUserId and uov.tiRecordStatus = 1 and uov.vchUserOptionValue = '1' ").Append("           join tUserOption uo on uov.iUserOptionId = uo.iUserOptionId and uo.tiRecordStatus = 1 ")
			.Append("           join tUserOptionRole uor on uo.iUserOptionId = uor.iUserOptionId and u.iRoleId = uor.iRoleId and uor.tiRecordStatus = 1 ")
			.Append("           join tInstitution i on u.iInstitutionId = i.iInstitutionId and i.tiRecordStatus = 1")
			.Append("           join tInstitutionResourceLicense irl on i.iInstitutionId = irl.iInstitutionId and irl.tiRecordStatus = 1")
			.AppendFormat("     join tResource r on irl.iResourceId = r.iResourceId and r.tiRecordStatus = 1 and r.iResourceStatusId = {0}", 7)
			.AppendFormat("           join {0}..ResourceEmails re on r.vchResourceISBN = re.resourceISBN ", _r2UtilitiesSettings.R2UtilitiesDatabaseName)
			.AppendFormat("           where uov.iUserOptionId = {0} and re.dateArchivedEmail is null ", 10)
			.Append("           and u.tiRecordStatus = 1 and i.tiRecordStatus = 1 and (u.dtExpirationDate is null or u.dtExpirationDate > getdate()) ")
			.AppendFormat("     and (i.iInstitutionAcctStatusId = {0} or (i.iInstitutionAcctStatusId = {1} and i.dtTrialAcctEnd > GETDATE() ) ) ", 1, 2)
			.ToString();
	}

	public void UpdateArchivedResourceEmailResources(List<IResource> archivedResources)
	{
		string updateSql = "Update [" + _r2UtilitiesSettings.R2UtilitiesDatabaseName + "].[dbo].[ResourceEmails] set dateArchivedEmail = GetDate() Where [resourceISBN] = '[ISBN]'";
		StringBuilder sql = new StringBuilder();
		foreach (IResource archivedResource in archivedResources)
		{
			sql.Append((updateSql + "; ").Replace("[ISBN]", archivedResource.Isbn));
		}
		_unitOfWork.Session.CreateSQLQuery(sql.ToString()).List();
	}

	public List<ShoppingCartUser> GetShoppingCartUsers()
	{
		string sql = new StringBuilder().Append("select u.vchUserEmail, u.iUserId, u.vchUserName, i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum, c.iCartId ").Append("from tUser u ").Append("           join tUserOptionValue uov on u.iUserId = uov.iUserId and uov.tiRecordStatus = 1 and uov.vchUserOptionValue = '1' ")
			.Append("           join tUserOption uo on uov.iUserOptionId = uo.iUserOptionId and uo.tiRecordStatus = 1 ")
			.Append("           join tUserOptionRole uor on uo.iUserOptionId = uor.iUserOptionId and u.iRoleId = uor.iRoleId and uor.tiRecordStatus = 1 ")
			.Append("join tInstitution i on u.iInstitutionId = i.iInstitutionId and ")
			.Append("       ((i.iInstitutionAcctStatusId = 1) or ")
			.Append("       (i.iInstitutionAcctStatusId = 2 and dateadd(dd, 0, datediff(dd, 0, i.dtTrialAcctEnd))  > dateadd(dd, 0, datediff(dd, 0, getdate())))) ")
			.Append("join tCart c on i.iInstitutionId = c.iInstitutionId and c.tiRecordStatus = 1 and c.iCartTypeId = 1 ")
			.Append("join tCartItem ci on c.iCartId = ci.iCartId and ci.tiRecordStatus = 1  and c.tiProcessed = 0 ")
			.Append("join tResource r on r.iResourceId = ci.iResourceId and r.iResourceStatusId in (6,8)")
			.AppendFormat("where u.iRoleId = 1 and uov.iUserOptionId = {0} and r.tiRecordStatus = 1 ", 3)
			.Append(" and u.tiRecordStatus = 1 and i.tiRecordStatus = 1 and (u.dtExpirationDate is null or u.dtExpirationDate > getdate()) ")
			.Append("and ((ci.iResourceId is null and ci.tiInclude = 1 and ci.iProductId <> 1) or (ci.iResourceId > 0)) ")
			.Append("and ((ci.dtLastUpdate is not null)  ")
			.AppendFormat("and dateadd(dd, 0, datediff(dd, 0, ci.dtLastUpdate))='{0}' ", DateTime.Now.Date.AddDays(-7.0))
			.AppendFormat("or dateadd(dd, 0, datediff(dd, 0, ci.dtLastUpdate))='{0}' ", DateTime.Now.Date.AddDays(-15.0))
			.AppendFormat("or dateadd(dd, 0, datediff(dd, 0, ci.dtLastUpdate))='{0}' ", DateTime.Now.Date.AddMonths(-1))
			.Append("or (ci.dtLastUpdate is null) ")
			.AppendFormat("and dateadd(dd, 0, datediff(dd, 0, ci.dtCreationDate))='{0}' ", DateTime.Now.Date.AddDays(-7.0))
			.AppendFormat("or dateadd(dd, 0, datediff(dd, 0, ci.dtCreationDate))='{0}' ", DateTime.Now.Date.AddDays(-15.0))
			.AppendFormat("or dateadd(dd, 0, datediff(dd, 0, ci.dtCreationDate))='{0}') ", DateTime.Now.Date.AddMonths(-1))
			.Append("group by u.vchUserEmail, u.iUserId, u.vchUserName, i.iInstitutionId, i.vchInstitutionName, i.vchInstitutionAcctNum, c.iCartId ")
			.Append("order by i.iInstitutionId, c.iCartId ")
			.ToString();
		IList<ShoppingCartUser> shoppingCartUsers = GetEntityList<ShoppingCartUser>(sql, null, logSql: true);
		return shoppingCartUsers.ToList();
	}

	public void AddNewResourcesToCart(int institutionId, List<IResource> resources)
	{
		try
		{
			Institution institution = _institutions.FirstOrDefault((Institution institution2) => institution2.Id == institutionId);
			AdminInstitution adminInstitution = null;
			if (institution != null)
			{
				adminInstitution = new AdminInstitution(institution);
			}
			if (adminInstitution == null)
			{
				throw new Exception("Institution is null and cannot be");
			}
			Cart cart = _carts.FetchMany((Cart cart2) => cart2.CartItems).SingleOrDefault((Cart cart2) => cart2.InstitutionId == adminInstitution.Id && !cart2.Processed && (int)cart2.CartType == 1) ?? new Cart
			{
				InstitutionId = institutionId,
				Discount = adminInstitution.Discount
			};
			StringBuilder sql = new StringBuilder();
			if (cart.Id == 0)
			{
				sql.AppendFormat(CartInsert, cart.InstitutionId, cart.Discount);
				IList query = _unitOfWork.Session.CreateSQLQuery(sql.ToString()).List();
				if (query.First() != null)
				{
					int.TryParse(query.First().ToString(), out var x);
					cart.Id = x;
				}
				if (cart.Id == 0)
				{
					throw new Exception("Could not insert a new cart and return the id of the cart.");
				}
			}
			List<CollectionManagementResource> collectionManagementResources = resources.Select((IResource resource) => new CollectionManagementResource(resource, cart.Id)).ToList();
			foreach (CollectionManagementResource collectionManagementResource in collectionManagementResources)
			{
				_resourceDiscountService.SetDiscount(collectionManagementResource, adminInstitution);
			}
			StringBuilder resultsBuilder = new StringBuilder();
			List<CartItem> cartItems = cart.CartItems.ToList();
			foreach (CollectionManagementResource collectionManagementResource2 in collectionManagementResources)
			{
				if (!cartItems.Any() || cartItems.All((CartItem cartItem) => cartItem.ResourceId != collectionManagementResource2.Resource.Id))
				{
					sql = new StringBuilder().AppendFormat(CartItemInsert, cart.Id, collectionManagementResource2.Resource.Id, collectionManagementResource2.Resource.ListPrice, collectionManagementResource2.DiscountPrice);
					resultsBuilder.AppendFormat("{0},", collectionManagementResource2.Resource.Id);
				}
			}
			if (sql.Length > 0)
			{
				_unitOfWork.Session.CreateSQLQuery(sql.ToString()).List();
			}
			resultsBuilder.Remove(resultsBuilder.Length - 1, 1);
			_log.InfoFormat("InstitutionId: {0}, CartId: {1}, ResourceIds added: {2} ", institutionId, cart.Id, resultsBuilder.ToString());
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			throw;
		}
	}

	public bool UpdateSavedReportLastUpdate(DateTime lastUpdate, int reportId)
	{
		try
		{
			string sql = $" UPDATE tSavedReports SET [dtLastUpdate] = '{lastUpdate}' WHERE iReportId =  {reportId} ";
			_unitOfWork.Session.CreateSQLQuery(sql).List();
		}
		catch (Exception ex)
		{
			_log.Error(ex.Message, ex);
			return false;
		}
		return true;
	}

	public List<Specialty> GetSpecialties()
	{
		return _specialties.ToList();
	}

	public DateTime? GetAggregateInstitutionResourceStatisticsStartDate()
	{
		string sql = new StringBuilder().AppendFormat(" select max(institutionResourceStatisticsDate) from {0}.dbo.DailyInstitutionResourceStatisticsCount ", _r2UtilitiesSettings.R2ReportsDatabaseName).ToString();
		IList results = _unitOfWork.Session.CreateSQLQuery(sql).List();
		object dateString = results.FirstOrNull();
		if (dateString == null)
		{
			return null;
		}
		DateTime.TryParse(dateString.ToString(), out var date);
		R2UtilitiesBase.Log.InfoFormat("GetAggregateInstitutionResourceStatisticsStartDate StartDate: {0}", date.ToString("d"));
		return date;
	}

	public void SetRecommendationsAlertSentDate(IEnumerable<int> recommendationId)
	{
		StringBuilder sb = new StringBuilder();
		foreach (int id in recommendationId)
		{
			sb.Append("         Update tInstitutionRecommendation   ").Append("       set dtAlertSentDate = GetDate()     ").Append("       , vchUpdaterId = 'R2Utilities'      ")
				.Append("       , dtLastUpdate = GetDate()          ")
				.AppendFormat(" Where iRecommendationId = {0};      ", id)
				.AppendLine();
		}
		_unitOfWork.Session.CreateSQLQuery(sb.ToString()).List();
	}

	public List<Recommendation> GetRecommendations(int institutionId)
	{
		IQueryable<Recommendation> query = from x in _recommendations.Fetch((Recommendation x) => x.RecommendedByUser).ThenFetch((IUser d) => d.Department).Fetch((Recommendation x) => x.PurchasedByUser)
				.ThenFetch((IUser d) => d.Department)
				.Fetch((Recommendation x) => x.AddedToCartByUser)
				.ThenFetch((IUser d) => d.Department)
				.Fetch((Recommendation x) => x.DeletedByUser)
				.ThenFetch((IUser d) => d.Department)
			where x.InstitutionId == institutionId && x.PurchaseDate == null && x.AlertSentDate == null && x.DeletedDate == null
			select x;
		return query.ToList();
	}

	public IEnumerable<int> GetInstitutionIdsForFacultyRecommentations()
	{
		return (from i in _institutions
			join ir in _recommendations on i.Id equals ir.InstitutionId
			where ir.AlertSentDate == null && ir.RecordStatus && ir.PurchaseDate == null && ir.DeletedDate == null && i.ExpertReviewerUserEnabled
			select i.Id).Distinct();
	}

	public List<User> GetAnnualMaintenanceFeeUsers()
	{
		return (from x in GetBaseUsers()
			where x.Role != null && ((int)x.Role.Code == 6 || (int)x.Role.Code == 2) && (x.ExpirationDate < DateTime.Now || x.ExpirationDate == null) && x.RecordStatus
			select x).HasOptionSelected(UserOptionCode.AnnualMaintenanceFee).ToList();
	}

	public List<User> GetFacultyRecommendationUsers(int institutionId)
	{
		return (from x in GetBaseUsers()
			where x.Institution.Id == institutionId && ((int)x.Role.Code == 7 || (int)x.Role.Code == 1)
			select x).HasOptionSelected(UserOptionCode.ExpertReviewRecommend).ToList();
	}

	public User GetInstitutionAdministrator(int institutionId)
	{
		return (from x in GetBaseUsers()
			where x.Institution.Id == institutionId && (int)x.Role.Code == 1
			orderby x.CreationDate
			select x).FirstOrDefault();
	}

	private IQueryable<User> GetBaseUsers()
	{
		_users.FetchMany((User x) => x.UserTerritories).Fetch((User x) => x.Institution).Fetch((User x) => x.Role)
			.Fetch((User x) => x.Department)
			.ToFuture();
		_users.FetchMany((User x) => x.OptionValues).ThenFetch((UserOptionValue x) => x.Option).ThenFetch((UserOption x) => x.Type)
			.ToFuture();
		return _users;
	}

	public List<UserIdAndInstitutionId> GetPdaHistoryUserIds()
	{
		string sql = new StringBuilder().Append(" select u.iUserId as 'UserId', u.iInstitutionId  as 'InstitutionId'").Append(PdaHistoryTables()).Append(" group by  u.iUserId, u.iInstitutionId ")
			.ToString();
		IList query = _unitOfWork.Session.CreateSQLQuery(sql).List();
		IEnumerable<UserIdAndInstitutionId> userIdAndInstitutionIds = from object[] x in query
			select new UserIdAndInstitutionId
			{
				InstitutionId = (int)x[1],
				UserId = (int)x[0]
			};
		return userIdAndInstitutionIds.ToList();
	}

	public PdaHistoryReport GetPdaHistoryReport(int institutionId, List<PdaHistoryResource> pdaResources)
	{
		StringBuilder sql = new StringBuilder().Append("SELECT irl.iResourceId AS ResourceId, ").Append("SUM(dirsc.contentRetrievalCount) AS ContentRetrievalCount, ").Append("SUM(dirsc.tocRetrievalCount) AS TocRetrievalCount, ")
			.Append("SUM(dirsc.sessionCount) AS SessionCount, ")
			.Append("SUM(dirsc.printCount) AS PrintCount, ")
			.Append("SUM(dirsc.emailCount) AS EmailCount, ")
			.Append("SUM(dirsc.accessTurnawayCount) AS AccessTurnawayCount ")
			.Append("FROM tInstitutionResourceLicense irl ")
			.Append("INNER JOIN tresource r ON irl.iResourceId = r.iResourceId ")
			.Append("LEFT JOIN vDailyInstitutionResourceStatisticsCount dirsc ")
			.Append("ON irl.iInstitutionId = dirsc.institutionId ")
			.Append("AND irl.iResourceId = dirsc.resourceId ")
			.Append("AND dirsc.institutionResourceStatisticsDate >= irl.dtPdaAddedDate ")
			.Append("WHERE r.tiRecordStatus = 1 ")
			.Append("AND r.iResourceStatusId IN (6, 7) ")
			.Append("AND irl.tiRecordStatus = 1 ")
			.Append("AND irl.tiLicenseOriginalSourceId = 2 ")
			.Append("AND irl.iInstitutionId = :institutionId ")
			.Append("GROUP BY irl.iResourceId");
		int queryTimeoutSeconds = 180;
		IQuery query = _unitOfWork.Session.CreateSQLQuery(sql.ToString()).SetParameter("institutionId", institutionId).SetTimeout(queryTimeoutSeconds)
			.SetResultTransformer(Transformers.AliasToBean<PdaHistoryCount>());
		IList<PdaHistoryCount> pdaHistoryCounts = query.List<PdaHistoryCount>();
		if (pdaHistoryCounts.Any())
		{
			List<PdaHistoryCount> pdaHistoryCountsList = pdaHistoryCounts.ToList();
			foreach (PdaHistoryCount pdaHistoryCount in pdaHistoryCountsList)
			{
				PdaHistoryResource resource = pdaResources.FirstOrDefault((PdaHistoryResource x) => x.Resource.Id == pdaHistoryCount.ResourceId);
				pdaHistoryCount.SetResource(resource);
			}
			return new PdaHistoryReport
			{
				PdaHistoryCounts = pdaHistoryCountsList,
				InstitutionId = institutionId
			};
		}
		return null;
	}
}
