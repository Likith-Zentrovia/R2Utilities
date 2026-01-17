using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;

namespace R2Utilities.DataAccess.Terms;

public class KeywordResource : TermResource
{
	public override string SqlInsert => new StringBuilder().Append("insert into tkeywordresource (iKeywordId, vchResourceISBN, vchChapterId, vchSectionId, vchCreatorId, tiRecordStatus, dtCreationDate) ").Append("values(@KeywordId_{0}, @ResourceISBN_{0}, @ChapterId_{0}, @SectionId_{0}, @CreatorId_{0}, 1, getdate()) ").ToString();

	public override string SqlInactivate => new StringBuilder().Append("update tkeywordresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN").ToString();

	public override void Populate(SqlDataReader reader)
	{
		base.Populate(reader);
		try
		{
			base.Id = GetInt32Value(reader, "iKeywordResourceId", -1);
			base.TermId = GetInt32Value(reader, "iKeywordId", -1);
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
		result.Add(new Int32Parameter($"KeywordResourceId_{x}", base.Id));
		result.Add(new Int32Parameter($"KeywordId_{x}", base.TermId));
		return result.ToArray();
	}
}
