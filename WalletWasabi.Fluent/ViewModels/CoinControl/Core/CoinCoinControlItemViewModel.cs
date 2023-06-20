using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class CoinCoinControlItemViewModel : CoinControlItemViewModelBase
{
	public CoinCoinControlItemViewModel(Pocket pocket) : this(pocket.Coins.First())
	{
		if (pocket.Coins.Count() != 1)
		{
			throw new InvalidOperationException("The pocket must contain only one coin.");
		}

		Labels = pocket.Labels;
	}

	public CoinCoinControlItemViewModel(SmartCoin smartCoin)
	{
		CoinJoinManager cjManager = Services.HostedServices.Get<CoinJoinManager>();

		SmartCoin = smartCoin;
		Amount = smartCoin.Amount;
		IsConfirmed = smartCoin.Confirmed;
		IsBanned = smartCoin.IsBanned;
		IsCoinjoining = cjManager.IsCoinInCoinJoin(smartCoin);
		var confirmationCount = smartCoin.GetConfirmations();
		ConfirmationStatus = $"{confirmationCount} confirmation{TextHelpers.AddSIfPlural(confirmationCount)}";
		BannedUntilUtcToolTip = smartCoin.BannedUntilUtc.HasValue ? $"Can't participate in coinjoin until: {smartCoin.BannedUntilUtc:g}" : null;
		AnonymityScore = (int)smartCoin.AnonymitySet;
		BannedUntilUtc = smartCoin.BannedUntilUtc;
		IsSelected = false;
		ScriptType = ScriptType.FromEnum(smartCoin.ScriptType);
	}

	public SmartCoin SmartCoin { get; }
}
