using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.AddWallet.CreateWallet
{
	public class RecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		public RecoveryWordsViewModel(IScreen screen, KeyManager keyManager, Mnemonic mnemonic, Global global)
		{
			HostScreen = screen;
			MnemonicWords = new List<RecoveryWord>();

			for (int i = 0; i < mnemonic.Words.Length; i++)
			{
				MnemonicWords.Add(new RecoveryWord(i + 1, mnemonic.Words[i]));
			}

			ContinueCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new ConfirmRecoveryWordsViewModel(HostScreen, MnemonicWords, keyManager, global)));
			CancelCommand = ReactiveCommand.Create(() => HostScreen.Router.NavigateAndReset.Execute(new SettingsPageViewModel(screen)));
		}

		public ICommand GoBackCommand => HostScreen.Router.NavigateBack;
		public ICommand ContinueCommand { get; }
		public ICommand CancelCommand { get; }

		public List<RecoveryWord> MnemonicWords { get; set; }

		public string UrlPathSegment { get; }
		public IScreen HostScreen { get; }
	}
}
