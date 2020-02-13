using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModelBase : ViewModelBase, IComparable<WalletViewModelBase>
	{
		private bool _isExpanded;
		private string _title;
		private bool _isBusy;

		public WalletViewModelBase(Wallet wallet)
		{
			Wallet = wallet;

			Title = Path.GetFileNameWithoutExtension(wallet.Path);
		}

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

		public bool IsBusy
		{
			get { return _isBusy; }
			set { this.RaiseAndSetIfChanged(ref _isBusy, value); }
		}

		public Wallet Wallet { get; }

		public int CompareTo([AllowNull] WalletViewModelBase other)
		{
			return Wallet.CompareTo(other.Wallet);
		}
	}
}
