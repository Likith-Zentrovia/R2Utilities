namespace R2Utilities.Infrastructure.Settings;

public interface IR2UtilitiesSettings
{
	string EnvironmentName { get; set; }

	string DefaultFromAddress { get; set; }

	string DefaultFromAddressName { get; set; }

	string EmailConfigDirectory { get; set; }

	string R2UtilitiesDatabaseConnection { get; set; }

	string R2DatabaseConnection { get; set; }

	string R2ReportsConnection { get; set; }

	string Ip2LocationConnection { get; set; }

	string LogEventsConnection { get; set; }

	int HtmlIndexerBatchSize { get; set; }

	int HtmlIndexerMaxIndexBatches { get; set; }

	int HtmlIndexerFragmentationLimit { get; set; }

	bool HtmlIndexerForceCompression { get; set; }

	int ResourceFileInsertBatchSize { get; set; }

	string Ip2LocationWorkingFolder { get; set; }

	string Ip2LocationTableName { get; set; }

	string Ip2LocationDatabaseName { get; set; }

	string[] IndexStoredFields { get; set; }

	string[] IndexEnumerableFields { get; set; }

	bool OrderBatchDescending { get; set; }

	string AuthorTableName { get; set; }

	string ResourceFileTableName { get; set; }

	string DefaultSpecialtyCode { get; set; }

	string DefaultPracticeAreaCode { get; set; }

	string BookLoaderSourceRootDirectory { get; set; }

	string BookLoaderImageDestinationDirectory { get; set; }

	string ResourceValidationBaseUrl { get; set; }

	bool EmailTestMode { get; set; }

	int EmailTestNumberOfEmails { get; set; }

	string PreludeDataLinkedServer { get; set; }

	string R2UtilitiesDatabaseName { get; set; }

	int PdaAddedToCartNumberOfDaysAgo { get; set; }

	int PdaRemovedFromCartNumberOfDays { get; set; }

	string R2ReportsDatabaseName { get; set; }

	string R2DatabaseName { get; set; }

	string TabersXmlPath { get; set; }

	string TabersDictionaryConnection { get; set; }

	string AggregateDailyCountFolder { get; set; }

	int AggregateDailyCountMonthsToGoBack { get; set; }

	int AggregateDailyCommandTimeout { get; set; }

	string AggregateDailyCountFolderZipLocation { get; set; }

	int AutoLicensesNumberOfLicenses { get; set; }

	string GeoIPsApiKey { get; set; }

	string SpecialIconBaseUrl { get; set; }

	string CmsHtmlContentUrl { get; set; }

	bool OverRideDashboardEmailQuickNotes { get; set; }

	int UpdateInstitutionStatisticsPreviousDays { get; set; }

	int DctUpdateEmailStartDaysAgo { get; set; }

	int MaxActivityReportInstitutionDisplay { get; set; }

	string ContentBackupDirectory { get; set; }

	string TaskCompressionTempDirectory { get; set; }

	string AuditFilesOnDiskBackupDirectory { get; set; }

	int AuditXmlDateModifiedWindowInSeconds { get; set; }

	string Ip2LocationDownloadUrl { get; set; }

	string Ip2LocationFileDestinations { get; set; }

	string EIsbnGetUrl { get; set; }

	int EIsbnGetRequestCount { get; set; }

	string UpdateTitleTaskXmlBackupLocation { get; set; }

	string UpdateTitleTaskWorkingFolder { get; set; }

	string RabbitMqReportUrl { get; set; }

	string[] RabbitMqReportUserNameAndPassword { get; set; }

	int AutoCartDeleteOlderInDays { get; set; }

	string FindEbookRootFileLocation { get; set; }

	string FindEbookFileExtensions { get; set; }

	int FindEbookDaysAgo { get; set; }

	string FindEbookRecipients { get; set; }

	string FindEbookIsbnDbKey { get; set; }

	string FindEbookUrl { get; set; }

	string FindEbookFileExcludedFolders { get; set; }
}
