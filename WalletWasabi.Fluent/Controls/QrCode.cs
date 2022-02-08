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
using Avalonia.Platform;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class QrCode : Control
{
	private const int MatrixPadding = 2;
	private const int MinimumBitmapSizePixelWh = 512;

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
		this.WhenAnyValue(x => x.Matrix)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(matrix =>
			{
				if (matrix is { })
				{
					FinalMatrix = AddPaddingToMatrix(matrix);
				}
			});

		_saveCommand = ReactiveCommand.CreateFromTask<string, Unit>(async address =>
		{
			await SaveQrCodeAsync(address);
			return Unit.Default;
		});

		SaveCommand.ThrownExceptions
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(_ =>
			{
				// The error is thrown also in ReceiveAddressViewModel -> SaveQrCodeCommand.ThrownExceptions.
				// However we need to catch it here too but to avoid duplicate logging we don't do anything here.
			});
	}

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

	public async Task SaveQrCodeAsync(string address)
	{
		if (FinalMatrix is null)
		{
			return;
		}

		var sfd = new SaveFileDialog();
		sfd.Title = "Save QR Code...";
		sfd.InitialFileName = $"{address}.png";
		sfd.Directory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
		sfd.Filters.Add(new FileDialogFilter()
		{ Name = "Portable Network Graphics (PNG) Image file", Extensions = { "png" } });

		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			var path = await sfd.ShowAsync(lifetime.MainWindow);

			if (!string.IsNullOrWhiteSpace(path))
			{
				var ext = Path.GetExtension(path);

				if (string.IsNullOrWhiteSpace(ext) || ext.ToLowerInvariant().TrimStart('.') != "png")
				{
					path = $"{path}.png";
				}

				var qrCodeSize = GetQrCodeSize(FinalMatrix, Bounds.Size);

				var pixSize = PixelSize.FromSize(qrCodeSize.coercedSize, 1);

				if (pixSize.Width < MinimumBitmapSizePixelWh || pixSize.Height < MinimumBitmapSizePixelWh)
				{
					pixSize = new PixelSize(MinimumBitmapSizePixelWh, MinimumBitmapSizePixelWh);
				}

				using var rtb = new RenderTargetBitmap(pixSize);
				using (var rtbCtx = rtb.CreateDrawingContext(null))
				{
					DrawQrCodeImage(rtbCtx, FinalMatrix, pixSize.ToSize(1));
				}
				rtb.Save(path);
			}
		}
	}

	private bool[,] AddPaddingToMatrix(bool[,] source)
	{
		var (indexW, indexH) = GetMatrixIndexSize(source);
		var nW = indexW + MatrixPadding * 2;
		var nH = indexH + MatrixPadding * 2;

		var paddedMatrix = new bool[nH, nW];

		for (var i = 0; i < indexH; i++)
		{
			for (var j = 0; j < indexW; j++)
			{
				paddedMatrix[i + MatrixPadding, j + MatrixPadding] = source[i, j];
			}
		}

		return paddedMatrix;
	}

	private (int indexW, int indexH) GetMatrixIndexSize(bool[,] source) =>
	(source.GetUpperBound(0) + 1, source.GetUpperBound(1) + 1);

	private void DrawQrCodeImage(IDrawingContextImpl ctx, bool[,] source, Size size)
	{
		var qrCodeSize = GetQrCodeSize(source, size);
		var (indexW, indexH) = GetMatrixIndexSize(source);
		var gcf = qrCodeSize.gridCellFactor;

		var canvasSize = new Rect(0, 0, gcf * indexW,
			gcf * indexH);

		ctx.DrawRectangle(Brushes.White, null, canvasSize);

		for (var i = 0; i < indexH; i++)
		{
			for (var j = 0; j < indexW; j++)
			{
				var cellValue = source[i, j];
				var rect = new Rect(i * gcf, j * gcf, gcf + 1, gcf + 1);
				var color = cellValue ? Brushes.Black : Brushes.White;
				ctx.DrawRectangle(color, null, rect);
			}
		}
	}

	public override void Render(DrawingContext context)
	{
		var source = FinalMatrix;

		if (source is null)
		{
			return;
		}

		DrawQrCodeImage(context.PlatformImpl, source, Bounds.Size);
	}

	private (Size coercedSize, double gridCellFactor) GetQrCodeSize(bool[,] source, Size size)
	{
		var (indexW, indexH) = GetMatrixIndexSize(source);

		var minDimension = Math.Min(indexW, indexH);
		var availMax = Math.Min(size.Width, size.Height);

		var gridCellFactor = Math.Floor(availMax / minDimension);

		var maxF = Math.Min(availMax, gridCellFactor * minDimension);

		var coercedSize = new Size(maxF, maxF);

		return (coercedSize, gridCellFactor);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		var source = FinalMatrix;

		if (source is null || source.Length == 0)
		{
			return new Size();
		}

		return GetQrCodeSize(source, availableSize).coercedSize;
	}
}
