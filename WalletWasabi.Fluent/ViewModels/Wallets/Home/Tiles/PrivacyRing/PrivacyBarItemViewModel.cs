using Avalonia;
using Avalonia.Media;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyBarItemViewModel : ViewModelBase, IDisposable
{
	public const double BarHeight = 10;

	public PrivacyBarItemViewModel(PrivacyBarViewModel parent, SmartCoin coin, double start, double width)
	{
		Coin = new WalletCoinViewModel(coin);

		IsPrivate = coin.IsPrivate(parent.Wallet.KeyManager.AnonScoreTarget);
		IsSemiPrivate = !IsPrivate && coin.IsSemiPrivate();
		IsNonPrivate = !IsPrivate && !IsSemiPrivate;

		Data = new RectangleGeometry
		{
			Rect = new Rect(start, 0, width, BarHeight)
		};
	}

	public PrivacyBarItemViewModel(bool isPrivate, bool isSemiPrivate, bool isNonPrivate, double start, double width)
	{
		IsPrivate = isPrivate;
		IsSemiPrivate = isSemiPrivate;
		IsNonPrivate = isNonPrivate;

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
