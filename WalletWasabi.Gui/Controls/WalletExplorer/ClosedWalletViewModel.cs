using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reactive;
using System.Text;
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

	class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(string walletFile)
		{
			WalletFile = walletFile;

			Title = Path.GetFileNameWithoutExtension(walletFile);
		}

		public string WalletFile { get; }
	}
}
