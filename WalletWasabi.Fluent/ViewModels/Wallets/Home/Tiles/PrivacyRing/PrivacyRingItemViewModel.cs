using Avalonia;
using Avalonia.Media;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyRingItemViewModel : WalletCoinViewModel, IPrivacyRingPreviewItem
{
	private const double TotalAngle = 2d * Math.PI;
	private const double UprightAngle = Math.PI / 2d;
	private const double SegmentWidth = 10.0;

	public PrivacyRingItemViewModel(PrivacyRingViewModel parent, SmartCoin coin, double start, double end) : base(coin)
	{
		var outerRadius = Math.Min(parent.Height / 2, parent.Width / 2);
		var innerRadius = outerRadius - SegmentWidth;

		var arc1Size = new Size(outerRadius, outerRadius);
		var arc2Size = new Size(innerRadius, innerRadius);

		var margin =
			end - start == 1.0
			? 0.01d
			: 2d;

		var startAngle = ((TotalAngle * start) - UprightAngle);
		var endAngle = ((TotalAngle * end) - UprightAngle);
		var isLargeArc = endAngle - startAngle > Math.PI;

		// Compensate for angles that are too small
		if (endAngle - startAngle < 0.03)
		{
			startAngle -= 0.005;
			endAngle += 0.005;
			margin -= 0.01;
		}

		var outerOffset = outerRadius == 0 ? 0 : (margin / (TotalAngle * outerRadius) * TotalAngle);
		var innerOffset = innerRadius == 0 ? 0 : (margin / (TotalAngle * innerRadius) * TotalAngle);

		var origin1 = GetAnglePoint(outerRadius, startAngle + outerOffset);
		var arc1 = GetAnglePoint(outerRadius, endAngle - outerOffset);
		var origin2 = GetAnglePoint(innerRadius, endAngle - innerOffset);
		var arc2 = GetAnglePoint(innerRadius, startAngle + innerOffset);

		OuterRadius = outerRadius;
		IsPrivate = coin.IsPrivate(parent.Wallet.KeyManager.AnonScoreTarget);
		IsSemiPrivate = !IsPrivate && coin.IsSemiPrivate();
		IsNonPrivate = !IsPrivate && !IsSemiPrivate;
		AmountText = $"{Amount.ToFormattedString()} BTC";
		Unconfirmed = !coin.Confirmed;
		Confirmations = coin.GetConfirmations();

		Data = new PathGeometry()
		{
			Figures =
			{
				new PathFigure
				{
					StartPoint = origin1, IsClosed = true,
					Segments = new PathSegments
					{
						new ArcSegment { Size = arc1Size, Point = arc1, IsLargeArc = isLargeArc },
						new LineSegment { Point = origin2 },
						new ArcSegment { Size = arc2Size, Point = arc2, SweepDirection = SweepDirection.CounterClockwise, IsLargeArc = isLargeArc }
					}
				}
			}
		};
	}

	public PathGeometry Data { get; }

	public double OuterRadius { get; }

	public bool IsPrivate { get; }
	public bool IsSemiPrivate { get; }
	public bool IsNonPrivate { get; }
	public string AmountText { get; }
	public bool Unconfirmed { get; }
	public int Confirmations { get; }

	private Point GetAnglePoint(double r, double angle)
	{
		var x = r * Math.Cos(angle);
		var y = r * Math.Sin(angle);
		return new Point(x, y);
	}
}
