using Avalonia;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyRingItemViewModel : WalletCoinViewModel, IPrivacyRingPreviewItem
{
	private const double TotalAngle = 2d * Math.PI;
	private const double UprightAngle = Math.PI / 2d;

	public PrivacyRingItemViewModel(PrivacyRingViewModel parent, SmartCoin coin, double start, double end) : base(coin)
	{
		var outerRadius = parent.OuterRadius;
		var innerRadius = parent.InnerRadius;

		Arc1Size = new Size(outerRadius, outerRadius);
		Arc2Size = new Size(innerRadius, innerRadius);

		var margin = 2d;

		var startAngle = (TotalAngle * start) - UprightAngle;
		var endAngle = (TotalAngle * end) - UprightAngle;

		var outerOffset = outerRadius == 0 ? 0 : (margin / (TotalAngle * outerRadius) * TotalAngle);
		var innerOffset = innerRadius == 0 ? 0 : (margin / (TotalAngle * innerRadius) * TotalAngle);

		Origin1 = GetAnglePoint(outerRadius, startAngle + outerOffset);
		Arc1 = GetAnglePoint(outerRadius, endAngle - outerOffset);
		Origin2 = GetAnglePoint(innerRadius, endAngle - innerOffset);
		Arc2 = GetAnglePoint(innerRadius, startAngle + innerOffset);

		OuterRadius = outerRadius;
		IsPrivate = coin.IsPrivate(parent.Wallet.KeyManager.AnonScoreTarget);
		IsSemiPrivate = !IsPrivate && coin.IsSemiPrivate();
		IsNonPrivate = !IsPrivate && !IsSemiPrivate;
		AmountText = $"{Amount.ToFormattedString()} BTC";
	}

	public double OuterRadius { get; }

	public Point Origin1 { get; }
	public Point Arc1 { get; }
	public Size Arc1Size { get; }

	public Point Origin2 { get; }
	public Point Arc2 { get; }
	public Size Arc2Size { get; }

	public bool IsPrivate { get; }
	public bool IsSemiPrivate { get; }
	public bool IsNonPrivate { get; }
	public string AmountText { get; }

	private Point GetAnglePoint(double r, double angle)
	{
		var x = r * Math.Cos(angle);
		var y = r * Math.Sin(angle);
		return new Point(x, y);
	}
}
