using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
        public string FilePath { get; }
        public Version Version { get; }

        public LegalDocuments(Version version, string filePath)
        {
            Version = Guard.NotNull(nameof(version), version);
            FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);
        }

        public static async Task<LegalDocuments> TryLoadAgreedAsync(string dataDir)
        {
            var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
            IoHelpers.EnsureDirectoryExists(legalFolderPath);

            int fileCount = Directory.EnumerateFileSystemEntries(legalFolderPath).Count();
            // If more than one file found, then something strange happened, delete the dir and start from zero.
            if (fileCount > 1)
            {
                Logger.LogInfo("Multiple legal docs detected. Recovering empty legal directory...");
                await IoHelpers.DeleteRecursivelyWithMagicDustAsync(legalFolderPath).ConfigureAwait(false);
                IoHelpers.EnsureDirectoryExists(legalFolderPath);
            }

            await TryEnsureBackwardsCompatibilityAsync(dataDir).ConfigureAwait(false);

            var existingFilePath = Directory.EnumerateFiles(legalFolderPath, "*.txt", SearchOption.TopDirectoryOnly).FirstOrDefault();
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

        private static async Task TryEnsureBackwardsCompatibilityAsync(string dataDir)
        {
            var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
            IoHelpers.EnsureDirectoryExists(legalFolderPath);

            int fileCount = Directory.EnumerateFileSystemEntries(legalFolderPath).Count();
            // If no file found then to ensure backwards compatibility we can check if a wallet file already exists.
            // If a wallet file already exist, that means the user already accepted 1.0 at one point with the old system.
            if (fileCount == 0)
            {
                var walletFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Wallets");
                var hasWallet = Directory.Exists(walletFolderPath) && Directory.EnumerateFileSystemEntries(walletFolderPath).Any();
                if (hasWallet)
                {
                    Logger.LogInfo("Wallets folder is not empty. Assuming Legal docs 1.0 acceptance.");
                    var text = await File.ReadAllTextAsync(EmbeddedFilePath).ConfigureAwait(false);
                    var onePointOfilePath = BuildFilePath(new Version(1, 0), dataDir);
                    await ToFileAsync(onePointOfilePath, text).ConfigureAwait(false);
                }
            }
        }

        public static async Task<(LegalDocuments legalDocuments, string content)> FetchLatestAsync(string dataDir, Func<Uri> destAction, EndPoint torSocks, CancellationToken cancel)
        {
            string filePath;
            Version version;

            using var client = new WasabiClient(destAction, torSocks);
            var versions = await client.GetVersionsAsync(cancel).ConfigureAwait(false);
            version = versions.LegalDocumentsVersion;
            filePath = BuildFilePath(version, dataDir);
            var legal = await client.GetLegalDocumentsAsync(cancel).ConfigureAwait(false);

            return (new LegalDocuments(version, filePath), legal);
        }

        private static string BuildFilePath(Version version, string dataDir)
        {
            var legalFolderPath = Path.Combine(dataDir, LegalFolderName);
            return Path.Combine(legalFolderPath, $"{version}.txt");
        }

        public async static Task ToFileAsync(string filePath, string legal)
        {
            var legalFolderPath = Path.GetDirectoryName(filePath);
            await IoHelpers.DeleteRecursivelyWithMagicDustAsync(legalFolderPath).ConfigureAwait(false);
            IoHelpers.EnsureDirectoryExists(legalFolderPath);
            await File.WriteAllTextAsync(filePath, legal).ConfigureAwait(false);
        }

        public async Task ToFileAsync(string legal) => await ToFileAsync(FilePath, legal).ConfigureAwait(false);
    }
}
