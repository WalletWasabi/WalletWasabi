using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.AddWallet.CreateWallet
{
	public class ConfirmRecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		private SourceList<RecoveryWord> _confirmationWords;

		public ConfirmRecoveryWordsViewModel(IScreen screen, List<RecoveryWord> mnemonicWords, KeyManager keyManager, Global global)
		{
			HostScreen = screen;

			_confirmationWords = new SourceList<RecoveryWord>();

			FinishCommand = ReactiveCommand.Create(() =>
			{
				global.WalletManager.AddWallet(keyManager);
				screen.Router.NavigationStack.Clear();
			},
				// CanExecute
				_confirmationWords
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.WhenValueChanged(x => x.IsConfirmed)
				.Select(x => !ConfirmationWords.Any(x => !x.IsConfirmed))
			);

			_confirmationWords
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.OnItemAdded(x => x.Reset());

			SetConfirmationWords(mnemonicWords);
		}

		public string UrlPathSegment { get; } = null!;

		public IScreen HostScreen { get; }

		public IEnumerable<RecoveryWord> ConfirmationWords => _confirmationWords.Items;

		public ICommand FinishCommand { get; }

		public ICommand CancelCommand { get; }

		public ICommand GoBackCommand => HostScreen.Router.NavigateBack;

		private void SetConfirmationWords(List<RecoveryWord> mnemonicWords)
		{
			var random = new Random();
			var unsortedConfWords = new List<RecoveryWord>();

			for (int i = 0; i < 4; i++)
			{
				int index;
				while (true)
				{
					index = random.Next(0, 12);

					if (!unsortedConfWords.Any(x => x.Index == index + 1))
					{
						break;
					}
				}

				unsortedConfWords.Add(mnemonicWords[index]);
			}

			_confirmationWords.AddRange(unsortedConfWords.OrderBy(x => x.Index));
		}
	}
}