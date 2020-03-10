using ReactiveUI;
using System;
using WalletWasabi.Gui.ViewModels;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModelBase : ViewModelBase, IComparable<WalletViewModelBase>
	{
		private bool _isExpanded;
		private string _title;
		private bool _isBusy;

		public WalletViewModelBase(string title)
		{
			Title = title;
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

		public int CompareTo([AllowNull] WalletViewModelBase other)
		{
			return Title.CompareTo(other.Title);
		}
	}
}
