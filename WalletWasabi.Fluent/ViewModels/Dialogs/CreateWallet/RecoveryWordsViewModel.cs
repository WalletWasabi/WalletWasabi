using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.CreateWallet
{
	public class RecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		private List<string> _mnemonicWords;

		public RecoveryWordsViewModel(IScreen screen)
		{
			HostScreen = screen;
			MnemonicWords = new List<string>();

			for (int i = 0; i < 12; i++)
			{
				MnemonicWords.Add($"{i + 1}. Random");
			}

			ContinueCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new ConfirmRecoveryWordsViewModel(HostScreen, MnemonicWords)));
		}

		public ICommand GoBackCommand => HostScreen.Router.NavigateBack;
		public ICommand ContinueCommand { get; }

		public List<string> MnemonicWords
		{
			get => _mnemonicWords;
			set => this.RaiseAndSetIfChanged(ref _mnemonicWords, value);
		}

		public string UrlPathSegment { get; }
		public IScreen HostScreen { get; }
	}
}
