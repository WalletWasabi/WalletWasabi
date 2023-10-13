using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using System.Linq;

namespace WalletWasabi.Fluent.Controls;

public class PrivacyBar : ItemsPresenter
{
	private const double GapBetweenSegments = 1.5;
	private const double EnlargeThreshold = 2;
	private const double EnlargeBy = 1;

	public static readonly StyledProperty<decimal> TotalAmountProperty =
		AvaloniaProperty.Register<PrivacyBarSegment, decimal>(nameof(TotalAmount));

	public decimal TotalAmount
	{
		get => GetValue(TotalAmountProperty);
		set => SetValue(TotalAmountProperty, value);
	}

	protected override IItemContainerGenerator CreateItemContainerGenerator()
	{
		return new PrivacyBarItemContainerGenerator(this);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		var children =
			this.GetVisualDescendants()
				.OfType<PrivacyBarSegment>()
				.ToList();

		var coinCount = children.Count;
		var totalAmount = TotalAmount;
		var usableWidth = finalSize.Width - (coinCount - 1) * GapBetweenSegments;

		// Calculate the width of the segments.
		var rawSegments =
			children.Select(segment =>
			{
				var amount = (double)segment.Amount;
				var width = totalAmount == 0m ? 0d : Math.Abs(usableWidth * amount / (double)totalAmount);

				return (Coin: segment, Width: width);
			}).ToArray();

		// Artificially enlarge segments smaller than the threshold px in order to make them visible.
		// Meanwhile decrease those segments that are larger than threshold px in order to fit all in the bar.
		var segmentsToEnlarge = rawSegments.Where(x => x.Width < EnlargeThreshold).ToArray();
		var segmentsToReduce = rawSegments.Except(segmentsToEnlarge).ToArray();
		var reduceBy = segmentsToEnlarge.Length * EnlargeBy / segmentsToReduce.Length;
		if (segmentsToEnlarge.Any() && segmentsToReduce.Any() && segmentsToReduce.All(x => x.Width - reduceBy > 0))
		{
			rawSegments = rawSegments.Select(x =>
			{
				var finalWidth = x.Width < EnlargeThreshold ? x.Width + EnlargeBy : x.Width - reduceBy;
				return (Coin: x.Coin, Width: finalWidth);
			}).ToArray();
		}

		var start = 0.0;
		foreach (var (coin, width) in rawSegments)
		{
			coin.Start = start;
			coin.BarWidth = width;

			start += width + GapBetweenSegments;
		}

		return base.ArrangeOverride(finalSize);
	}

	private class PrivacyBarItemContainerGenerator : ItemContainerGenerator
	{
		public PrivacyBarItemContainerGenerator(IControl owner) : base(owner)
		{
		}

		protected override IControl CreateContainer(object item)
		{
			return new PrivacyBarSegment
			{
				DataContext = item,
			};
		}
	}
}
