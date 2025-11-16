using System.IO;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Announcements;

public class ReleaseHighlights
{
	public ReleaseHighlights()
	{
		Parse();
	}

	private string Summary { get; set; } = "";
	private string Details { get; set; } = "";


	public string Title => $"Wasabi Wallet v{Constants.ClientVersion}{(Constants.VersionName.Length > 0 ? " - " + Constants.VersionName : string.Empty)}: What's new?";
	public string Caption { get; private set; } = "";

	public string SummaryMd => !string.IsNullOrWhiteSpace(Summary)
		? $"""
		   ## Summary
		   {Summary}
		   """
		: "";

	public string DetailsMd => !string.IsNullOrWhiteSpace(Details)
		? $"""
		   ## Details
		   {Details}
		   """
		: "";
	public string MarkdownText => $"""
	                               {SummaryMd}
	                               {DetailsMd}
	                               """;

	private void Parse()
	{
		var releaseHighlights = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "Announcements", "ReleaseHighlights.md");
		if (!File.Exists(releaseHighlights))
		{
			throw new FileNotFoundException("The release notes file was not found.", releaseHighlights);
		}

		string fileContent = File.ReadAllText(releaseHighlights);

		Summary = ExtractSection(fileContent, "## Release Highlights");
		string releaseSummary = ExtractSection(fileContent, "## Release Summary");

		if (!string.IsNullOrEmpty(releaseSummary))
		{
			var lines = releaseSummary.Split(['\n']);
			Caption = lines.SkipWhile(string.IsNullOrEmpty).FirstOrDefault()?.Trim() ?? string.Empty;
			Details = string.Join("\n", lines.SkipWhile(string.IsNullOrEmpty).Skip(1).Select(line => line.Trim()));
		}
	}

	private static string ExtractSection(string content, string sectionHeader)
	{
		var lines = content.Split('\n');
		int startIndex = Array.FindIndex(lines, line => line.TrimStart().StartsWith(sectionHeader));

		if (startIndex == -1)
		{
			return string.Empty;
		}
		var sectionLines = lines.Skip(startIndex + 1)
			.TakeWhile(line => !line.TrimStart().StartsWith("## "))
			.ToList();

		return string.Join("\n", sectionLines).Trim();
	}
}
