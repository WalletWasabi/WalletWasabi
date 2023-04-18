using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Legal;

public class LegalDocuments
{
	public static readonly string EmbeddedFilePathForWw1 = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "Legal", "Assets", "LegalDocumentsWw1.txt");
	public static readonly string EmbeddedFilePathForWw2 = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "Legal", "Assets", "LegalDocumentsWw2.txt");

	public LegalDocuments(Version version, string content)
	{
		Version = version;
		Content = content;
	}

	public Version Version { get; }
	public string Content { get; }

	public static async Task<LegalDocuments?> LoadAgreedAsync(string folderPath)
	{
		if (!Directory.Exists(folderPath))
		{
			return null;
		}

		var legalDocCandidates = FindCandidates(folderPath);
		switch (legalDocCandidates.Count())
		{
			case 1:
				string? filePath = legalDocCandidates.Single();
				string? content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
				string? verString = Path.GetFileNameWithoutExtension(filePath);
				Version version = Version.Parse(verString)
					?? throw new InvalidOperationException("Legal documents version is null.");
				return new LegalDocuments(version, content);

			case > 1:
				RemoveCandidates(folderPath);
				break;
		}

		return null;
	}

	private static IEnumerable<string> FindCandidates(string folderPath)
	{
		if (!Directory.Exists(folderPath))
		{
			return Enumerable.Empty<string>();
		}

		return Directory
			.EnumerateFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
			.Where(x => Version.TryParse(Path.GetFileNameWithoutExtension(x), out _));
	}

	public static void RemoveCandidates(string folderPath)
	{
		if (!Directory.Exists(folderPath))
		{
			return;
		}

		foreach (var candidate in FindCandidates(folderPath))
		{
			File.Delete(candidate);
			Logger.LogInfo($"Removed legal doc candidate: {candidate}.");
		}
	}

	public async Task ToFileAsync(string folderPath)
	{
		folderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath);
		IoHelpers.EnsureDirectoryExists(folderPath);
		string filePath = Path.Combine(folderPath, $"{Version}.txt");

		RemoveCandidates(folderPath);
		await File.WriteAllTextAsync(filePath, Content).ConfigureAwait(false);
	}
}
