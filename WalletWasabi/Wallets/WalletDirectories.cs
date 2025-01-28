using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wallets;

public class WalletDirectories
{
	public const string WalletsDirName = "Wallets";
	public const string WalletFileExtension = "json";

	public WalletDirectories(Network network, string workDir)
	{
		Network = network;
		var correctedWorkDir = Guard.NotNullOrEmptyOrWhitespace(nameof(workDir), workDir, true);
		WalletsDir = network == Network.Main
			? Path.Combine(correctedWorkDir, WalletsDirName)
			: Path.Combine(correctedWorkDir, WalletsDirName, network.ToString());

		Directory.CreateDirectory(WalletsDir);
	}

	public string WalletsDir { get; }

	public Network Network { get; }

	public string GetWalletFilePaths(string walletName)
	{
		if (!walletName.EndsWith($".{WalletFileExtension}", StringComparison.OrdinalIgnoreCase))
		{
			walletName = $"{walletName}.{WalletFileExtension}";
		}
		return Path.Combine(WalletsDir, walletName);
	}

	public IEnumerable<FileInfo> EnumerateWalletFiles()
	{
		var walletsDirInfo = new DirectoryInfo(WalletsDir);
		var walletsDirExists = walletsDirInfo.Exists;
		var searchPattern = $"*.{WalletFileExtension}";
		var searchOption = SearchOption.TopDirectoryOnly;
		IEnumerable<FileInfo> result;

		
		if (!walletsDirExists)
		{
			return Enumerable.Empty<FileInfo>();
		}

		result = walletsDirInfo.EnumerateFiles(searchPattern, searchOption);
		
		return result.OrderByDescending(t => t.LastAccessTimeUtc);
	}

	public string GetNextWalletName(string prefix = "Random Wallet")
	{
		int i = 1;
		var walletNames = EnumerateWalletFiles().Select(x => Path.GetFileNameWithoutExtension(x.Name));
		while (true)
		{
			var walletName = i == 1 ? prefix : $"{prefix} {i}";

			if (!walletNames.Contains(walletName))
			{
				return walletName;
			}

			i++;
		}
	}
}
