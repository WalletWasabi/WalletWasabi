using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinHelpers
{
	public static LabelsArray GetLabels(this SmartCoin coin, int privateThreshold)
	{
		if (coin.IsPrivate(privateThreshold) || coin.IsSemiPrivate(privateThreshold))
		{
			return LabelsArray.Empty;
		}

		if (coin.HdPubKey.ClusterLabels.IsEmpty)
		{
			return CoinPocketHelper.UnlabelledFundsText;
		}

		return coin.HdPubKey.ClusterLabels;
	}

	public static int GetConfirmations(this SmartCoin coin) => coin.Transaction.GetConfirmations((int)Services.SmartHeaderChain.TipHeight);

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
