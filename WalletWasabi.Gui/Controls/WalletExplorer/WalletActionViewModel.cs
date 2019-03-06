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
	public class WalletActionViewModel : WasabiDocumentTabViewModel, IDisposable
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

		#region IDisposable Support

		protected volatile bool _disposedValue = false; // To detect redundant calls

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
