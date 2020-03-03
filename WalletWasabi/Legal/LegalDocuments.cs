using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Legal
{
	public class LegalDocuments
	{
		public const string EmbeddedFileName = "LegalDocuments.txt";
		public const string LegalFolderName = "Legal";
		public const string AssetsFoldername = "Assets";
		public static readonly string EmbeddedFilePath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), LegalFolderName, AssetsFoldername, EmbeddedFileName);

		public LegalDocuments(string filePath)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
			var verString = Path.GetFileNameWithoutExtension(FilePath);
			Version = Version.Parse(verString);
		}

		public string FilePath { get; }
		public Version Version { get; }

		public static LegalDocuments TryLoadAgreed(string dataDir)
		{
			var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
			IoHelpers.EnsureDirectoryExists(legalFolderPath);
			var legalDocCandidates = FindCandidates(legalFolderPath);
			var legalDocCandidateCount = legalDocCandidates.Count();

			if (legalDocCandidateCount == 1)
			{
				var filePath = legalDocCandidates.Single();
				return new LegalDocuments(filePath);
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

		public async Task ToFileAsync(string content)
		{
			var legalFolderPath = Path.GetDirectoryName(FilePath);
			RemoveCandidates(legalFolderPath);
			await File.WriteAllTextAsync(FilePath, content).ConfigureAwait(false);
		}
	}
}
