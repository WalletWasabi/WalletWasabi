using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
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

	/// <returns>Labels ordered by blockchain.</returns>
	public static IEnumerable<LabelsArray> GetTransactionLabels() => Services.BitcoinStore.TransactionStore.GetLabels();

	public static IEnumerable<LabelsArray> GetReceiveAddressLabels() =>
		Services.WalletManager
			.GetWallets(refreshWalletList: false) // Don't refresh wallet list as it may be slow.
			.Select(x => x.KeyManager)
			.SelectMany(x => x.GetReceiveLabels());

	public static IEnumerable<LabelsArray> GetChangeAddressLabels() =>
		Services.WalletManager
			.GetWallets(refreshWalletList: false) // Don't refresh wallet list as it may be slow.
			.Select(x => x.KeyManager)
			.SelectMany(x => x.GetChangeLabels());
}
