using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using AvalonStudio.Extensibility.Theme;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls
{
	public class QrCode : Control
	{ 
		private const int MatrixPadding = 3;
		private Size _coercedSize = new Size();
 		static QrCode()
		{
			AffectsMeasure<QrCode>(MatrixProperty);
		}
 
		public Bitmap RenderQrToBitmap()
		{
			// TODO: This will be more simplified when Avalonia 0.9 is released.
			//		 Remove WriteableFramebuffer and replace simply with RenderTargetBitmap
			//		 Save bitmap function later on.

			var pixelBounds = PixelSize.FromSize(_coercedSize, 1);
			var framebuffer = new WritableFramebuffer(PixelFormat.Bgra8888, pixelBounds);
			var ipri = AvaloniaLocator.Current.GetService<IPlatformRenderInterface>();

			using (var target = ipri.CreateRenderTarget(new object[] { framebuffer }))
			using (var ctx = target.CreateDrawingContext(null))
			using (var ctxi = new DrawingContext(ctx))
			{
				Render(ctxi);
			}

			var outBitmap = new Bitmap(PixelFormat.Bgra8888,
									   framebuffer.Address,
									   framebuffer.Size,
									   new Vector(96, 96),
									   framebuffer.RowBytes);
			framebuffer.Deallocate();

			return outBitmap;
		}

		public static readonly DirectProperty<QrCode, bool[,]> MatrixProperty =
			AvaloniaProperty.RegisterDirect<QrCode, bool[,]>(
				nameof(Matrix),
				o => o.Matrix,
				(o, v) => o.Matrix = v);

		private bool[,] _matrix;

		[Content]
		public bool[,] Matrix
		{
			get => _matrix;
			set
			{
				if (value is null)
				{
					return;
				}

				var dims = GetMatrixDimensions(value);
				var nW = dims.W + (MatrixPadding * 2);
				var nH = dims.H + (MatrixPadding * 2);

				var paddedMatrix = new bool[nH, nW];

				for (var i = 0; i < dims.H; i++)
				{
					for (var j = 0; j < dims.W; j++)
					{
						paddedMatrix[i + MatrixPadding, j + MatrixPadding] = value[i, j];
					}
				}

				SetAndRaise(MatrixProperty, ref _matrix, paddedMatrix);
			}
		}
 
		public static readonly DirectProperty<QrCode, Bitmap> AddressQRCodeBitmapProperty =
			AvaloniaProperty.RegisterDirect<QrCode, Bitmap>(
				nameof(AddressQRCodeBitmap),
				o => o.AddressQRCodeBitmap,
				(o, v) => o.AddressQRCodeBitmap = v);
		
		private Bitmap _AddressQRCodeBitmap;
		
		public Bitmap AddressQRCodeBitmap
		{
			get { return _AddressQRCodeBitmap; }
			set { SetAndRaise(AddressQRCodeBitmapProperty, ref _AddressQRCodeBitmap, value); }
		}
		public override void Render(DrawingContext context)
		{
			var source = Matrix;

			if (source is null)
			{
				return;
			}

			var dims = GetMatrixDimensions(source);

			var factor = CalculateDiscreteRectSize(source);

			context.FillRectangle(Brushes.White, new Rect(0, 0, factor * dims.W, factor * dims.H));

			for (var i = 0; i < dims.H; i++)
			{
				for (var j = 0; j < dims.W; j++)
				{
					var cellValue = source[i, j];
					var rect = new Rect(i * factor, j * factor, factor, factor);
					var color = cellValue ? Brushes.Black : Brushes.White;
					context.FillRectangle(color, rect);
				}
			}
		}

		private (int W, int H) GetMatrixDimensions(bool[,] source)
				=> (source.GetUpperBound(0) + 1,
					source.GetUpperBound(1) + 1);

		private int CalculateDiscreteRectSize(bool[,] source)
		{
			var dims = GetMatrixDimensions(source);
			var minDimension = Math.Min(dims.W, dims.H);
			var minBound = Math.Min(Bounds.Width, Bounds.Height);
			var factorR = (float)minBound / minDimension;
			return (int)Math.Floor(factorR);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var source = Matrix;

			if (source is null || source.Length == 0)
			{
				return new Size();
			}

			var dims = GetMatrixDimensions(source);
			var minDimension = Math.Min(dims.W, dims.H);
			var availMax = Math.Min(availableSize.Width, availableSize.Height);
			var factor = CalculateDiscreteRectSize(source);
			var maxF = Math.Min(availMax, factor * minDimension);
			_coercedSize = new Size(maxF, maxF);

			AddressQRCodeBitmap = RenderQrToBitmap();

			return _coercedSize;
		}
	}
}
