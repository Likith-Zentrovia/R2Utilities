using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.Core;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;

namespace R2Utilities.DataAccess.Terms;

public class TermResourceDataService : DataServiceBase
{
	private static readonly string SqlInactivateKeywordResource = new StringBuilder().Append("update tkeywordresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN ").ToString();

	private static readonly string SqlInactivateDiseaseResource = new StringBuilder().Append("update tdiseaseresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN ").ToString();

	private static readonly string SqlInactivateDiseaseSynonymResource = new StringBuilder().Append("update tdiseasesynonymresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN ").ToString();

	private static readonly string SqlInactivateDrugResource = new StringBuilder().Append("update tdrugresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN ").ToString();

	private static readonly string SqlInactivateDrugSynonymResource = new StringBuilder().Append("update tdrugsynonymresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN ").ToString();

	private static readonly string SqlAtoZIndexDelete = new StringBuilder().Append("delete from tAtoZIndex where vchResourceISBN = @ResourceISBN ").ToString();

	private static readonly string SqlAtoZIndexKeywordInsert = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey ").Append("                      , iResourceId, vchResourceISBN, vchChapterId, vchSectionId, iAtoZIndexTypeId) ").Append("    select k.iKeywordId, k.vchKeywordDesc, upper(substring(ltrim(rtrim(k.vchKeywordDesc)), 0, 2)) as AlphaKey ")
		.Append("         , r.iResourceId, kr.vchResourceISBN, kr.vchChapterId, kr.vchSectionId, 5 ")
		.Append("    from   tKeyword k ")
		.Append("     join  tKeywordResource kr on kr.iKeywordId = k.iKeywordId ")
		.Append("     join  tResource r on r.vchResourceISBN = kr.vchResourceISBN ")
		.Append("    where  r.iResourceId  = @ResourceId ")
		.Append("      and  k.tiRecordStatus = 1 and kr.tiRecordStatus = 1 ")
		.ToString();

	private static readonly string SqlAtoZIndexDiseaseInsert = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId,vchResourceISBN,vchChapterId,vchSectionId,iAtoZIndexTypeId) ").Append("    select dn.iDiseaseNameId AS Id, dn.vchDiseaseName as Name ").Append("         , upper(substring(ltrim(rtrim(dn.vchDiseaseName)), 0, 2)) as AlphaKey ")
		.Append("         , r.iResourceId, dr.vchResourceISBN, dr.vchChapterId, dr.vchSectionId, 1 ")
		.Append("    from tdiseasename dn ")
		.Append("        join tDiseaseResource dr on dr.iDiseaseNameId = dn.iDiseaseNameId ")
		.Append("        join dbo.tResource r on r.vchResourceISBN = dr.vchResourceISBN ")
		.Append("    where  r.iResourceId  = @ResourceId ")
		.Append("      and  dn.tiRecordStatus = 1 and dr.tiRecordStatus = 1 ")
		.ToString();

	private static readonly string SqlAtoZIndexDiseaseSynonymInsert = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId,vchResourceISBN,vchChapterId,vchSectionId,iAtoZIndexTypeId) ").Append("    select ds.iDiseaseSynonymId as Id, ds.vchDiseaseSynonym as Name ").Append("         , upper(substring(ltrim(rtrim(ds.vchDiseaseSynonym)), 0, 2)) as AlphaKey ")
		.Append("         , r.iResourceId, dsr.vchResourceISBN, dsr.vchChapterId, dsr.vchSectionId, 2 ")
		.Append("    from tdiseasesynonym ds ")
		.Append("        join dbo.tDiseaseSynonymResource dsr on dsr.iDiseaseSynonymId = ds.iDiseaseSynonymId ")
		.Append("        join dbo.tResource r on r.vchResourceISBN = dsr.vchResourceISBN ")
		.Append("    where  r.iResourceId  = @ResourceId ")
		.Append("      and  ds.tiRecordStatus = 1 and dsr.tiRecordStatus = 1 ")
		.ToString();

	private static readonly string SqlAtoZIndexDrugInsert = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId,vchResourceISBN,vchChapterId,vchSectionId,iAtoZIndexTypeId) ").Append("    select dl.iDrugListId as Id, dl.vchDrugName as Name ").Append("         , upper(substring(ltrim(rtrim(dl.vchDrugName)), 0, 2)) as AlphaKey ")
		.Append("        , r.iResourceId, dr.vchResourceISBN, dr.vchChapterId, dr.vchSectionId, 3 ")
		.Append("      from tDrugsList dl ")
		.Append("        join dbo.tDrugResource dr on dr.iDrugListId = dl.iDrugListId ")
		.Append("        join dbo.tResource r on r.vchResourceISBN = dr.vchResourceISBN ")
		.Append("    where  r.iResourceId  = @ResourceId ")
		.Append("      and  dl.tiRecordStatus = 1 and dr.tiRecordStatus = 1 ")
		.ToString();

	private static readonly string SqlAtoZIndexDrugSynonymInsert = new StringBuilder().Append("insert into tAtoZIndex (iParentTableId, vchName, chrAlphaKey, iResourceId,vchResourceISBN,vchChapterId,vchSectionId,iAtoZIndexTypeId) ").Append("    select ds.iDrugSynonymId as Id, ds.vchDrugSynonymName as Name ").Append("         , upper(substring(ltrim(rtrim(ds.vchDrugSynonymName)), 0, 2)) as AlphaKey ")
		.Append("         , r.iResourceId, dsr.vchResourceISBN, dsr.vchChapterId, dsr.vchSectionId, 4 ")
		.Append("      from tDrugSynonym ds ")
		.Append("        join dbo.tDrugSynonymResource dsr on dsr.iDrugSynonymId = ds.iDrugSynonymId ")
		.Append("        join dbo.tResource r on r.vchResourceISBN = dsr.vchResourceISBN ")
		.Append("    where  r.iResourceId  = @ResourceId ")
		.Append("      and  ds.tiRecordStatus = 1 and dsr.tiRecordStatus = 1 ")
		.ToString();

	private const bool LogSql = true;

	public int InsertTermResources(IEnumerable<TermResource> termResources)
	{
		Stopwatch stopwatch = new Stopwatch();
		SqlConnection cnn = null;
		SqlCommand command = null;
		try
		{
			StringBuilder insert = new StringBuilder();
			List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>();
			int x = 0;
			foreach (TermResource termResource in termResources)
			{
				insert.AppendFormat(termResource.SqlInsert, x);
				parameters.AddRange(termResource.ToParameters(x).ToList());
				x++;
			}
			cnn = GetConnection();
			command = GetSqlCommand(cnn, insert.ToString(), parameters);
			bool flag = true;
			LogCommandDebug(command);
			stopwatch.Start();
			int rows = command.ExecuteNonQuery();
			bool flag2 = true;
			stopwatch.Stop();
			FactoryBase.Log.DebugFormat("rows effected: {0}, insert time: {1}ms", rows, stopwatch.ElapsedMilliseconds);
			return rows;
		}
		catch (Exception ex)
		{
			LogCommandInfo(command);
			FactoryBase.Log.Error(ex.Message, ex);
			throw;
		}
		finally
		{
			DisposeConnections(cnn, command);
		}
	}

	public int InactivateTermResources(string resourceIsbn)
	{
		ISqlCommandParameter[] parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("ResourceISBN", resourceIsbn)
		}.ToArray();
		int count = ExecuteUpdateStatement(SqlInactivateKeywordResource, parameters, logSql: true);
		count += ExecuteUpdateStatement(SqlInactivateDiseaseResource, parameters, logSql: true);
		count += ExecuteUpdateStatement(SqlInactivateDiseaseSynonymResource, parameters, logSql: true);
		count += ExecuteUpdateStatement(SqlInactivateDrugResource, parameters, logSql: true);
		return count + ExecuteUpdateStatement(SqlInactivateDrugSynonymResource, parameters, logSql: true);
	}

	public int DeleteAtoZIndex(string resourceIsbn)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("ResourceISBN", resourceIsbn)
		};
		return ExecuteUpdateStatement(SqlAtoZIndexDelete, parameters.ToArray(), logSql: true);
	}

	public int InsertAtoZIndex(int resourceId)
	{
		ISqlCommandParameter[] parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("ResourceId", resourceId)
		}.ToArray();
		int count = ExecuteInsertStatementReturnRowCount(SqlAtoZIndexKeywordInsert, parameters, logSql: true);
		count += ExecuteInsertStatementReturnRowCount(SqlAtoZIndexDiseaseInsert, parameters, logSql: true);
		count += ExecuteInsertStatementReturnRowCount(SqlAtoZIndexDiseaseSynonymInsert, parameters, logSql: true);
		count += ExecuteInsertStatementReturnRowCount(SqlAtoZIndexDrugInsert, parameters, logSql: true);
		return count + ExecuteInsertStatementReturnRowCount(SqlAtoZIndexDrugSynonymInsert, parameters, logSql: true);
	}
}
