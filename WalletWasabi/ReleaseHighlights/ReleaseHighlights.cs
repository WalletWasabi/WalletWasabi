
using System.IO;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.ReleaseHighlights;

public class ReleaseHighlights
{
	public ReleaseHighlights()
	{
		Parse();
	}

	private string Summary { get; set; } = "";
	private string Details { get; set; } = "";


	public string Title => $"Wasabi Wallet v{Constants.ClientVersion} - What's new?";
	public string Caption { get; private set; } = "";

	public string MarkdownText => $"""
	                               ## Summary
	                               {Summary}
	                               ## Details
	                               {Details}
	                               """;

	private void Parse()
	{
		var releaseHighlights = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "ReleaseHighlights", "ReleaseHighlights.md");
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
