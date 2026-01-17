using R2V2.Infrastructure.Settings;

namespace R2Utilities.Infrastructure.Settings;

public class IndexTermHighlightSettings : AutoSettings, ITermHighlightSettings
{
	public string IndexLocation { get; set; }

	public string OutputLocation { get; set; }

	public string BackupLocation { get; set; }

	public int BatchSize { get; set; }

	public int MaxIndexBatches { get; set; }

	public bool UpdateResourceStatus { get; set; }

	public int MaxWordCountPerDataCall { get; set; }

	public TermHighlightType TermHighlightType => TermHighlightType.IndexTerms;
}
