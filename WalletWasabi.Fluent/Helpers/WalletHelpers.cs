using System.Collections.Generic;
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

	public static List<LabelsByWallet> GetLabelsByWallets(IEnumerable<Wallet> wallets)
	{
		var result = new List<LabelsByWallet>();

		foreach (var wallet in wallets)
		{
			var (changeLabels, receiveLabels) = wallet.KeyManager.GetLabels();
			result.Add(new LabelsByWallet(wallet.WalletId, changeLabels, receiveLabels));
		}

		return result;
	}

	public record LabelsByWallet(WalletId WalletId, List<string> ChangeLabels, List<string> ReceiveLabels);
}
