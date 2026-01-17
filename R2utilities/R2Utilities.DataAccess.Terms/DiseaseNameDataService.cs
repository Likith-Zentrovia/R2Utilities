using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2Utility.DataServices;
using R2Utilities.DataAccess.Mesh;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.DataAccess.Terms;

public class DiseaseNameDataService : DataServiceBase
{
	private static readonly string SqlSelectAll = new StringBuilder().Append("select iDiseaseNameId, vchDiseaseName, vchDiseaseDesc, vchDiseaseUrl, vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate, tiRecordStatus, iParentDiseaseNameId, vchRelationName ").Append("from tdiseasename ").Append("where vchRelationName LIKE 'C%' ")
		.Append("{0} ")
		.ToString();

	private static readonly string SqlInsert = new StringBuilder().Append("insert into tdiseasename (vchDiseaseName, vchDiseaseDesc, vchDiseaseUrl, vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate, tiRecordStatus, iParentDiseaseNameId, ").Append("\t\t\t\t\t\t\tvchRelationName) ").Append("values(@DiseaseName, @DiseaseDesc, @DiseaseUrl, @CreatorId, @CreationDate, @UpdaterId, @LastUpdate, @RecordStatus, @ParentDiseaseNameId, @RelationName)  ")
		.ToString();

	private static readonly string SqlUpdate = new StringBuilder().Append("update tdiseasename ").Append("set vchDiseaseName = @DiseaseName, vchDiseaseDesc = @DiseaseDesc, vchDiseaseUrl = @DiseaseUrl, vchCreatorId = @CreatorId, dtCreationDate = @CreationDate, ").Append("\tvchUpdaterId = @UpdaterId, dtLastUpdate = @LastUpdate, tiRecordStatus = @RecordStatus, iParentDiseaseNameId = @ParentDiseaseNameId, vchRelationName = @RelationName ")
		.Append("where iDiseaseNameId = @DiseaseNameId ")
		.ToString();

	private static readonly string SqlInactivateNonMesh = new StringBuilder().Append("update tdiseasename ").Append("set tiRecordStatus = 0 ").Append("where tiRecordStatus = 1 and vchRelationName like 'C%' and not exists ( ")
		.Append("\tselect * ")
		.Append("\tfrom MeshDiseaseTerms mdt ")
		.Append("\twhere mdt.DescriptorName = vchDiseaseName AND mdt.TreeNumber = vchRelationName ")
		.Append(") ")
		.ToString();

	public IEnumerable<MeshTerm> MeshTerms { get; set; }

	public DiseaseNames DiseaseNames { get; protected set; }

	public DiseaseNameDataService(IR2UtilitiesSettings r2UtilitiesSettings)
	{
		base.ConnectionString = r2UtilitiesSettings.R2DatabaseConnection;
	}

	public int UpdateDiseases(string taskName)
	{
		DiseaseNames = SelectAll();
		int count = 0;
		foreach (MeshTerm meshTerm in MeshTerms)
		{
			DiseaseName diseaseName = DiseaseNames.Find(meshTerm);
			if (diseaseName == null)
			{
				Insert(DiseaseName.CreateFrom(meshTerm, taskName));
			}
			else
			{
				diseaseName.UpdateFrom(meshTerm, taskName);
				if (!diseaseName.IsChanged)
				{
					continue;
				}
				Update(diseaseName);
			}
			count++;
		}
		return count;
	}

	public int InactivateNonMeshDiseases()
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
		return ExecuteUpdateStatement(SqlInactivateNonMesh, parameters.ToArray(), logSql: true);
	}

	public int UpdateParentDiseaseIds(string taskName)
	{
		DiseaseNames = SelectAll(activeOnly: true);
		int count = 0;
		foreach (DiseaseName diseaseName in DiseaseNames.Values)
		{
			string parentTreeNumber = MeshTerm.ParentTreeNumber(diseaseName.RelationName);
			int? parentDiseaseNameId = ((parentTreeNumber == null) ? null : DiseaseNames.Values.Single((DiseaseName d) => d.RelationName == parentTreeNumber))?.Id ?? diseaseName.Id;
			if (diseaseName.ParentDiseaseNameId != parentDiseaseNameId)
			{
				diseaseName.ParentDiseaseNameId = parentDiseaseNameId;
				diseaseName.UpdaterId = taskName;
				Update(diseaseName);
				count++;
			}
		}
		return count;
	}

	private DiseaseNames SelectAll(bool activeOnly = false)
	{
		string whereClause = (activeOnly ? "and tiRecordStatus = 1 " : "");
		IEnumerable<DiseaseName> diseaseNames = GetEntityList<DiseaseName>(string.Format(SqlSelectAll, whereClause), null, logSql: true);
		return new DiseaseNames(diseaseNames);
	}

	private void Insert(DiseaseName diseaseName)
	{
		ExecuteInsertStatementReturnIdentity(SqlInsert, diseaseName.ToParameters(), logSql: true);
	}

	private void Update(DiseaseName diseaseName)
	{
		ExecuteUpdateStatement(SqlUpdate, diseaseName.ToParameters(), logSql: true);
	}
}
