using R2V2.Infrastructure.Settings;

namespace R2Utilities.Infrastructure.Settings;

public class R2UtilitiesSettings : AutoSettings, IR2UtilitiesSettings
{
	public string EnvironmentName { get; set; }

	public string DefaultFromAddress { get; set; }

	public string DefaultFromAddressName { get; set; }

	public string EmailConfigDirectory { get; set; }

	public string R2UtilitiesDatabaseConnection { get; set; }

	public string R2DatabaseConnection { get; set; }

	public string R2ReportsConnection { get; set; }

	public string Ip2LocationConnection { get; set; }

	public string LogEventsConnection { get; set; }

	public int HtmlIndexerBatchSize { get; set; }

	public int HtmlIndexerMaxIndexBatches { get; set; }

	public int HtmlIndexerFragmentationLimit { get; set; }

	public bool HtmlIndexerForceCompression { get; set; }

	public int ResourceFileInsertBatchSize { get; set; }

	public string Ip2LocationWorkingFolder { get; set; }

	public string Ip2LocationTableName { get; set; }

	public string Ip2LocationDatabaseName { get; set; }

	public string[] IndexStoredFields { get; set; }

	public string[] IndexEnumerableFields { get; set; }

	public bool OrderBatchDescending { get; set; }

	public string AuthorTableName { get; set; }

	public string ResourceFileTableName { get; set; }

	public string DefaultSpecialtyCode { get; set; }

	public string DefaultPracticeAreaCode { get; set; }

	public string BookLoaderSourceRootDirectory { get; set; }

	public string BookLoaderImageDestinationDirectory { get; set; }

	public string ResourceValidationBaseUrl { get; set; }

	public bool EmailTestMode { get; set; }

	public int EmailTestNumberOfEmails { get; set; }

	public string PreludeDataLinkedServer { get; set; }

	public string R2UtilitiesDatabaseName { get; set; }

	public int PdaAddedToCartNumberOfDaysAgo { get; set; }

	public int PdaRemovedFromCartNumberOfDays { get; set; }

	public string R2ReportsDatabaseName { get; set; }

	public string R2DatabaseName { get; set; }

	public string TabersXmlPath { get; set; }

	public string TabersDictionaryConnection { get; set; }

	public string AggregateDailyCountFolder { get; set; }

	public string AggregateDailyCountFolderZipLocation { get; set; }

	public int AggregateDailyCountMonthsToGoBack { get; set; }

	public int AggregateDailyCommandTimeout { get; set; }

	public int AutoLicensesNumberOfLicenses { get; set; }

	public string GeoIPsApiKey { get; set; }

	public string SpecialIconBaseUrl { get; set; }

	public string CmsHtmlContentUrl { get; set; }

	public bool OverRideDashboardEmailQuickNotes { get; set; }

	public int UpdateInstitutionStatisticsPreviousDays { get; set; }

	public int DctUpdateEmailStartDaysAgo { get; set; }

	public int MaxActivityReportInstitutionDisplay { get; set; }

	public string ContentBackupDirectory { get; set; }

	public string TaskCompressionTempDirectory { get; set; }

	public string AuditFilesOnDiskBackupDirectory { get; set; }

	public int AuditXmlDateModifiedWindowInSeconds { get; set; }

	public string Ip2LocationDownloadUrl { get; set; }

	public string Ip2LocationFileDestinations { get; set; }

	public string EIsbnGetUrl { get; set; }

	public int EIsbnGetRequestCount { get; set; }

	public string UpdateTitleTaskXmlBackupLocation { get; set; }

	public string UpdateTitleTaskWorkingFolder { get; set; }

	public string RabbitMqReportUrl { get; set; }

	public string[] RabbitMqReportUserNameAndPassword { get; set; }

	public int AutoCartDeleteOlderInDays { get; set; }

	public string FindEbookRootFileLocation { get; set; }

	public string FindEbookFileExtensions { get; set; }

	public int FindEbookDaysAgo { get; set; }

	public string FindEbookRecipients { get; set; }

	public string FindEbookIsbnDbKey { get; set; }

	public string FindEbookUrl { get; set; }

	public string FindEbookFileExcludedFolders { get; set; }
}
