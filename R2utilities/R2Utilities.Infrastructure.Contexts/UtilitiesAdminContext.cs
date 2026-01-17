using System;
using R2V2.Contexts;
using R2V2.Core.Admin;
using R2V2.Core.Institution;
using R2V2.Infrastructure.Storages;

namespace R2Utilities.Infrastructure.Contexts;

public class UtilitiesAdminContext : IAdminContext
{
	public const string AdminInstitutionKey = "Current.AdminInstitution";

	private readonly Func<InstitutionService> _institutionServiceFactory;

	private readonly Func<IUserSessionStorageService> _userSessionStorageServiceFactory;

	private InstitutionService InstitutionService => _institutionServiceFactory();

	private IUserSessionStorageService UserSessionStorageService => _userSessionStorageServiceFactory();

	public UtilitiesAdminContext(Func<InstitutionService> institutionServiceFactory, Func<IUserSessionStorageService> userSessionStorageServiceFactory)
	{
		_institutionServiceFactory = institutionServiceFactory;
		_userSessionStorageServiceFactory = userSessionStorageServiceFactory;
	}

	public IAdminInstitution GetAdminInstitution(int institutionId)
	{
		IAdminInstitution adminInstitution = UserSessionStorageService.Get<AdminInstitution>("Current.AdminInstitution");
		if (adminInstitution == null)
		{
			IInstitution institution = InstitutionService.GetInstitutionForAdminNotCached(institutionId);
			adminInstitution = new AdminInstitution(institution);
			UserSessionStorageService.Put("Current.AdminInstitution", adminInstitution);
		}
		return adminInstitution;
	}

	public void ReloadAdminInstitution(int institutionId, int userId)
	{
		throw new NotImplementedException();
	}
}
