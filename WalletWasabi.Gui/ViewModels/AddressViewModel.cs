using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Gma.QrCodeNet.Encoding;
using ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Services;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.ViewModels
{
	public class AddressViewModel : ViewModelBase, IDisposable
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		private bool _isExpanded;
		private bool[,] _qrCode;
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;
		private string _label;
		private bool _inEditMode;
		private object _qrCodeBitmap;

		public HdPubKey Model { get; }
		public Global Global { get; }

		public AddressViewModel(HdPubKey model, Global global)
		{
			Global = global;
			Model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;
			_label = model.Label.ToString();

			this.WhenAnyValue(x => x.IsExpanded)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Where(x => x)
				.Take(1)
				.Subscribe(_ =>
				{
					try
					{
						var encoder = new QrEncoder();
						encoder.TryEncode(Address, out var qrCode);
						Dispatcher.UIThread.PostLogException(() => QrCode = qrCode.Matrix.InternalArray);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				});

			_expandMenuCaption = this.WhenAnyValue(x => x.IsExpanded)
									 .Select(x => (x ? "Hide " : "Show ") + "QR Code")
									 .ToProperty(this, x => x.ExpandMenuCaption);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(IsLurkingWifeMode));
				this.RaisePropertyChanged(nameof(Address));
				this.RaisePropertyChanged(nameof(Label));
			}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Label)
				.Subscribe(newLabel =>
				{
					if (InEditMode)
					{
						KeyManager keyManager = Global.WalletService.KeyManager;
						HdPubKey hdPubKey = keyManager.GetKeys(x => Model == x).FirstOrDefault();

						if (hdPubKey != default)
						{
							hdPubKey.SetLabel(new SmartLabel(newLabel), kmToFile: keyManager);
						}
					}
				});
		}

		public bool IsLurkingWifeMode => Global.UiConfig.LurkingWifeMode is true;

		public bool ClipboardNotificationVisible
		{
			get => _clipboardNotificationVisible;
			set => this.RaiseAndSetIfChanged(ref _clipboardNotificationVisible, value);
		}

		public double ClipboardNotificationOpacity
		{
			get => _clipboardNotificationOpacity;
			set => this.RaiseAndSetIfChanged(ref _clipboardNotificationOpacity, value);
		}

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public string Label
		{
			get => _label;
			set
			{
				if (!IsLurkingWifeMode)
				{
					this.RaiseAndSetIfChanged(ref _label, value);
				}
			}
		}

		internal ObservableAsPropertyHelper<string> _expandMenuCaption;
		public string ExpandMenuCaption => _expandMenuCaption?.Value ?? string.Empty;

		public object AddressQRCodeBitmap
		{
			get => _qrCodeBitmap;
			set => this.RaiseAndSetIfChanged(ref _qrCodeBitmap, value);
		}

		public bool InEditMode
		{
			get => _inEditMode;
			set => this.RaiseAndSetIfChanged(ref _inEditMode, value);
		}

		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();

		public string PubKey => Model.PubKey.ToString();

		public string KeyPath => Model.FullKeyPath.ToString();

		public bool[,] QrCode
		{
			get => _qrCode;
			set => this.RaiseAndSetIfChanged(ref _qrCode, value);
		}

		public CancellationTokenSource CancelClipboardNotification { get; set; }

		public async Task TryCopyToClipboardAsync()
		{
			try
			{
				CancelClipboardNotification?.Cancel();
				while (CancelClipboardNotification != null)
				{
					await Task.Delay(50);
				}
				CancelClipboardNotification = new CancellationTokenSource();

				var cancelToken = CancelClipboardNotification.Token;

				await Application.Current.Clipboard.SetTextAsync(Address);
				cancelToken.ThrowIfCancellationRequested();

				ClipboardNotificationVisible = true;
				ClipboardNotificationOpacity = 1;

				await Task.Delay(1000, cancelToken);
				ClipboardNotificationOpacity = 0;
				await Task.Delay(1000, cancelToken);
				ClipboardNotificationVisible = false;
			}
			catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				CancelClipboardNotification?.Dispose();
				CancelClipboardNotification = null;
			}
		}

		public async Task SaveQRCodeAsync()
		{
			var sfd = new SaveFileDialog();

			sfd.InitialFileName = $"{Address}.png";
			sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			sfd.Filters.Add(new FileDialogFilter() { Name = "Portable Network Graphics (PNG) Image file", Extensions = { "png" } });

			var path = await sfd.ShowAsync(Application.Current.MainWindow, fallBack: true);

			if (!string.IsNullOrWhiteSpace(path))
			{
				var ext = Path.GetExtension(path);

				if (string.IsNullOrWhiteSpace(ext))
				{
					path = $"{path}.png";
				}

				var imageService = Locator.Current.GetService<IImageService>();

				await imageService.SaveImageAsync(path, AddressQRCodeBitmap);
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
