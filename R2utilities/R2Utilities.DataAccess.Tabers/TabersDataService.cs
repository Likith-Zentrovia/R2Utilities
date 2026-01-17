using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R2Library.Data.ADO.Core.SqlCommandParameters;
using R2Utilities.DataAccess.Terms;
using R2Utilities.Infrastructure.Settings;

namespace R2Utilities.DataAccess.Tabers;

public class TabersDataService : TermDataService
{
	private static readonly string TermContentInsert = new StringBuilder().Append("insert into TermContent (Term, Content, SectionId) ").Append("values(@Term, @Content, @SectionId); ").ToString();

	private static readonly string MainEntryInsert = new StringBuilder().Append("insert into MainEntry (Currency, DateRevised, Name, EditionAdded, Letter, Output, SortOrder, SpaceSaver, XlinkType, OrthoDispKey, Biography, Abbrev, Symb) ").Append("values(@Currency, @DateRevised, @Name, @EditionAdded, @Letter, @Output, @SortOrder, @SpaceSaver, @XlinkType, @OrthoDispKey, @Biography, @Abbrev, @Symb); ").ToString();

	private static readonly string OrthoDispInsert = new StringBuilder().Append("insert into OrthoDisp (OrthoDispId, OrthoDispText, PluralKey) ").Append("values(@OrthoDispId, @OrthoDispText, @PluralKey); ").ToString();

	private static readonly string SenseInsert = new StringBuilder().Append("insert into Sense (MainEntryKey, Definition) ").Append("values(@MainEntryKey, @Definition); ").ToString();

	private static readonly string SpecialtyInsert = new StringBuilder().Append("insert into Specialty (MainEntryKey, Primary1Code) ").Append("values(@MainEntryKey, @Primary1Code); ").ToString();

	private static readonly string VariantInsert = new StringBuilder().Append("insert into Variant (MainEntryKey, OrthoDispKey) ").Append("values(@MainEntryKey, @OrthoDispKey); ").ToString();

	private static readonly string PronounceInsert = new StringBuilder().Append("insert into Pronounce (MainEntryKey, PronounceText, AudioFile) ").Append("values(@MainEntryKey, @PronounceText, @AudioFile); ").ToString();

	private static readonly string PluralInsert = new StringBuilder().Append("insert into Plural (MainEntryKey) ").Append("values(@MainEntryKey); ").ToString();

	private static readonly string EtymologyInsert = new StringBuilder().Append("insert into Etymology (MainEntryKey, EtymologyText) ").Append("values(@MainEntryKey, @EtymologyText); ").ToString();

	private static readonly string SubentryInsert = new StringBuilder().Append("insert into Subentry (MainEntryKey, Currency, DateRevised, Name, EditionAdded, Output, SpaceSaver, XlinkType) ").Append("values(@MainEntryKey, @Currency, @DateRevised, @Name, @EditionAdded, @Output, @SpaceSaver, @XlinkType); ").ToString();

	private static readonly string AbbrevXentryInsert = new StringBuilder().Append("insert into AbbrevXentry (MainEntryKey, SubentryKey, XlinkHref, AbbrevXentryText) ").Append("values(@MainEntryKey, @SubentryKey, @XlinkHref, @AbbrevXentryText); ").ToString();

	private static readonly string XentryInsert = new StringBuilder().Append("insert into Xentry (MainEntryKey, SubentryKey, XlinkHref, XentryText) ").Append("values(@MainEntryKey, @SubentryKey, @XlinkHref, @XentryText); ").ToString();

	private static readonly string DefExpInsert = new StringBuilder().Append("insert into DefExp (SenseKey, Output, DefExpText) ").Append("values(@SenseKey, @Output, @DefExpText); ").ToString();

	private static readonly string MainEntrySelect = new StringBuilder().Append("select Name ").Append("from MainEntry ").Append("where Name in ({0}) ")
		.ToString();

	private static readonly string TermContentSelect = new StringBuilder().Append("select Term ").Append("from TermContent ").Append("where {0} ")
		.Append("order by Term ")
		.ToString();

	public TabersDataService(IR2UtilitiesSettings r2UtilitiesSettings, TabersTermHighlightSettings tabersTermHighlightSettings)
		: base(tabersTermHighlightSettings, r2UtilitiesSettings.TabersDictionaryConnection)
	{
	}

