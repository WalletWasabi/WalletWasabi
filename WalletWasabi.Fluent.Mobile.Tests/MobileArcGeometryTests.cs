using Avalonia;
using Avalonia.Headless.XUnit;
using WalletWasabi.Fluent.Mobile.Controls;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileArcGeometryTests
{
	[AvaloniaTheory]
	[InlineData(360)]
	[InlineData(-360)]
	[InlineData(720)]
	[InlineData(-720)]
	public void FullRingsUseNondegenerateClosedGeometry(double sweep)
	{
		var geometry = MobileArcGeometry.Create(new Point(100, 100), 90, -90, sweep);
		Assert.NotNull(geometry);
		var bounds = geometry.Bounds;
		Assert.InRange(bounds.X, 9.99, 10.01);
		Assert.InRange(bounds.Y, 9.99, 10.01);
		Assert.InRange(bounds.Width, 179.99, 180.01);
		Assert.InRange(bounds.Height, 179.99, 180.01);
	}

	[AvaloniaFact]
	public void LightReferenceGaugeLeavesTheBottomQuarterOpen()
	{
		var geometry = MobileArcGeometry.Create(new Point(100, 100), 90, 135, 270);
		Assert.NotNull(geometry);
		var bounds = geometry.Bounds;
		Assert.InRange(bounds.X, 9.99, 10.01);
		Assert.InRange(bounds.Y, 9.99, 10.01);
		Assert.InRange(bounds.Width, 179.99, 180.01);
		var expectedBottom = 100 + 90 / Math.Sqrt(2);
		Assert.InRange(bounds.Bottom, expectedBottom - 0.02, expectedBottom + 0.02);
	}

	[AvaloniaFact]
	public void WholeStartRotationsPreserveArcBounds()
	{
		var first = MobileArcGeometry.Create(new Point(100, 100), 90, 135, 270);
		var rotated = MobileArcGeometry.Create(new Point(100, 100), 90, 135 + 360 * 10, 270);
		Assert.NotNull(first);
		Assert.NotNull(rotated);
		Assert.Equal(first.Bounds, rotated.Bounds);
	}

	[Theory]
	[InlineData(0, -90, 360)]
	[InlineData(-1, -90, 360)]
	[InlineData(double.NaN, -90, 360)]
	[InlineData(double.PositiveInfinity, -90, 360)]
	[InlineData(90, double.NaN, 360)]
	[InlineData(90, double.PositiveInfinity, 360)]
	[InlineData(90, -90, double.NaN)]
	[InlineData(90, -90, double.NegativeInfinity)]
	[InlineData(90, -90, 0)]
	public void InvalidOrEmptyArcsDoNotReachTheNativeRenderer(double radius, double start, double sweep)
	{
		Assert.Null(MobileArcGeometry.Create(new Point(100, 100), radius, start, sweep));
	}

	[Fact]
	public void NonfiniteCentersAreRejected()
	{
		Assert.Null(MobileArcGeometry.Create(new Point(double.PositiveInfinity, 0), 90, -90, 360));
		Assert.Null(MobileArcGeometry.Create(new Point(0, double.NaN), 90, -90, 360));
	}
}
