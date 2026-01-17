using System.Data.SqlClient;
using R2Library.Data.ADO.Core;

namespace R2Utilities.DataAccess;

public class ShoppingCartUser : FactoryBase, IDataEntity
{
	public string Email { get; set; }

	public int UserId { get; set; }

	public int InstitutionId { get; set; }

	public string InstitutionName { get; set; }

	public string AccountNumber { get; set; }

	public int CartId { get; set; }

	public string UserName { get; set; }

	public void Populate(SqlDataReader reader)
	{
		Email = GetStringValue(reader, "vchUserEmail");
		UserId = GetInt32Value(reader, "iUserId", 0);
		InstitutionId = GetInt32Value(reader, "iInstitutionId", 0);
		InstitutionName = GetStringValue(reader, "vchInstitutionName");
		AccountNumber = GetStringValue(reader, "vchInstitutionAcctNum");
		CartId = GetInt32Value(reader, "iCartId", 0);
		UserName = GetStringValue(reader, "vchUserName");
	}
}