	public int InsertTermContent(string term, string content, string sectionId)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("Term", term),
			new StringParameter("Content", content),
			new StringParameter("SectionId", sectionId)
		};
		return ExecuteInsertStatementReturnIdentity(TermContentInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertMainEntry(string currency, DateTime? dateRevised, string name, int? editionAdded, string letter, string output, int? sortOrder, string spaceSaver, string xlinkType, int orthoDispKey, string biography, string abbrev, string symb)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("Currency", currency),
			new DateTimeNullParameter("DateRevised", dateRevised),
			new StringParameter("Name", name),
			new Int32NullParameter("EditionAdded", editionAdded),
			new StringParameter("Letter", letter),
			new StringParameter("Output", output),
			new Int32NullParameter("SortOrder", sortOrder),
			new StringParameter("SpaceSaver", spaceSaver),
			new StringParameter("XlinkType", xlinkType),
			new Int32Parameter("OrthoDispKey", orthoDispKey),
			new StringParameter("Biography", biography),
			new StringParameter("Abbrev", abbrev),
			new StringParameter("Symb", symb)
		};
		return ExecuteInsertStatementReturnIdentity(MainEntryInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertOrthoDisp(string orthoDispId, string orthoDispText, int? pluralKey)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new StringParameter("OrthoDispId", orthoDispId),
			new StringParameter("OrthoDispText", orthoDispText),
			new Int32NullParameter("PluralKey", pluralKey)
		};
		return ExecuteInsertStatementReturnIdentity(OrthoDispInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertSense(int mainEntryKey, string definition)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MainEntryKey", mainEntryKey),
			new StringParameter("Definition", definition)
		};
		return ExecuteInsertStatementReturnIdentity(SenseInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertSpecialty(int mainEntryKey, string primary1Code)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MainEntryKey", mainEntryKey),
			new StringParameter("Primary1Code", primary1Code)
		};
		return ExecuteInsertStatementReturnIdentity(SpecialtyInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertVariant(int mainEntryKey, int orthoDispKey)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MainEntryKey", mainEntryKey),
			new Int32Parameter("OrthoDispKey", orthoDispKey)
		};
		return ExecuteInsertStatementReturnIdentity(VariantInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertPronounce(int mainEntryKey, string pronounceText, string audioFile)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MainEntryKey", mainEntryKey),
			new StringParameter("PronounceText", pronounceText),
			new StringParameter("AudioFile", audioFile)
		};
		return ExecuteInsertStatementReturnIdentity(PronounceInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertPlural(int mainEntryKey)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MainEntryKey", mainEntryKey)
		};
		return ExecuteInsertStatementReturnIdentity(PluralInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertEtymology(int mainEntryKey, string etymologyText)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MainEntryKey", mainEntryKey),
			new StringParameter("EtymologyText", etymologyText)
		};
		return ExecuteInsertStatementReturnIdentity(EtymologyInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertSubentry(int mainEntryKey, string currency, DateTime? dateRevised, string name, int? editionAdded, string output, string spaceSaver, string xlinkType)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("MainEntryKey", mainEntryKey),
			new StringParameter("Currency", currency),
			new DateTimeNullParameter("DateRevised", dateRevised),
			new StringParameter("Name", name),
			new Int32NullParameter("EditionAdded", editionAdded),
			new StringParameter("Output", output),
			new StringParameter("SpaceSaver", spaceSaver),
			new StringParameter("XlinkType", xlinkType)
		};
		return ExecuteInsertStatementReturnIdentity(SubentryInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertAbbrevXentry(int? mainEntryKey, int? subentryKey, string xlinkHref, string abbrevXentryText)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32NullParameter("MainEntryKey", mainEntryKey),
			new Int32NullParameter("SubentryKey", subentryKey),
			new StringParameter("XlinkHref", xlinkHref),
			new StringParameter("AbbrevXentryText", abbrevXentryText)
		};
		return ExecuteInsertStatementReturnIdentity(AbbrevXentryInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertXentry(int? mainEntryKey, int? subentryKey, string xlinkHref, string xentryText)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32NullParameter("MainEntryKey", mainEntryKey),
			new Int32NullParameter("SubentryKey", subentryKey),
			new StringParameter("XlinkHref", xlinkHref),
			new StringParameter("XentryText", xentryText)
		};
		return ExecuteInsertStatementReturnIdentity(XentryInsert, parameters.ToArray(), logSql: false);
	}

	public int InsertDefExp(int senseKey, string output, string defExpText)
	{
		List<ISqlCommandParameter> parameters = new List<ISqlCommandParameter>
		{
			new Int32Parameter("SenseKey", senseKey),
			new StringParameter("Output", output),
			new StringParameter("DefExpText", defExpText)
		};
		return ExecuteInsertStatementReturnIdentity(DefExpInsert, parameters.ToArray(), logSql: false);
	}

	public List<MainEntry> SelectMainEntries(List<string> names)
	{
		string nameString = string.Join(", ", names.Select((string n) => "'" + n + "'"));
		return GetEntityList<MainEntry>(string.Format(MainEntrySelect, nameString), null, logSql: false);
	}

	public override IEnumerable<TermToHighlight> SelectTermsToHighlight(HashSet<SearchTermItem> terms)
	{
		terms.RemoveWhere((SearchTermItem term) => term.SearchTerm == "is");
		return base.SelectTermsToHighlight(terms);
	}
}
