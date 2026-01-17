using System.Text;
using R2V2.Core.R2Utilities;
using R2V2.Infrastructure.Email;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Email.EmailBuilders;

public class FindEBookEmailBuildService : EmailBuildBaseService
{
	public FindEBookEmailBuildService(ILog<EmailBuildBaseService> log, IEmailSettings emailSettings, IContentSettings contentSettings)
		: base(log, emailSettings, contentSettings)
	{
	}

	public void InitEmailTemplates()
	{
		SetTemplates("FindEBook_Body.html", "FindEBook_Publisher.html", includeUnsubscribe: false, "FindEBook_Publisher_Item.html");
	}

	public R2V2.Infrastructure.Email.EmailMessage BuildEmail(EBookReport eBookReport, string[] emails)
	{
		string bodyHtml = BuildBody(eBookReport);
		string messageHtml = GetEmailHtml(bodyHtml);
		if (string.IsNullOrWhiteSpace(messageHtml))
		{
			return null;
		}
		return BuildEmailMessage(emails, $"R2 Library eBook Report {eBookReport.EndDate:g}", messageHtml);
	}

	private string GetEmailHtml(string bodyHtml)
	{
		return BuildMainHtml("Found eBook Report", bodyHtml, null);
	}

	private string BuildSubItemHtml(EBookFile eBookFile, bool colorRow = false)
	{
		string bgColor = "style='background-color: #f7f7f7'";
		if (eBookFile.Details != null)
		{
			string detailRow = "\n                    <tr>\n                        <td class='details' colspan='4' " + (colorRow ? bgColor : "") + "><b>Title:</b> " + eBookFile.Details.Title + "</td>\n                        <td class='details' colspan='2' " + (colorRow ? bgColor : "") + "><b>Publication Date:</b> " + eBookFile.Details.DatePublished + "</td>\n                        <td class='details' " + (colorRow ? bgColor : "") + "><b>Language:</b> " + eBookFile.Details.Language + "</td>\n                    </tr>\n";
			return base.SubItemTemplate.Replace("{FileName}", eBookFile.Name ?? "").Replace("{Extensions}", eBookFile.GetExtensionString() ?? "").Replace("{Date}", $"{eBookFile.CreateTime:yyyy-MM-dd hh:mm:ss}")
				.Replace("{Path}", eBookFile.GetPathString() ?? "")
				.Replace("{GoogleBooks}", eBookFile.NameAsIsbn ? ("<a href='" + eBookFile.GoogleBooksUrl() + "'>Google Books</a>") : "")
				.Replace("{Google}", eBookFile.NameAsIsbn ? ("<a href='" + eBookFile.GoogleSearchUrl() + "'>Google</a>") : "")
				.Replace("{Amazon}", eBookFile.NameAsIsbn ? ("<a href='" + eBookFile.AmazonSearchUrl() + "'>Amazon</a>") : "")
				.Replace("{Details}", detailRow ?? "")
				.Replace("{CSS_Style}", (colorRow ? bgColor : "") ?? "");
		}
		return base.SubItemTemplate.Replace("{FileName}", eBookFile.Name ?? "").Replace("{Date}", $"{eBookFile.CreateTime:yyyy-MM-dd hh:mm:ss}").Replace("{Extensions}", eBookFile.GetExtensionString() ?? "")
			.Replace("{Path}", eBookFile.GetPathString() ?? "")
			.Replace("{GoogleBooks}", eBookFile.NameAsIsbn ? ("<a href='" + eBookFile.GoogleBooksUrl() + "'>Google Books</a>") : "")
			.Replace("{Google}", eBookFile.NameAsIsbn ? ("<a href='" + eBookFile.GoogleSearchUrl() + "'>Google</a>") : "")
			.Replace("{Amazon}", eBookFile.NameAsIsbn ? ("<a href='" + eBookFile.AmazonSearchUrl() + "'>Amazon</a>") : "")
			.Replace("{Details}", "")
			.Replace("{CSS_Style}", (colorRow ? bgColor : "") ?? "");
	}

	private string BuildItemHtml(EBookPublisher eBookPublisher, StringBuilder subItemBuilder)
	{
		return base.ItemTemplate.Replace("{Publisher_Name}", eBookPublisher.Publisher).Replace("{Publisher_File_Count}", $"{eBookPublisher.FileCount}").Replace("{Publisher_Items}", subItemBuilder.ToString());
	}

	private string BuildBody(EBookReport eBookReport)
	{
		StringBuilder itemBuilder = new StringBuilder();
		eBookReport.PublisherFiles.ForEach(delegate(EBookPublisher x)
		{
			StringBuilder subItemBuilder = new StringBuilder();
			int counter = 0;
			x.Files.ForEach(delegate(EBookFile y)
			{
				subItemBuilder.Append(BuildSubItemHtml(y, counter % 2 != 0));
				counter++;
			});
			itemBuilder.Append(BuildItemHtml(x, subItemBuilder));
		});
		return base.BodyTemplate.Replace("{Report_StartDate}", $"{eBookReport.StartDate:yyyy-MM-dd hh:mm:ss}").Replace("{Report_EndDate}", $"{eBookReport.EndDate:yyyy-MM-dd hh:mm:ss}").Replace("{Publisher_Counts}", $"{eBookReport.PublisherCount}")
			.Replace("{Title_Counts}", $"{eBookReport.TitleCount}")
			.Replace("{Publishers}", itemBuilder.ToString());
	}
}
