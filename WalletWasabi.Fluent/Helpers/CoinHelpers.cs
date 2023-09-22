using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinHelpers
{
	public static LabelsArray GetLabels(this SmartCoin coin, int privateThreshold)
	{
		if (coin.IsPrivate(privateThreshold) || coin.IsSemiPrivate(privateThreshold))
		{
			return LabelsArray.Empty;
		}

		if (coin.HdPubKey.Cluster.Labels == LabelsArray.Empty)
		{
			return CoinPocketHelper.UnlabelledFundsText;
		}

		return coin.HdPubKey.Cluster.Labels;
	}

	public static int GetConfirmations(this SmartCoin coin) => coin.Height.Type == HeightType.Chain ? (int)Services.BitcoinStore.SmartHeaderChain.TipHeight - coin.Height.Value + 1 : 0;

	public static PrivacyLevel GetPrivacyLevel(this SmartCoin coin, Wallet wallet)
	{
		var anonScoreTarget = wallet.AnonScoreTarget;
		return coin.GetPrivacyLevel(anonScoreTarget);
	}

	public static PrivacyLevel GetPrivacyLevel(this SmartCoin coin, int privateThreshold)
	{
		if (coin.IsPrivate(privateThreshold))
		{
			return PrivacyLevel.Private;
		}

		if (coin.IsSemiPrivate(privateThreshold))
		{
			return PrivacyLevel.SemiPrivate;
		}

		return PrivacyLevel.NonPrivate;
	}
}
