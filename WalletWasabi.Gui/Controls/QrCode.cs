using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls
{
	public class QrCode : Control
	{
		private readonly int _matrixPadding;
		private Size _coercedSize;

		static QrCode()
		{
			AffectsMeasure<QrCode>(MatrixProperty);
		}

		public QrCode()
		{
			_coercedSize = new Size();
			_matrixPadding = 3;

			this.WhenAnyValue(x => x.Matrix)
				.Where(x => x != null)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => _matrix = AddPaddingToMatrix(x));

			this.WhenAnyValue(x => x.DoSaveQRCode)
				.Where(x => x)
				.DistinctUntilChanged()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async x => await SaveQRCode());
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

		public static readonly DirectProperty<QrCode, bool> DoSaveQRCodeProperty =
			AvaloniaProperty.RegisterDirect<QrCode, bool>(
				nameof(DoSaveQRCode),
				o => o.DoSaveQRCode,
				(o, v) => o.DoSaveQRCode = v);

		private bool _DoSaveQRCode;

		public bool DoSaveQRCode
		{
			get => _DoSaveQRCode;
			set => SetAndRaise(DoSaveQRCodeProperty, ref _DoSaveQRCode, value);
		}

		public static readonly DirectProperty<QrCode, string> AddressProperty =
			AvaloniaProperty.RegisterDirect<QrCode, string>(
				nameof(Address),
				o => o.Address,
				(o, v) => o.Address = v);

		private string _Address;

		public string Address
		{
			get => _Address;
			set => SetAndRaise(AddressProperty, ref _Address, value);
		}


		private async Task SaveQRCode()
		{
			var sfd = new SaveFileDialog();

			sfd.InitialFileName = $"{Address}.png";
			sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			sfd.Filters.Add(new FileDialogFilter() { Name = "Portable Network Graphics (PNG) Image file", Extensions = { "png" } });

			var fileFullName = await sfd.ShowAsync(Application.Current.MainWindow);

			if (!string.IsNullOrWhiteSpace(fileFullName))
			{
				var ext = Path.GetExtension(fileFullName);

				if (string.IsNullOrWhiteSpace(ext))
				{
					fileFullName = $"{fileFullName}.png";
				}

				ExportMatrix(fileFullName);
			}
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

			return _coercedSize;
		}

		public void ExportMatrix(string outputPath)
		{
			// TODO: This will be more simplified when Avalonia 0.9 is released.
			//		 Remove WriteableFramebuffer and replace simply with RenderTargetBitmap
			//		 Save bitmap function later on.

			var pixelBounds = PixelSize.FromSize(_coercedSize, 1);

			var ipri = AvaloniaLocator.Current.GetService<IPlatformRenderInterface>();

			using (var framebuffer = new WritableFramebuffer(PixelFormat.Bgra8888, pixelBounds))
			using (var target = ipri.CreateRenderTarget(new object[] { framebuffer }))
			using (var ctx = target.CreateDrawingContext(null))
			using (var ctxi = new DrawingContext(ctx))
			{
				Render(ctxi);

				var ret = new Bitmap(PixelFormat.Bgra8888,
								 framebuffer.Address,
								 framebuffer.Size,
								 new Vector(96, 96),
								 framebuffer.RowBytes);

				ret.Save(outputPath);
			}
		}
	}
}