using AvalonStudio.Controls;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletActionViewModel : WasabiDocumentTabViewModel
	{
		protected CompositeDisposable Disposables { get; }

		public WalletViewModel Wallet { get; }

		public WalletActionViewModel(string title, WalletViewModel walletViewModel)
			: base(title)
		{
			Disposables = new CompositeDisposable();

			Wallet = walletViewModel;
			DoItCommand = ReactiveCommand.Create(DisplayActionTab).DisposeWith(Disposables);
		}

		public ReactiveCommand DoItCommand { get; }

		public void DisplayActionTab()
		{
			IoC.Get<IShell>().AddOrSelectDocument(this);
		}
	}
}
