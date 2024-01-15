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

	public static List<LabelsByWallet> GetLabelsByWallets()
	{
		var result = new List<LabelsByWallet>();

		foreach (var wallet in Services.WalletManager.GetWallets(refreshWalletList: false))
		{
			var (changeLabels, receiveLabels) = wallet.KeyManager.GetLabels();
			result.Add(new LabelsByWallet(wallet.WalletId, changeLabels, receiveLabels));
		}

		return result;
	}

	public record LabelsByWallet(WalletId WalletId, List<LabelsArray> ChangeLabels, List<LabelsArray> ReceiveLabels);
}
