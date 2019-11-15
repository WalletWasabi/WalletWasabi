using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Controls
{
	public class QrCode : Control
	{
		private const int MatrixPadding = 2;
		private Size CoercedSize { get; set; }
		private double GridCellFactor { get; set; }
		private bool[,] FinalMatrix { get; set; }

		static QrCode()
		{
			AffectsMeasure<QrCode>(MatrixProperty);
		}

		public QrCode()
		{
			CoercedSize = new Size();

			this.WhenAnyValue(x => x.Matrix)
				.Where(x => x != null)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => FinalMatrix = AddPaddingToMatrix(x));

			this.WhenAnyValue(x => x.QRImageSavePath)
				.Where(x => !string.IsNullOrWhiteSpace(x) || !string.IsNullOrEmpty(x))
				.Where(x => !(FinalMatrix is null))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => GenerateQRCodeBitmap(x));
		}

		private void GenerateQRCodeBitmap(string x)
		{
			var pixSize = PixelSize.FromSize(CoercedSize, 1);
			using var rtb = new RenderTargetBitmap(pixSize);
			
			rtb.Render(this);
			rtb.Save(x);
		}

		public static readonly DirectProperty<QrCode, string> QRImageSavePathProperty =
			AvaloniaProperty.RegisterDirect<QrCode, string>(
				nameof(QRImageSavePath),
				o => o.QRImageSavePath,
				(o, v) => o.QRImageSavePath = v);

		private string _qRImageSavePath;

		public string QRImageSavePath
		{
			get => _qRImageSavePath;
			set => SetAndRaise(QRImageSavePathProperty, ref _qRImageSavePath, value);
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
			set => SetAndRaise(MatrixProperty, ref _matrix, value);
		}

		private bool[,] AddPaddingToMatrix(bool[,] matrix)
		{
			var dims = GetMatrixDimensions(matrix);
			var nW = dims.w + (MatrixPadding * 2);
			var nH = dims.h + (MatrixPadding * 2);

			var paddedMatrix = new bool[nH, nW];

			for (var i = 0; i < dims.h; i++)
			{
				for (var j = 0; j < dims.w; j++)
				{
					paddedMatrix[i + MatrixPadding, j + MatrixPadding] = matrix[i, j];
				}
			}

			return paddedMatrix;
		}

		public override void Render(DrawingContext context)
		{
			var source = FinalMatrix;

			if (source is null)
			{
				return;
			}

			var dims = GetMatrixDimensions(source);

			context.FillRectangle(Brushes.White, new Rect(0, 0, GridCellFactor * dims.w, GridCellFactor * dims.h));

			for (var i = 0; i < dims.h; i++)
			{
				for (var j = 0; j < dims.w; j++)
				{
					var cellValue = source[i, j];
					var rect = new Rect(i * GridCellFactor, j * GridCellFactor, GridCellFactor, GridCellFactor);
					var color = cellValue ? Brushes.Black : Brushes.White;
					context.FillRectangle(color, rect);
				}
			}
		}

		private (int w, int h) GetMatrixDimensions(bool[,] source)
		{
			return (source.GetUpperBound(0) + 1, source.GetUpperBound(1) + 1);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var source = FinalMatrix;

			if (source is null || source.Length == 0)
			{
				return new Size();
			}

			var dims = GetMatrixDimensions(source);
			var minDimension = Math.Min(dims.w, dims.h);
			var availMax = Math.Min(availableSize.Width, availableSize.Height);

			GridCellFactor = Math.Floor(availMax / minDimension);

			var maxF = Math.Min(availMax, GridCellFactor * minDimension);

			CoercedSize = new Size(maxF, maxF);

			return CoercedSize;
		}
	}
}