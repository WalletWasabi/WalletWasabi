using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Legal
{
	public class LegalDocuments
	{
		public const string EmbeddedFileName = "LegalDocuments.txt";
		public const string LegalFolderName = "Legal";
		public const string AssetsFoldername = "Assets";
		public static readonly string EmbeddedFilePath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), LegalFolderName, AssetsFoldername, EmbeddedFileName);
		public string FilePath { get; }
		public Version Version { get; }

		public static async Task<LegalDocuments> CreateAsync(string dataDir, Func<Uri> destAction, EndPoint torSocks, CancellationToken cancel)
		{
			string filePath;
			Version version;

			var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
			IoHelpers.EnsureDirectoryExists(legalFolderPath);
			var filePaths = Directory.EnumerateFiles(legalFolderPath, "*.txt", SearchOption.TopDirectoryOnly);

			// If more than one file found, then something strange happened, delete the dir and start from zero.
			if (filePaths.Count() > 1)
			{
				Directory.Delete(legalFolderPath, true);
				filePaths = Enumerable.Empty<string>();
			}
			IoHelpers.EnsureDirectoryExists(legalFolderPath);

			var existingFilePath = filePaths.FirstOrDefault();
			if (existingFilePath is { })
			{
				var verString = Path.GetFileNameWithoutExtension(existingFilePath);
				if (Version.TryParse(verString, out version))
				{
					filePath = existingFilePath;
				}
				else
				{
					File.Delete(existingFilePath);

					return await FetchLatestFromBackendNewAsync(legalFolderPath, destAction, torSocks, cancel).ConfigureAwait(false);
				}
			}
			else
			{
				return await FetchLatestFromBackendNewAsync(legalFolderPath, destAction, torSocks, cancel).ConfigureAwait(false);
			}

			return new LegalDocuments(version, filePath);
		}

		public static async Task<LegalDocuments> FetchLatestFromBackendNewAsync(string legalFolderPath, Func<Uri> destAction, EndPoint torSocks, CancellationToken cancel)
		{
			string filePath;
			Version version;

			using var client = new WasabiClient(destAction, torSocks);
			var versions = await client.GetVersionsAsync(cancel).ConfigureAwait(false);
			version = versions.LegalDocumentsVersion;
			filePath = Path.Combine(legalFolderPath, $"{version}.txt");
			var legal = await client.GetLegalDocumentsAsync(cancel).ConfigureAwait(false);
			await File.WriteAllTextAsync(filePath, legal).ConfigureAwait(false);

			return new LegalDocuments(version, filePath);
		}

		public LegalDocuments(Version version, string filePath)
		{
			Version = Guard.NotNull(nameof(version), version);
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);
		}
	}
}
