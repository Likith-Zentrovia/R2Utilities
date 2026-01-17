using System;
using System.Collections.Generic;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Library.Data.ADO.R2.DataServices;

namespace R2Utilities.Tasks.ContentTasks.AhfsDrugMonograph;

public class AhfsDrugDataService : DataServiceBase
{
	private static readonly string DrugInsert = new StringBuilder().Append("insert into tAhfsDrug (iUnitNumber, vchFullName, vchShortName, vchClassNumber, vchClassText ").Append("    , vchXmlFileName, vchIntroduction, vchCreatorId, dtCreationDate, vchUpdaterId, dtLastUpdate ").Append("    , tiRecordStatus) ")
		.Append("values (@UnitNumber, @FullName, @ShortName, @ClassNumber, @ClassText ")
		.Append("    , @XmlFileName, @Introduction, @CreatorId, @CreationDate, @UpdaterId, @LastUpdate ")
		.Append("    , @RecordStatus) ")
		.ToString();

	private static readonly string DrugSynonymInsert = new StringBuilder().Append("insert into tAhfsDrugSynonym (iAhfsDrugId, vchSynonym, tiAhfsDrugSynonymTypeId) ").Append("values (@AhfsDrugId, @Synonym, @AhfsDrugSynonymTypeId) ").ToString();

	public int Insert(AhfsDrug drug)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("UnitNumber", Convert.ToInt32(drug.UnitNumber)),
			new StringParameter("FullName", drug.FullTitle),
			new StringParameter("ShortName", drug.ShortTitle),
			new StringParameter("ClassNumber", drug.ClassNumber),
			new StringParameter("ClassText", drug.ClassText),
			new StringParameter("XmlFileName", drug.XmlFileName),
			new StringParameter("Introduction", drug.Introduction),
			new StringParameter("CreatorId", "r2Utilities"),
			new DateTimeParameter("CreationDate", DateTime.Now),
			new StringParameter("UpdaterId", null),
			new DateTimeNullParameter("LastUpdate", null),
			new Int16Parameter("RecordStatus", 1)
		};
		int id = ExecuteInsertStatementReturnIdentity(DrugInsert, parameters, logSql: true);
		InsertSynonyms(id, drug.Synonyms, 1);
		InsertSynonyms(id, drug.PrintNames, 2);
		InsertSynonyms(id, drug.GenericNames, 3);
		InsertSynonyms(id, drug.ChecmicalNames, 4);
		return id;
	}

	private int InsertSynonyms(int id, IEnumerable<string> terms, short typeId)
	{
		int totalRows = 0;
		foreach (string term in terms)
		{
			List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
			{
				new Int32Parameter("AhfsDrugId", id),
				new StringParameter("Synonym", term),
				new Int16Parameter("AhfsDrugSynonymTypeId", 1)
			};
			totalRows += ExecuteInsertStatementReturnRowCount(DrugSynonymInsert, parameters.ToArray(), logSql: true);
		}
		return totalRows;
	}
}
