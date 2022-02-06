using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using JetBrains.Annotations;
using SkiaSharp;

namespace WalletWasabi.Fluent.Controls;

public class CustomBlurBehind : Control
{
	public static readonly StyledProperty<ExperimentalAcrylicMaterial> MaterialProperty =
		AvaloniaProperty.Register<CustomBlurBehind, ExperimentalAcrylicMaterial>(
			"Material");

	public ExperimentalAcrylicMaterial Material
	{
		get => GetValue(MaterialProperty);
		set => SetValue(MaterialProperty, value);
	}

	static ImmutableExperimentalAcrylicMaterial DefaultAcrylicMaterial =
		(ImmutableExperimentalAcrylicMaterial)new ExperimentalAcrylicMaterial()
		{
			MaterialOpacity = 0.0,
			TintColor = Colors.Azure,
			TintOpacity = 0.0,
			PlatformTransparencyCompensationLevel = 0
		}.ToImmutable();

	static CustomBlurBehind()
	{
		AffectsRender<CustomBlurBehind>(MaterialProperty);
	}

	[CanBeNull] private static SKShader s_acrylicNoiseShader;

	class BlurBehindRenderOperation : ICustomDrawOperation
	{
		private readonly ImmutableExperimentalAcrylicMaterial _material;
		private readonly Rect _bounds;

		public BlurBehindRenderOperation(ImmutableExperimentalAcrylicMaterial material, Rect bounds)
		{
			_material = material;
			_bounds = bounds;
		}

		public void Dispose()
		{

		}

		public bool HitTest(Point p) => _bounds.Contains(p);


		static SKColorFilter CreateAlphaColorFilter(double opacity)
		{
			if (opacity > 1)
			{
				opacity = 1;
			}

			var c = new byte[256];
			var a = new byte[256];
			for (var i = 0; i < 256; i++)
			{
				c[i] = (byte)i;
				a[i] = (byte)(i * opacity);
			}

			return SKColorFilter.CreateTable(a, c, c, c);
		}

		public void Render(IDrawingContextImpl context)
		{
			if (context is not ISkiaDrawingContextImpl skia)
			{
				return;
			}

			if (!skia.SkCanvas.TotalMatrix.TryInvert(out var currentInvertedTransform))
			{
				return;
			}


			using var backgroundSnapshot = skia.SkSurface.Snapshot();
			using var backdropShader = SKShader.CreateImage(backgroundSnapshot, SKShaderTileMode.Clamp,
				SKShaderTileMode.Clamp, currentInvertedTransform);

			using var blurred = SKSurface.Create(skia.GrContext, false, new SKImageInfo(
				(int)Math.Ceiling(_bounds.Width),
				(int)Math.Ceiling(_bounds.Height), SKImageInfo.PlatformColorType, SKAlphaType.Premul));
			using (var filter = SKImageFilter.CreateBlur(10, 10, SKShaderTileMode.Clamp))
			using (var blurPaint = new SKPaint
			       {
				       Shader = backdropShader,
				       ImageFilter = filter
			       })
			{
				blurred.Canvas.DrawRect(0, 0, (float)_bounds.Width, (float)_bounds.Height, blurPaint);
			}

			using (var blurSnap = blurred.Snapshot())
			using (var blurSnapShader = SKShader.CreateImage(blurSnap))
			using (var blurSnapPaint = new SKPaint
			       {
				       Shader = blurSnapShader,
				       IsAntialias = true
			       })
			{
				skia.SkCanvas.DrawRect(0, 0, (float)_bounds.Width, (float)_bounds.Height, blurSnapPaint);
			}
		}

		public Rect Bounds => _bounds.Inflate(4);

		public bool Equals(ICustomDrawOperation? other)
		{
			return other is BlurBehindRenderOperation op && op._bounds == _bounds && op._material.Equals(_material);
		}
	}


	public override void Render(DrawingContext context)
	{
		var mat = Material != null
			? (ImmutableExperimentalAcrylicMaterial)Material.ToImmutable()
			: DefaultAcrylicMaterial;
		context.Custom(new BlurBehindRenderOperation(mat, new Rect(default, Bounds.Size)));
	}
}