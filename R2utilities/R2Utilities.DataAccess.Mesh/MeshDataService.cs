using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Logging;

namespace R2Utilities.DataAccess.Mesh;

public class MeshDataService : DataServiceBase
{
	private static readonly string SqlDiseaseSelect = new StringBuilder().Append("select null Term, DescriptorName, cast(ScopeNote as varchar(max)) ScopeNote, TreeNumber ").Append("from MeshDiseaseTerms t ").Append("order by DescriptorName, TreeNumber ")
		.ToString();

	private static readonly string SqlDiseaseSynonymSelect = new StringBuilder().Append("select Term, DescriptorName, null ScopeNote, TreeNumber ").Append("from MeshDiseaseSynonymTerms ds ").Append("order by DescriptorName, TreeNumber ")
		.ToString();

	private readonly ILog<MeshDataService> _log;

	public MeshDataService(ILog<MeshDataService> log, IR2UtilitiesSettings r2UtilitiesSettings)
	{
		_log = log;
		base.ConnectionString = r2UtilitiesSettings.R2DatabaseConnection;
	}

	public IEnumerable<MeshTerm> SelectDiseaseTerms()
	{
		return SelectTerms(SqlDiseaseSelect);
	}

	public IEnumerable<MeshTerm> SelectDiseaseSynonymTerms()
	{
		return SelectTerms(SqlDiseaseSynonymSelect);
	}

	private IEnumerable<MeshTerm> SelectTerms(string meshTermSelect)
	{
		return GetEntityList<MeshTerm>(meshTermSelect, null, logSql: true);
	}
}
