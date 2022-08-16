using Avalonia;
using Avalonia.Media;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyBarItemViewModel : WalletCoinViewModel
{
	public bool IsPrivate { get; }
	public bool IsSemiPrivate { get; }
	public bool IsNonPrivate { get; }

	public PrivacyBarItemViewModel(PrivacyBarViewModel parent, SmartCoin coin, double start, double width) : base(coin)
	{
		IsPrivate = coin.IsPrivate(parent.Wallet.KeyManager.AnonScoreTarget);
		IsSemiPrivate = !IsPrivate && coin.IsSemiPrivate();
		IsNonPrivate = !IsPrivate && !IsSemiPrivate;

		Data = new RectangleGeometry
		{
			Rect = new Rect((double)start, 0, (double)width, 10)
		};
	}

	public RectangleGeometry Data { get; }
}
