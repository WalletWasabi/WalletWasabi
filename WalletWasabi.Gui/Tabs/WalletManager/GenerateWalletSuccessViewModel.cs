using NBitcoin;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		private string _mnemonicWords;

		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner, Mnemonic mnemonic) : base("Wallet Generated Successfully!")
		{
			Disposables = new CompositeDisposable();

			_mnemonicWords = mnemonic.ToString();

			ConfirmCommand = ReactiveCommand.Create(() =>
			{
				owner.SelectTestPassword();
			}).DisposeWith(Disposables);
		}

		public string MnemonicWords
		{
			get => _mnemonicWords;
			set => this.RaiseAndSetIfChanged(ref _mnemonicWords, value);
		}

		public ReactiveCommand ConfirmCommand { get; }

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();
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
