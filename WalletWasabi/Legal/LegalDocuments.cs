using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Legal
{
	public class LegalDocuments
	{
		public const string EmbeddedFileName = "LegalDocuments.txt";
		public const string LegalFolderName = "Legal";
		public const string AssetsFoldername = "Assets";
		public static readonly string EmbeddedFilePath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), LegalFolderName, AssetsFoldername, EmbeddedFileName);

		public LegalDocuments(Version version, string content)
		{
			Version = version;
			Content = content;
		}

		public Version Version { get; }
		public string Content { get; }

		public static async Task<LegalDocuments?> TryLoadAgreedAsync(string dataDir)
		{
			var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
			IoHelpers.EnsureDirectoryExists(legalFolderPath);
			var legalDocCandidates = FindCandidates(legalFolderPath);
			var legalDocCandidateCount = legalDocCandidates.Count();

			if (legalDocCandidateCount == 1)
			{
				var filePath = legalDocCandidates.Single();

				var content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
				var verString = Path.GetFileNameWithoutExtension(filePath);
				var version = Version.Parse(verString);

				return new LegalDocuments(version, content);
			}
			else if (legalDocCandidateCount > 1)
			{
				RemoveCandidates(legalFolderPath);
			}

			return null;
		}

		private static IEnumerable<string> FindCandidates(string legalFolderPath)
		{
			return Directory
				.EnumerateFiles(legalFolderPath, "*.txt", SearchOption.TopDirectoryOnly)
				.Where(x => Version.TryParse(Path.GetFileNameWithoutExtension(x), out _));
		}

		private static void RemoveCandidates(string legalFolderPath)
		{
			IoHelpers.EnsureDirectoryExists(legalFolderPath);
			foreach (var candidate in FindCandidates(legalFolderPath))
			{
				File.Delete(candidate);
				Logger.LogInfo($"Removed legal doc candidate: {candidate}.");
			}
		}

		public async Task ToFileAsync(string dataDir)
		{
			dataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir, trim: true);
			var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
			var filePath = Path.Combine(legalFolderPath, $"{Version}.txt");

			if (filePath is null)
			{
				throw new InvalidOperationException($"Invalid {nameof(legalFolderPath)}.");
			}

			RemoveCandidates(legalFolderPath);
			await File.WriteAllTextAsync(filePath, Content).ConfigureAwait(false);
		}
	}
}
