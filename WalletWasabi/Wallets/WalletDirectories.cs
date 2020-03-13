using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;

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

		public IEnumerable<FileInfo> EnumerateWalletFiles(bool includeBackupDir = false)
		{
			var walletsDirInfo = new DirectoryInfo(WalletsDir);
			var walletsDirExists = walletsDirInfo.Exists;
			var searchPattern = $"*.{WalletFileExtension}";
			var searchOption = SearchOption.TopDirectoryOnly;
			IEnumerable<FileInfo> result = null;

			if (includeBackupDir)
			{
				var backupsDirInfo = new DirectoryInfo(WalletsBackupDir);
				if (!walletsDirExists && !backupsDirInfo.Exists)
				{
					return Enumerable.Empty<FileInfo>();
				}

				result = walletsDirInfo
					.EnumerateFiles(searchPattern, searchOption)
					.Concat(backupsDirInfo.EnumerateFiles(searchPattern, searchOption));
			}
			else
			{
				if (!walletsDirExists)
				{
					return Enumerable.Empty<FileInfo>();
				}

				result = walletsDirInfo.EnumerateFiles(searchPattern, searchOption);
			}

			return result.OrderByDescending(t => t.LastAccessTimeUtc);
		}

		public string GetNextWalletName(string prefix = "Wallet")
		{
			int i = 0;
			while (true)
			{
				var walletName = $"{prefix}{i}";
				if (!File.Exists(GetWalletFilePaths(walletName).walletFilePath))
				{
					return walletName;
				}

				i++;
			}
		}
	}
}
