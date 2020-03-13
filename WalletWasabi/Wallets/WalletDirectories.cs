using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wallets
{
	public class WalletDirectories
	{
		public const string WalletsDirName = "Wallets";
		public const string WalletsBackupDirName = "WalletBackups";
		private const string WalletFileExtension = "json";

		public WalletDirectories(string workDir)
		{
			WorkDir = Guard.NotNullOrEmptyOrWhitespace(nameof(workDir), workDir, true);

			Directory.CreateDirectory(WalletsDir);
			Directory.CreateDirectory(WalletsBackupDir);
		}

		public string WorkDir { get; }
		public string WalletsDir => Path.Combine(WorkDir, WalletsDirName);
		public string WalletsBackupDir => Path.Combine(WorkDir, WalletsBackupDirName);

		public (string walletFilePath, string walletBackupFilePath) GetWalletFilePaths(string walletName)
		{
			if (!walletName.EndsWith($".{WalletFileExtension}", StringComparison.OrdinalIgnoreCase))
			{
				walletName = $"{walletName}.{WalletFileExtension}";
			}
			return (Path.Combine(WalletsDir, walletName), Path.Combine(WalletsBackupDir, walletName));
		}

		public IEnumerable<FileInfo> EnumerateWalletFiles()
		{
			if (!Directory.Exists(WalletsDir))
			{
				return Enumerable.Empty<FileInfo>();
			}

			var directoryInfo = new DirectoryInfo(WalletsDir);
			return directoryInfo.GetFiles($"*.{WalletFileExtension}", SearchOption.TopDirectoryOnly).OrderByDescending(t => t.LastAccessTimeUtc);
		}
	}
}
