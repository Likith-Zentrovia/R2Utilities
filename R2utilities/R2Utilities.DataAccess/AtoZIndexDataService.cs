using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;

namespace R2Utilities.DataAccess;

public class AtoZIndexDataService : DataServiceBase
{
	public int DeleteAtoZIndexRecordsForResource(int resourceId)
	{
		string sql = "delete from tAtoZIndex where iResourceId = @ResourceId;";
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return ExecuteUpdateStatement(sql, parameters, logSql: true);
	}

	public int InsertDrugNameIntoAtoZIndexForResource(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId, vchResourceISBN, vchChapterId, vchSectionId, iAtoZIndexTypeId) ").Append("select dn.iDiseaseNameId as [Id], dn.vchDiseaseName as [Name] ").Append("     , upper(substring(ltrim(rtrim(dn.vchDiseaseName)), 0, 2)) as AlphaKey ")
			.Append("     , r.iResourceId, r.vchResourceISBN, dr.vchChapterId, dr.vchSectionId, 1 ")
			.Append("from   dbo.tdiseasename dn ")
			.Append(" join  dbo.tDiseaseResource dr on dr.iDiseaseNameId = dn.iDiseaseNameId ")
			.Append(" join  dbo.tResource r on r.vchResourceISBN = dr.vchResourceISBN ")
			.Append("  and  r.iResourceId = @ResourceId; ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return ExecuteUpdateStatement(sql.ToString(), parameters, logSql: true);
	}

	public int InsertDrugNameSynonymsIntoAtoZIndexForResource(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId, vchResourceISBN, vchChapterId, vchSectionId, iAtoZIndexTypeId) ").Append("select ds.iDiseaseSynonymId as Id, ds.vchDiseaseSynonym as Name ").Append("     , upper(substring(ltrim(rtrim(ds.vchDiseaseSynonym)), 0, 2)) as AlphaKey ")
			.Append("     , r.iResourceId, r.vchResourceISBN, dsr.vchChapterId, dsr.vchSectionId, 2 ")
			.Append("from   dbo.tdiseasesynonym ds ")
			.Append(" join  dbo.tDiseaseSynonymResource dsr on dsr.iDiseaseSynonymId = ds.iDiseaseSynonymId ")
			.Append(" join  dbo.tResource r on r.vchResourceISBN = dsr.vchResourceISBN ")
			.Append("  and  r.iResourceId = @ResourceId; ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return ExecuteUpdateStatement(sql.ToString(), parameters, logSql: true);
	}

	public int InsertDiseaseNamesIntoAtoZIndexForResource(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId, vchResourceISBN, vchChapterId, vchSectionId, iAtoZIndexTypeId) ").Append("select dl.iDrugListId as [Id], dl.vchDrugName as [Name] ").Append("     , upper(substring(ltrim(rtrim(dl.vchDrugName)), 0, 2)) as AlphaKey ")
			.Append("     , r.iResourceId, r.vchResourceISBN, dr.vchChapterId, dr.vchSectionId, 3 ")
			.Append("from   dbo.tDrugsList dl ")
			.Append(" join  dbo.tDrugResource dr on dr.iDrugListId = dl.iDrugListId ")
			.Append(" join  dbo.tResource r on r.vchResourceISBN = dr.vchResourceISBN ")
			.Append("  and  r.iResourceId = @ResourceId; ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return ExecuteUpdateStatement(sql.ToString(), parameters, logSql: true);
	}

	public int InsertDiseaseSynonymsIntoAtoZIndexForResource(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId, vchResourceISBN, vchChapterId, vchSectionId, iAtoZIndexTypeId) ").Append("select ds.iDrugSynonymId as [Id], ds.vchDrugSynonymName as [Name] ").Append("     , upper(substring(ltrim(rtrim(ds.vchDrugSynonymName)), 0, 2)) as AlphaKey ")
			.Append("     , r.iResourceId, r.vchResourceISBN, dsr.vchChapterId, dsr.vchSectionId, 4 ")
			.Append("from   dbo.tDrugSynonym ds ")
			.Append(" join  dbo.tDrugSynonymResource dsr on dsr.iDrugSynonymId = ds.iDrugSynonymId ")
			.Append(" join  dbo.tResource r on r.vchResourceISBN = dsr.vchResourceISBN ")
			.Append("  and  r.iResourceId = @ResourceId; ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return ExecuteUpdateStatement(sql.ToString(), parameters, logSql: true);
	}

	public int InsertKeywordsIntoAtoZIndexForResource(int resourceId)
	{
		StringBuilder sql = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId, vchResourceISBN, vchChapterId, vchSectionId, iAtoZIndexTypeId) ").Append("select k.iKeywordId as [Id], k.vchKeywordDesc as [Name] ").Append("     , upper(substring(ltrim(rtrim(k.vchKeywordDesc)), 0, 2)) as AlphaKey ")
			.Append("     , r.iResourceId, r.vchResourceISBN, kr.vchChapterId, kr.vchSectionId, 5 ")
			.Append("from   dbo.tKeyword k ")
			.Append(" join  dbo.tKeywordResource kr on kr.iKeywordId = k.iKeywordId ")
			.Append(" join  dbo.tResource r on r.vchResourceISBN = kr.vchResourceISBN ")
			.Append("  and  r.iResourceId = @ResourceId; ");
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		};
		return ExecuteUpdateStatement(sql.ToString(), parameters, logSql: true);
	}
}
