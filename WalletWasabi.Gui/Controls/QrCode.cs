using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using AvalonStudio.Extensibility.Theme;
using ReactiveUI;
using System;
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

		private const int matrixPadding = 3;

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

			var curBounds = new Size(Bounds.Width, Bounds.Height);
			var pixelBounds = PixelSize.FromSize(curBounds, 1);

			var sfd = new SaveFileDialog();
			var fileFullName = await sfd.ShowAsync(Application.Current.MainWindow, fallBack: true);

			if (!string.IsNullOrWhiteSpace(fileFullName))
			{
				var ext = Path.GetExtension(fileFullName);

				if (string.IsNullOrWhiteSpace(ext))
				{
					fileFullName = $"{fileFullName}.bmp";
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

				var h = value.GetUpperBound(0) + 1;
				var nH = h + (matrixPadding * 2);

				var w = value.GetUpperBound(1) + 1;
				var nW = w + (matrixPadding * 2);

				var paddedMatrix = new bool[nH, nW];

				for (var i = 0; i < h; i++)
				{
					for (var j = 0; j < w; j++)
					{
						paddedMatrix[i + matrixPadding, j + matrixPadding] = value[i, j];
					}
				}

				SetAndRaise(MatrixProperty, ref _matrix, paddedMatrix);
			}
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

			var h = source.GetUpperBound(0) + 1;
			var w = source.GetUpperBound(1) + 1;

			var minDimension = Math.Min(w, h);
			var minBound = Math.Min(Bounds.Width, Bounds.Height);
			var factorR = (float)minBound / minDimension;
			var factor = Math.Floor(factorR);

			context.FillRectangle(Brushes.White, new Rect(0, 0, factor * w, factor * h));

			for (var i = 0; i < h; i++)
			{
				for (var j = 0; j < w; j++)
				{
					var cellValue = source[i, j];
					var rect = new Rect(i * factor, j * factor, factor, factor);
					var color = cellValue ? Brushes.Black : Brushes.White;
					context.FillRectangle(color, rect);
				}
			}

		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var source = Matrix;

			if (source is null || source.Length == 0)
			{
				return new Size();
			}

			var max = Math.Min(availableSize.Width, availableSize.Height);

			var size = new Size(max, max);

			return size;
		}
	}
}