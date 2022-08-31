using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Fluent.Views.Wallets.Advanced.WalletCoins.Columns;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugCoinViewModel : ViewModelBase
{
	private readonly SmartCoin _coin;

	public DebugCoinViewModel(SmartCoin coin)
	{
		_coin = coin;

		Amount = _coin.Amount;
	}

	public Money Amount { get; private set; }
}

public partial class DebugWalletViewModel : ViewModelBase
{
	private readonly Wallet _wallet;
	private readonly ICoinsView? _coins;

	public DebugWalletViewModel(Wallet wallet)
	{
		_wallet = wallet;

		if (wallet.Coins is { })
		{
			_coins = ((CoinsRegistry) wallet.Coins).AsAllCoinsView();
		}

		WalletName = _wallet.WalletName;
		Coins = _coins?.Select(x => new DebugCoinViewModel(x)).ToList();

		CoinsSource = new FlatTreeDataGridSource<DebugCoinViewModel>(Coins ?? Enumerable.Empty<DebugCoinViewModel>())
		{
			Columns =
			{
				new TextColumn<DebugCoinViewModel, Money>(
					"Amount",
					x => x.Amount,
					new GridLength(0, GridUnitType.Auto)),
			}
		};

		CoinsSource.RowSelection!.SingleSelect = true;

	}
	public string WalletName { get; private set; }

	public List<DebugCoinViewModel>? Coins { get; }

	public FlatTreeDataGridSource<DebugCoinViewModel> CoinsSource { get; private set; }
}

public partial class DebuggerViewModel : ViewModelBase
{
	[AutoNotify] private DebugWalletViewModel? _selectedWallet;

	public DebuggerViewModel()
	{
		Wallets = Services.WalletManager.GetWallets().Select(x => new DebugWalletViewModel(x)).ToList();

		SelectedWallet = Wallets.FirstOrDefault();
	}

	public List<DebugWalletViewModel> Wallets { get; }
}
