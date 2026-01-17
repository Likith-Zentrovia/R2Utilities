namespace R2Utilities.Infrastructure.Settings;

public interface ITermHighlightSettings
{
	string IndexLocation { get; set; }

	string OutputLocation { get; set; }

	string BackupLocation { get; set; }

	int BatchSize { get; set; }

	int MaxIndexBatches { get; set; }

	bool UpdateResourceStatus { get; set; }

	int MaxWordCountPerDataCall { get; set; }

	TermHighlightType TermHighlightType { get; }
}
