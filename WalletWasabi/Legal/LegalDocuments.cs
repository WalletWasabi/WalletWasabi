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

		public static LegalDocuments TryLoadAgreed(string dataDir)
		{
			var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
			IoHelpers.EnsureDirectoryExists(legalFolderPath);
			var filePaths = Directory.EnumerateFiles(legalFolderPath, "*.txt", SearchOption.TopDirectoryOnly);

			// If more than one file found, then something strange happened, delete the dir and start from zero.
			if (filePaths.Count() > 1)
			{
				IoHelpers.CleanDirectory(legalFolderPath);
				filePaths = Enumerable.Empty<string>();
			}

			var existingFilePath = filePaths.FirstOrDefault();
			if (existingFilePath is { })
			{
				var verString = Path.GetFileNameWithoutExtension(existingFilePath);
				if (Version.TryParse(verString, out Version version))
				{
					string filePath = existingFilePath;
					return new LegalDocuments(version, filePath);
				}
				else
				{
					File.Delete(existingFilePath);
				}
			}

			return null;
		}

		public static async Task<(LegalDocuments legalDocuments, string content)> FetchLatestAsync(string dataDir, Func<Uri> destAction, EndPoint torSocks, CancellationToken cancel)
		{
			string filePath;
			Version version;

			var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
			using var client = new WasabiClient(destAction, torSocks);
			var versions = await client.GetVersionsAsync(cancel).ConfigureAwait(false);
			version = versions.LegalDocumentsVersion;
			filePath = Path.Combine(legalFolderPath, $"{version}.txt");
			var legal = await client.GetLegalDocumentsAsync(cancel).ConfigureAwait(false);

			return (new LegalDocuments(version, filePath), legal);
		}

		public async Task ToFileAsync(string legal)
		{
			var legalFolderPath = Path.GetDirectoryName(FilePath);
			IoHelpers.CleanDirectory(legalFolderPath);
			await File.WriteAllTextAsync(FilePath, legal).ConfigureAwait(false);
		}

		public LegalDocuments(Version version, string filePath)
		{
			Version = Guard.NotNull(nameof(version), version);
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);
		}
	}
}
