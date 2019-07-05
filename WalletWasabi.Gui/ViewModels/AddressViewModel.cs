using Avalonia;
using Avalonia.Threading;
using Gma.QrCodeNet.Encoding;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.KeyManagement;

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

		public HdPubKey Model { get; }
		public Global Global { get; }

		public AddressViewModel(HdPubKey model, Global global)
		{
			Global = global;
			Model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;
			Label = model.Label;

			this.WhenAnyValue(x => x.IsExpanded)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(x =>
				{
					try
					{
						if (x is true && QrCode is null)
						{
							var encoder = new QrEncoder();
							encoder.TryEncode(Address, out var qrCode);
							Dispatcher.UIThread.PostLogException(() => QrCode = qrCode.Matrix.InternalArray);
						}
					}
					catch (Exception ex)
					{
						Logging.Logger.LogError<AddressViewModel>(ex);
					}
				});

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
							hdPubKey.SetLabel(newLabel, kmToFile: keyManager);
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

		public bool InEditMode
		{
			get => _inEditMode;
			set => this.RaiseAndSetIfChanged(ref _inEditMode, value);
		}

		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();

		public string Pubkey => Model.PubKey.ToString();

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
			catch (Exception ex) when (ex is OperationCanceledException
									|| ex is TaskCanceledException
									|| ex is TimeoutException)
			{
				Logging.Logger.LogTrace<AddressViewModel>(ex);
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<AddressViewModel>(ex);
			}
			finally
			{
				CancelClipboardNotification?.Dispose();
				CancelClipboardNotification = null;
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
