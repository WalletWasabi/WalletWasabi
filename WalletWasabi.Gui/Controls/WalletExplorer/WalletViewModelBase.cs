using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModelBase : ViewModelBase, IComparable<WalletViewModelBase>
	{
		private string _title;

		public string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public int CompareTo([AllowNull] WalletViewModelBase other)
		{
			if(other is null)
			{
				return -1;
			}

			return Title.CompareTo(other.Title);
		}
	}
}
