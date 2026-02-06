using Avalonia;
using Avalonia.Media;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyRingItemViewModel : IPrivacyRingPreviewItem
{
	private const double TotalAngle = 2d * Math.PI;
	private const double UprightAngle = Math.PI / 2d;
	private const double SegmentWidth = 10.0;

	public PrivacyRingItemViewModel(PrivacyRingViewModel parent, CoinModel coin, double start, double end)
	{
		OuterRadius = Math.Min(parent.Height / 2, parent.Width / 2);

		Data = CreateGeometry(start, end, OuterRadius);

		IsPrivate = coin.IsPrivate;
		IsSemiPrivate = coin.IsSemiPrivate;
		IsNonPrivate = coin.IsNonPrivate;
		Amount = new Amount(coin.Amount);
		Unconfirmed = !coin.IsConfirmed;
		Confirmations = coin.Confirmations;
		AnonScoreText = $"{coin.AnonScore}";
		Labels = coin.Labels;

		PrivacyLevelText = GetPrivacyLevelDescription();

		Reference = PrivacyLevelText;
		if (Unconfirmed)
		{
			Reference += " (pending)";
		}
	}

	public PrivacyRingItemViewModel(PrivacyRingViewModel parent, PrivacyLevel privacyLevel, Money amount, double start, double end, string anonScoreText)
	{
		OuterRadius = Math.Min(parent.Height / 2, parent.Width / 2);

		Data = CreateGeometry(start, end, OuterRadius);

		IsPrivate = privacyLevel == PrivacyLevel.Private;
		IsSemiPrivate = privacyLevel == PrivacyLevel.SemiPrivate;
		IsNonPrivate = privacyLevel == PrivacyLevel.NonPrivate;
		Amount = new Amount(amount);
		AnonScoreText = anonScoreText;
		Unconfirmed = false;

		PrivacyLevelText = GetPrivacyLevelDescription();

		Reference = GetPrivacyLevelDescription();
	}

	public PathGeometry Data { get; private set; }

	public double OuterRadius { get; private set; }

	public bool IsPrivate { get; }
	public bool IsSemiPrivate { get; }
	public bool IsNonPrivate { get; }
	public string AnonScoreText { get; }
	public LabelsArray Labels { get; }
	public Amount Amount { get; }
	public string PrivacyLevelText { get; }
	public bool Unconfirmed { get; }
	public int Confirmations { get; }
	public string Reference { get; }

	private PathGeometry CreateGeometry(double start, double end, double outerRadius)
	{
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

		return new PathGeometry()
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

	private Point GetAnglePoint(double r, double angle)
	{
		var x = r * Math.Cos(angle);
		var y = r * Math.Sin(angle);
		return new Point(x, y);
	}

	private string GetPrivacyLevelDescription() =>
		this switch
		{
			{ IsPrivate: true } => "Private",
			{ IsSemiPrivate: true } => "Semi-private",
			{ IsNonPrivate: true } => "Non-private",
			_ => "[Unknown]"
		};
}
