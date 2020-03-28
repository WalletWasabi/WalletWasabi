using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModelBase : ViewModelBase, IComparable<WalletViewModelBase>, IDisposable
	{
		private bool _isExpanded;
		private bool _isBusy;
		private string _title;
		private WalletState _walletState;
		private CompositeDisposable _disposables;
		private bool _disposedValue;

		public WalletViewModelBase(Wallet wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);

			_disposables = new CompositeDisposable();

			Wallet = wallet;
			Title = WalletName;

			WalletState = wallet.State;

			Observable.FromEventPattern<WalletState>(wallet, nameof(wallet.StateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => WalletState = x.EventArgs)
				.DisposeWith(_disposables);

			this.WhenAnyValue(x => x.WalletState)
				.Subscribe(x =>
				{
					switch (x)
					{
						case WalletState.Started:
						case WalletState.Stopped:
						case WalletState.Uninitialized:
							IsBusy = false;
							break;

						default:
							IsBusy = true;
							break;
					}
				});
		}

		public WalletState WalletState
		{
			get => _walletState;
			private set => this.RaiseAndSetIfChanged(ref _walletState, value);
		}

		public Wallet Wallet { get; }

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public string WalletName => Wallet.WalletName;

		public bool IsBusy
		{
			get => _isBusy;
			private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		public int CompareTo([AllowNull] WalletViewModelBase other)
		{
			if (WalletState != other.WalletState)
			{
				if (WalletState == WalletState.Started || other.WalletState == WalletState.Started)
				{
					return other.WalletState.CompareTo(WalletState);
				}
			}

			return Title.CompareTo(other.Title);
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_disposables?.Dispose();
					_disposables = null;
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
