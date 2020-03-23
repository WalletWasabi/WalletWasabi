using ReactiveUI;
using System;
using WalletWasabi.Gui.ViewModels;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Wallets;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModelBase : ViewModelBase, IComparable<WalletViewModelBase>
	{
		private bool _isExpanded;
		private bool _isBusy;
		private string _title;

		public WalletViewModelBase(Wallet wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);
			Wallet = wallet;
			Title = WalletName;
		}

        protected Wallet Wallet { get; }

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
			return Title.CompareTo(other.Title);
		}
	}
}
