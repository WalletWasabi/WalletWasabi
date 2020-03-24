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
	public class WalletViewModelBase : ViewModelBase, IComparable<WalletViewModelBase>
	{
		private bool _isExpanded;
		private bool _isBusy;
		private string _title;
		private WalletState _walletState;

		public WalletViewModelBase(Wallet wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);

			Wallet = wallet;
			Title = WalletName;

			WalletState = wallet.State;

			this.WhenAnyValue(x => x.WalletState)
				.Subscribe(x =>
				{
					switch (x)
					{
						case WalletState.Initialized:
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
			get { return _walletState; }
			set { this.RaiseAndSetIfChanged(ref _walletState, value); }
		}

		public Wallet Wallet { get; }

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public string Title
		{
			get { return _title; }
			set { this.RaiseAndSetIfChanged(ref _title, value); }
		}

		public string WalletName => Wallet.WalletName;

		public bool IsBusy
		{
			get { return _isBusy; }
			set { this.RaiseAndSetIfChanged(ref _isBusy, value); }
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
	}
}
