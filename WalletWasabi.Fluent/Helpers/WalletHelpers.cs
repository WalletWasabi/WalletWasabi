using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class WalletHelpers
{
	public static WalletType GetType(KeyManager keyManager)
	{
		if (keyManager.Icon is { } icon &&
			Enum.TryParse(typeof(WalletType), icon, true, out var type) &&
			type is { })
		{
			return (WalletType)type;
		}

		return keyManager.IsHardwareWallet ? WalletType.Hardware : WalletType.Normal;
	}

	public static IEnumerable<string> GetLabels()
	{
		// Don't refresh wallet list as it may be slow.
		IEnumerable<SmartLabel> labels = Services.WalletManager.GetWallets(refreshWalletList: false)
			.Select(x => x.KeyManager)
			.SelectMany(x => x.GetLabels());

		var txStore = Services.BitcoinStore.TransactionStore;
		if (txStore is { })
		{
			labels = labels.Concat(txStore.GetLabels());
		}

		return labels.SelectMany(x => x.Labels);
	}

	public static (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName)
	{
		string walletFilePath = Path.Combine(Services.WalletManager.WalletDirectories.WalletsDir, $"{walletName}.json");

		if (string.IsNullOrEmpty(walletName))
		{
			return null;
		}

		if (walletName.IsTrimmable())
		{
			return (ErrorSeverity.Error, "Leading and trailing white spaces are not allowed!");
		}

		if (File.Exists(walletFilePath))
		{
			return (ErrorSeverity.Error, $"A wallet named {walletName} already exists. Please try a different name.");
		}

		if (!WalletGenerator.ValidateWalletName(walletName))
		{
			return (ErrorSeverity.Error, "Selected Wallet is not valid. Please try a different name.");
		}

		return null;
	}
}
