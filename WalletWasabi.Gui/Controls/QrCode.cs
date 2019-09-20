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
		private static readonly Geometry SaveIcon = Geometry.Parse(
				"M13,9V3.5L18.5,9M6,2C4.89,2 4,2.89 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2H6Z");

		private ReactiveCommand<Unit, Unit> SaveCommand { get; }

		private const int MatrixPadding = 3;

		private Size CoercedSize = new Size();

		private static DrawingPresenter GetSavePresenter()
		{
			return new DrawingPresenter
			{
				Drawing = new GeometryDrawing
				{
					Brush = Brush.Parse("#22B14C"),
					Geometry = SaveIcon
				},
				Width = 16,
				Height = 16
			};
		}

		static QrCode()
		{
			AffectsMeasure<QrCode>(MatrixProperty);
		}

		public QrCode()
		{
			SaveCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await SaveAsync();
			});

			SaveCommand.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning(ex));
		}

		private async Task SaveAsync()
		{
			var source = Matrix;

			if (source is null) return;

			var pixelBounds = PixelSize.FromSize(CoercedSize, 1);

			var sfd = new SaveFileDialog();

			sfd.InitialFileName = $"{Address}.png";
			sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			sfd.Filters.Add(new FileDialogFilter() { Name = "PNG Files", Extensions = { "png" } });

			var fileFullName = await sfd.ShowAsync(Application.Current.MainWindow,
												   fallBack: true);

			if (!string.IsNullOrWhiteSpace(fileFullName))
			{
				var ext = Path.GetExtension(fileFullName);

				if (string.IsNullOrWhiteSpace(ext))
				{
					fileFullName = $"{fileFullName}.png";
				}

				// TODO: This will be more simplified when Avalonia 0.9 is released.
				//		 Remove WriteableFramebuffer and replace simply with RenderTargetBitmap
				//		 Save bitmap function later on.

				var outputStream = File.OpenWrite(fileFullName);

				var framebuffer = new WritableFramebuffer(PixelFormat.Rgba8888, pixelBounds);
				var ipri = Avalonia.AvaloniaLocator.Current.GetService<IPlatformRenderInterface>();

				using (var target = ipri.CreateRenderTarget(new object[] { framebuffer }))
				using (var ctx = target.CreateDrawingContext(null))
				using (var ctxi = new DrawingContext(ctx))
				{
					this.Render(ctxi);
				}

				var outBitmap = new Bitmap(PixelFormat.Rgba8888,
										   framebuffer.Address,
										   framebuffer.Size,
										   new Vector(96, 96),
										   framebuffer.RowBytes);
				framebuffer.Deallocate();

				outBitmap.Save(outputStream);

				await outputStream.FlushAsync();

				outputStream.Close();
			}
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
				if (value is null) return;

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

		public static readonly DirectProperty<QrCode, string> AddressProperty =
			AvaloniaProperty.RegisterDirect<QrCode, string>(
				nameof(Address),
				o => o.Address,
				(o, v) => o.Address = v);

		private string _address;

		public string Address
		{
			get { return _address; }
			set { SetAndRaise(AddressProperty, ref _address, value); }
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			ContextMenu = new ContextMenu
			{
				DataContext = this,
				Items = new Avalonia.Controls.Controls()
			};

			var menuItems = (ContextMenu.Items as Avalonia.Controls.Controls);

			menuItems.Add(new MenuItem
			{
				Header = "Save",
				Foreground = ColorTheme.CurrentTheme.Foreground,
				Command = SaveCommand,
				Icon = GetSavePresenter()
			});
		}

		public override void Render(DrawingContext context)
		{
			var source = Matrix;

			if (source is null) return;

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
			CoercedSize = new Size(maxF, maxF);

			return CoercedSize;
		}
	}
}