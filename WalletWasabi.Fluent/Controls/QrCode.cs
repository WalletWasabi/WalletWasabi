using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls
{
	public class QrCode : Control
	{
		private const int MatrixPadding = 2;

		public static readonly DirectProperty<QrCode, ReactiveCommand<string, Unit>> SaveCommandProperty =
			AvaloniaProperty.RegisterDirect<QrCode, ReactiveCommand<string, Unit>>(
				nameof(SaveCommand),
				o => o.SaveCommand,
				(o, v) => o.SaveCommand = v);

		public static readonly DirectProperty<QrCode, bool[,]?> MatrixProperty =
			AvaloniaProperty.RegisterDirect<QrCode, bool[,]?>(nameof(Matrix), o => o.Matrix, (o, v) => o.Matrix = v);

		private ReactiveCommand<string, Unit> _saveCommand;

		private bool[,]? _matrix;

		static QrCode()
		{
			AffectsMeasure<QrCode>(MatrixProperty);
		}

		public QrCode()
		{
			CoercedSize = new Size();

			this.WhenAnyValue(x => x.Matrix)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(matrix =>
				{
					if (matrix is { })
					{
						FinalMatrix = AddPaddingToMatrix(matrix);
					}
				});

			_saveCommand = ReactiveCommand.CreateFromTask<string, Unit>(SaveQrCodeAsync);

			SaveCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(_ =>
				{
					// The error is thrown also in ReceiveAddressViewModel -> SaveQrCodeCommand.ThrownExceptions.
					// However we need to catch it here too but to avoid duplicate logging we don't do anything here.
				});
		}

		private Size CoercedSize { get; set; }
		private double GridCellFactor { get; set; }
		private bool[,]? FinalMatrix { get; set; }

		public ReactiveCommand<string, Unit> SaveCommand
		{
			get => _saveCommand;
			set => SetAndRaise(SaveCommandProperty, ref _saveCommand, value);
		}

		[Content]
		public bool[,]? Matrix
		{
			get => _matrix;
			set => SetAndRaise(MatrixProperty, ref _matrix, value);
		}

		public async Task<Unit> SaveQrCodeAsync(string address)
		{
			if (FinalMatrix is null)
			{
				return Unit.Default;
			}

			var sfd = new SaveFileDialog();
			sfd.Title = "Save QR Code...";
			sfd.InitialFileName = $"{address}.png";
			sfd.Directory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			sfd.Filters.Add(new FileDialogFilter() { Name = "Portable Network Graphics (PNG) Image file", Extensions = { "png" } });

			var visualRoot = (ClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
			var path = await sfd.ShowAsync(visualRoot.MainWindow);

			if (!string.IsNullOrWhiteSpace(path))
			{
				var ext = Path.GetExtension(path);

				if (string.IsNullOrWhiteSpace(ext) || ext.ToLowerInvariant().TrimStart('.') != "png")
				{
					path = $"{path}.png";
				}

				var pixSize = PixelSize.FromSize(CoercedSize, 1);
				using var rtb = new RenderTargetBitmap(pixSize);

				rtb.Render(this);
				rtb.Save(path);
			}

			return Unit.Default;
		}

		private static bool[,] AddPaddingToMatrix(bool[,] matrix)
		{
			var (width, height) = GetMatrixDimensions(matrix);
			var nW = width + (MatrixPadding * 2);
			var nH = height + (MatrixPadding * 2);

			var paddedMatrix = new bool[nH, nW];

			for (var i = 0; i < height; i++)
			{
				for (var j = 0; j < width; j++)
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

			var (width, height) = GetMatrixDimensions(source);

			context.FillRectangle(Brushes.White, new Rect(0, 0, GridCellFactor * width, GridCellFactor * height));

			for (var i = 0; i < height; i++)
			{
				for (var j = 0; j < width; j++)
				{
					var cellValue = source[i, j];
					var rect = new Rect(i * GridCellFactor, j * GridCellFactor, GridCellFactor + 1, GridCellFactor + 1);
					var color = cellValue ? Brushes.Black : Brushes.White;
					context.FillRectangle(color, rect);
				}
			}
		}

		private static (int w, int h) GetMatrixDimensions(bool[,] source)
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

			var (width, height) = GetMatrixDimensions(source);
			var minDimension = Math.Min(width, height);
			var availMax = Math.Min(availableSize.Width, availableSize.Height);

			GridCellFactor = Math.Floor(availMax / minDimension);

			var maxF = Math.Min(availMax, GridCellFactor * minDimension);

			CoercedSize = new Size(maxF, maxF);

			return CoercedSize;
		}
	}
}
