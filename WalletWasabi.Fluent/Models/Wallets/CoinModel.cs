using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class CoinModel : ReactiveObject
{
	public CoinModel(Wallet wallet, SmartCoin coin)
	{
		PrivacyLevel = coin.GetPrivacyLevel(wallet.AnonScoreTarget);
		Amount = coin.Amount;
		IsConfirmed = coin.Confirmed;
		Confirmations = coin.GetConfirmations();
		AnonimitySet = (int)coin.AnonymitySet;
		Labels = coin.GetLabels(wallet.AnonScoreTarget);
		Key = coin.Outpoint.GetHashCode();
	}

	public Money Amount { get; }

	public int Key { get; }

	public PrivacyLevel PrivacyLevel { get; }

	public bool IsConfirmed { get; }

	public int Confirmations { get; }

	public int AnonimitySet { get; }

	public LabelsArray Labels { get; }

	public bool IsPrivate => PrivacyLevel == PrivacyLevel.Private;

	public bool IsSemiPrivate => PrivacyLevel == PrivacyLevel.SemiPrivate;

	public bool IsNonPrivate => PrivacyLevel == PrivacyLevel.NonPrivate;
}
