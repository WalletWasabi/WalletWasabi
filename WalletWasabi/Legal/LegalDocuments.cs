using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

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

		public LegalDocuments(string dataDir)
		{
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
			var desiredFilePath = Path.Combine(legalFolderPath, $"{Constants.LegalDocumentsVersion.ToString()}.txt");
			if (existingFilePath is { })
			{
				var verString = Path.GetFileNameWithoutExtension(existingFilePath);
				if (Version.TryParse(verString, out Version version))
				{
					FilePath = existingFilePath;
					Version = version;
				}
				else
				{
					File.Delete(existingFilePath);

					File.Copy(EmbeddedFilePath, desiredFilePath);
					Version = Constants.LegalDocumentsVersion;
				}
			}
			else
			{
				File.Copy(EmbeddedFilePath, desiredFilePath);
				Version = Constants.LegalDocumentsVersion;
			}
		}
	}
}
