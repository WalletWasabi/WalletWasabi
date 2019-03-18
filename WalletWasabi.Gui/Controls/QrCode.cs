using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Rendering;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace WalletWasabi.Gui.Controls
{
	public class QrCode : Control
	{
		static QrCode()
		{
			AffectsMeasure<QrCode>(MatrixProperty);
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

		public override void Render(DrawingContext context)
		{
			var source = Matrix;

			if (source != null)
			{
				var h = source.GetUpperBound(0);
				var w = source.GetUpperBound(1);

				var minDimension = (float)Math.Min(w, h);
				var minBound = Math.Min(Bounds.Width, Bounds.Height);
				var factor = minBound / minDimension;

				for (var i = 0; i <= h; i++)
				{
					for (var j = 0; j <= w; j++)
					{
						var cellValue = source[i, j];
						var color = cellValue ? Brushes.Black : Brushes.White;
						var rect = new Rect(i * factor, j * factor, factor, factor);
						context.FillRectangle(color, rect);
					}
				}
			}
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var source = Matrix;

			if (source is null || source.Length == 0)
				return new Size();

			var max = Math.Min(availableSize.Width, availableSize.Height);

			var size = new Size(max, max);

			return size;
		}
	}
}
