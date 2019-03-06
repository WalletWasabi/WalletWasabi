using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		private TransactionInfo _model;
		private bool _clipboardNotificationVisible;
		private double _clipboardNotificationOpacity;

		public TransactionViewModel(TransactionInfo model)
		{
			Disposables = new CompositeDisposable();

			_model = model;
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;

			_confirmed = model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed, model.Confirmed).DisposeWith(Disposables);
		}

		private readonly ObservableAsPropertyHelper<bool> _confirmed;

		public string DateTime => _model.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

		public bool Confirmed => _confirmed.Value;

		public string AmountBtc => _model.AmountBtc;

		public Money Amount => Money.TryParse(_model.AmountBtc, out Money money) ? money : Money.Zero;

		public string Label => _model.Label;

		public string TransactionId => _model.TransactionId;

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

		private long _copyNotificationsInprocess = 0;

		public void CopyToClipboard()
		{
			Application.Current.Clipboard.SetTextAsync(TransactionId).GetAwaiter().GetResult();

			Interlocked.Increment(ref _copyNotificationsInprocess);
			ClipboardNotificationVisible = true;
			ClipboardNotificationOpacity = 1;

			Dispatcher.UIThread.PostLogException(async () =>
			{
				try
				{
					await Task.Delay(1000);
					if (Interlocked.Read(ref _copyNotificationsInprocess) <= 1)
					{
						ClipboardNotificationOpacity = 0;
						await Task.Delay(1000);
						if (Interlocked.Read(ref _copyNotificationsInprocess) <= 1)
						{
							ClipboardNotificationVisible = false;
						}
					}
				}
				finally
				{
					Interlocked.Decrement(ref _copyNotificationsInprocess);
				}
			});
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

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
