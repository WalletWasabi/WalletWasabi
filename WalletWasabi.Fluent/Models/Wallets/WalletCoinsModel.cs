using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletCoinsModel
{
	private readonly Wallet _wallet;

	public WalletCoinsModel(Wallet wallet, IWalletModel walletModel)
	{
		_wallet = wallet;

		List =
			Observable.Defer(() => GetCoins().ToObservable())                                                        // initial coin list
					  .Concat(walletModel.TransactionProcessed.SelectMany(_ => GetCoins()))                          // Refresh whenever there's a relevant transaction
					  .Concat(walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).SelectMany(_ => GetCoins())) // Also refresh whenever AnonScoreTarget changes
					  .ToObservableChangeSet();

		Pockets =
			Observable.Defer(() => _wallet.GetPockets().ToObservable())                                                       // initial pocket list
					  .Concat(walletModel.TransactionProcessed.SelectMany(_ => wallet.GetPockets()))                          // Refresh whenever there's a relevant transaction
					  .Concat(walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).SelectMany(_ => wallet.GetPockets())) // Also refresh whenever AnonScoreTarget changes
					  .ToObservableChangeSet();
	}

	public IObservable<IChangeSet<ICoinModel>> List { get; }

	public IObservable<IChangeSet<Pocket>> Pockets { get; }

	private IEnumerable<ICoinModel> GetCoins()
	{
		return _wallet.Coins.Select(x => new CoinModel(_wallet, x));
	}
}
