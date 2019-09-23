using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls
{
	public class QrCode : Control
	{
		private readonly int _matrixPadding;
		private Size _coercedSize;
		private double _factor;

		static QrCode()
		{
			AffectsMeasure<QrCode>(MatrixProperty);
		}

		public QrCode()
		{
			_coercedSize = new Size();
			_matrixPadding = 2;

			this.WhenAnyValue(x => x.Matrix)
				.Where(x => x != null)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => _matrix = AddPaddingToMatrix(x));
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
			var nW = dims.W + (_matrixPadding * 2);
			var nH = dims.H + (_matrixPadding * 2);

			var paddedMatrix = new bool[nH, nW];

			for (var i = 0; i < dims.H; i++)
				for (var j = 0; j < dims.W; j++)
				{
					paddedMatrix[i + _matrixPadding, j + _matrixPadding] = matrix[i, j];
				}

			return paddedMatrix;
		}

		public override void Render(DrawingContext context)
		{
			var source = Matrix;

			if (source is null)
			{
				return;
			}

			var dims = GetMatrixDimensions(source);

			context.FillRectangle(Brushes.White, new Rect(0, 0, _factor * dims.W, _factor * dims.H));

			for (var i = 0; i < dims.H; i++)
				for (var j = 0; j < dims.W; j++)
				{
					var cellValue = source[i, j];
					var rect = new Rect(i * _factor, j * _factor, _factor, _factor);
					var color = cellValue ? Brushes.Black : Brushes.White;
					context.FillRectangle(color, rect);
				}
		}

		private (int W, int H) GetMatrixDimensions(bool[,] source)
				=> (source.GetUpperBound(0) + 1,
					source.GetUpperBound(1) + 1);
 
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

			_factor = Math.Floor(availMax / minDimension);

			var maxF = Math.Min(availMax, _factor * minDimension);

			_coercedSize = new Size(maxF, maxF);

			return _coercedSize;
		}
	}
}