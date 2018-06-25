using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel
	{
		private string _mnemonicWords;

		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner) : base("Wallet Generated Successfully!")
		{
			_mnemonicWords = "";

			ConfirmCommand = ReactiveCommand.Create(() =>
			{
				owner.SelectLoadWallet();
			});
		}

		public string MnemonicWords
		{
			get { return _mnemonicWords; }
			set { this.RaiseAndSetIfChanged(ref _mnemonicWords, value); }
		}

		public ReactiveCommand ConfirmCommand { get; }

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();
		}
	}
}
