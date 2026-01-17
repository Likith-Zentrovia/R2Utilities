using System.Text;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.DataAccess.Terms;

public class IndexTermDataService : TermDataService
{
	private static readonly string SqlDiseaseResourceInsert = new StringBuilder().Append("insert into tdiseaseresource (iDiseaseNameId, vchResourceISBN, vchChapterId, vchSectionId, vchCreatorId) ").Append("values(@DiseaseNameId, @ResourceISBN, @ChapterId, @SectionId, @CreatorId)  ").ToString();

	private static readonly string SqlDiseaseResourceInactivate = new StringBuilder().Append("update tdiseaseresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN").ToString();

	private static readonly string SqlDrugResourceInsert = new StringBuilder().Append("insert into tdrugresource (iDrugListId, vchResourceISBN, vchChapterId, vchSectionId, vchCreatorId, vchTitle) ").Append("values(@DrugListId, @ResourceISBN, @ChapterId, @SectionId, @CreatorId, @vchTitle)  ").ToString();

	private static readonly string SqlDrugResourceInactivate = new StringBuilder().Append("update tdrugresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN").ToString();

	private static readonly string SqlDrugSynonymResourceInsert = new StringBuilder().Append("insert into tdrugresource (iDrugSynonymId, vchResourceISBN, vchChapterId, vchSectionId, vchCreatorId, vchTitle) ").Append("values(@DrugSynonymId, @ResourceISBN, @ChapterId, @SectionId, @CreatorId, @vchTitle)  ").ToString();

	private static readonly string SqlDrugSynonymResourceInactivate = new StringBuilder().Append("update tdrugsynonymresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN").ToString();

	private static readonly string SqlKeywordResourceInsert = new StringBuilder().Append("insert into tdrugresource (iKeywordId, vchResourceISBN, vchChapterId, vchSectionId, vchCreatorId) ").Append("values(@KeywordId, @ResourceISBN, @ChapterId, @SectionId, @CreatorId)  ").ToString();

	private static readonly string SqlKeywordResourceInactivate = new StringBuilder().Append("update tdrugsynonymresource set tiRecordStatus = 0 where vchResourceISBN = @ResourceISBN").ToString();

	public IndexTermDataService(IR2UtilitiesSettings r2UtilitiesSettings, IndexTermHighlightSettings indexTermHighlightSettings)
		: base(indexTermHighlightSettings, r2UtilitiesSettings.R2DatabaseConnection)
	{
	}

	private void InsertDiseaseResource(DiseaseResource diseaseResource)
	{
	}
}
