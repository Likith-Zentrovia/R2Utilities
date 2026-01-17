using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;

namespace R2Utilities.DataAccess.Terms;

public class DrugResource : TermResource
{
	public override string SqlInsert => new StringBuilder().Append("insert into tdrugresource (iDrugListId, vchResourceISBN, vchChapterId, vchSectionId, vchCreatorId, vchTitle, dtCreationDate) ").Append("values(@DrugListId_{0}, @ResourceISBN_{0}, @ChapterId_{0}, @SectionId_{0}, @CreatorId_{0}, @Title_{0}, getdate())  ").ToString();

	public override string SqlInactivate => new StringBuilder().Append("update tdrugresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN").ToString();

	public override void Populate(SqlDataReader reader)
	{
		base.Populate(reader);
		try
		{
			base.Id = GetInt32Value(reader, "iDrugResourceId", -1);
			base.TermId = GetInt32Value(reader, "iDrugListId", -1);
		}
		catch (Exception ex)
		{
			FactoryBase.Log.ErrorFormat(ex.Message, ex);
			throw;
		}
	}

	public override IEnumerable<ISqlCommandParameter> ToParameters(int x)
	{
		List<ISqlCommandParameter> result = base.ToParameters(x).ToList();
		result.Add(new Int32Parameter($"DrugResourceId_{x}", base.Id));
		result.Add(new Int32Parameter($"DrugListId_{x}", base.TermId));
		return result.ToArray();
	}
}
