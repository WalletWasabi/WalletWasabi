using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using SkiaSharp;

namespace WalletWasabi.Fluent.Controls;
#pragma warning disable CS0612

public class BlurBehind : Control
{
	private BlurBehindRenderOperation? _operation;

	public static readonly StyledProperty<Vector> BlurRadiusProperty =
		AvaloniaProperty.Register<BlurBehind, Vector>(
			nameof(BlurRadius), new Vector(10, 10));

	public Vector BlurRadius
	{
		get => GetValue(BlurRadiusProperty);
		set => SetValue(BlurRadiusProperty, value);
	}

	private class BlurBehindRenderOperation : ICustomDrawOperation
	{
		private readonly Rect _bounds;
		private readonly Vector _blurRadius;

		public BlurBehindRenderOperation(Rect bounds, Vector blurRadius)
		{
			_bounds = bounds;
			_blurRadius = blurRadius;
		}

		public void Dispose()
		{
			// nothing to do.
		}

		public bool HitTest(Point p) => _bounds.Contains(p);

		public void Render(IDrawingContextImpl context)
		{
			var leaseFeature = context.GetFeature<ISkiaSharpApiLeaseFeature>();
			if (leaseFeature is null)
			{
				return;
			}

			using var lease = leaseFeature.Lease();

			if (!lease.SkCanvas.TotalMatrix.TryInvert(out var currentInvertedTransform))
			{
				return;
			}

			// One or both dimensions has zero size.
			// Skia doesnt like that.
			if (_bounds.Size.AspectRatio == 0)
			{
				return;
			}

			using var backgroundSnapshot = lease.SkSurface.Snapshot();

			using var preBlur =
				   DrawingContextHelper.CreateDrawingContext(
					   new Size(backgroundSnapshot.Width, backgroundSnapshot.Height), new Vector(96, 96),
					   lease.GrContext);

			var leaseCustomFeature = preBlur.GetFeature<ISkiaSharpApiLeaseFeature>();
			if (leaseCustomFeature is null)
			{
				return;
			}
			using var leaseCustom = leaseCustomFeature.Lease();

			using (var filter = SKImageFilter.CreateBlur((int)_blurRadius.X, (int)_blurRadius.Y, SKShaderTileMode.Clamp))
			using (var blurPaint = new SKPaint { ImageFilter = filter })
			{
				var canvas = leaseCustom.SkSurface.Canvas;
				canvas.DrawSurface(lease.SkSurface, 0f, 0f, blurPaint);
				canvas.Flush();
			}

			using var preBlurredSnapshot = leaseCustom.SkSurface.Snapshot();

			using var backdropShader = SKShader.CreateImage(preBlurredSnapshot, SKShaderTileMode.Clamp,
				SKShaderTileMode.Clamp, currentInvertedTransform);

			using var blurred = DrawingContextHelper.CreateDrawingContext(_bounds.Size, new Vector(96, 96), lease.GrContext);
			using (var blurPaint = new SKPaint { Shader = backdropShader })
			{
				leaseCustom.SkSurface.Canvas.DrawRect(0, 0, (float)_bounds.Width, (float)_bounds.Height, blurPaint);
			}

			blurred.DrawTo(context);
		}

		public Rect Bounds => _bounds.Inflate(_blurRadius.X);

		public bool Equals(ICustomDrawOperation? other)
		{
			return other is BlurBehindRenderOperation op && op._bounds == _bounds;
		}
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		_operation = new BlurBehindRenderOperation(new Rect(new Point(), finalSize), BlurRadius);

		return base.ArrangeOverride(finalSize);
	}

	public override void Render(DrawingContext context)
	{
		if (_operation is { })
		{
			context.Custom(_operation);
		}
	}
}
