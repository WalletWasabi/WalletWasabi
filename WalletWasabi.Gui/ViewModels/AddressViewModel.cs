using Avalonia;
using Gma.QrCodeNet.Encoding;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Logging;

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
		private ObservableAsPropertyHelper<string> _expandMenuCaption;

		public HdPubKey Model { get; }
		public Global Global { get; }

		public AddressViewModel(HdPubKey model, Global global)
		{
			Global = global;
			Model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;
			_label = model.Label;

			this.WhenAnyValue(x => x.IsExpanded)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Where(x => x)
				.Take(1)
				.Select(x =>
				{
					var encoder = new QrEncoder();
					encoder.TryEncode(Address, out var qrCode);
					return qrCode.Matrix.InternalArray;
				})
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(qr => QrCode = qr, onError: ex => Logger.LogError(ex)); // Catch the exceptions everywhere (e.g.: Select) except in Subscribe.

			Global.UiConfig
				.WhenAnyValue(x => x.LurkingWifeMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(IsLurkingWifeMode));
					this.RaisePropertyChanged(nameof(Address));
					this.RaisePropertyChanged(nameof(Label));
				}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Label)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(newLabel =>
				{
					if (InEditMode)
					{
						KeyManager keyManager = Global.WalletService.KeyManager;
						HdPubKey hdPubKey = keyManager.GetKeys(x => Model == x).FirstOrDefault();

						if (hdPubKey != default)
						{
							hdPubKey.SetLabel(newLabel, kmToFile: keyManager);
						}
					}
				});

			_expandMenuCaption = this
				.WhenAnyValue(x => x.IsExpanded)
				.Select(x => (x ? "Hide " : "Show ") + "QR Code")
				.ObserveOn(RxApp.MainThreadScheduler)
				.ToProperty(this, x => x.ExpandMenuCaption)
				.DisposeWith(Disposables);
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
				if (!IsLurkingWifeMode && !new SmartLabel(value).IsEmpty)
				{
					this.RaiseAndSetIfChanged(ref _label, value);
				}
			}
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

		public ReactiveCommand<string, Unit> ExecuteSaveQRCodeCommand { get; set; }

		public string ExpandMenuCaption => _expandMenuCaption?.Value ?? string.Empty;

		private CancellationTokenSource CancelClipboardNotification { get; set; }

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
			await ExecuteSaveQRCodeCommand?.Execute(Address);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					CancelClipboardNotification?.Dispose();
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
