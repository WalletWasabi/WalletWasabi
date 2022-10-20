using Avalonia;
using Avalonia.Media;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyBarItemViewModel : ViewModelBase, IDisposable
{
	public const double BarHeight = 10;

	public PrivacyBarItemViewModel(PrivacyBarViewModel parent, SmartCoin coin, double start, double width)
	{
		Coin = new WalletCoinViewModel(coin);

		IsPrivate = coin.IsPrivate(parent.Wallet.AnonScoreTarget);
		IsSemiPrivate = !IsPrivate && coin.IsSemiPrivate();
		IsNonPrivate = !IsPrivate && !IsSemiPrivate;

		Data = new RectangleGeometry
		{
			Rect = new Rect(start, 0, width, BarHeight)
		};
	}

	public PrivacyBarItemViewModel(Pocket pocket, Wallet wallet, double start, double width)
	{
		IsPrivate = pocket.Coins.All(x => x.IsPrivate(wallet.AnonScoreTarget));
		IsSemiPrivate = !IsPrivate && pocket.Coins.All(x => x.IsSemiPrivate());
		IsNonPrivate = !IsPrivate && !IsSemiPrivate;

		Data = new RectangleGeometry
		{
			Rect = new Rect(start, 0, width, BarHeight)
		};
	}

	public RectangleGeometry Data { get; }
	public WalletCoinViewModel? Coin { get; }

	public bool IsPrivate { get; }
	public bool IsSemiPrivate { get; }
	public bool IsNonPrivate { get; }

	public void Dispose()
	{
		Coin?.Dispose();
	}
}
