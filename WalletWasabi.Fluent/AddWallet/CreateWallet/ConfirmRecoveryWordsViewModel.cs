using Avalonia.Input;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.AddWallet.CreateWallet
{
	public class ConfirmRecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		private bool _isConfirmationFinished;

		public ConfirmRecoveryWordsViewModel(IScreen screen, List<RecoveryWord> mnemonicWords, KeyManager keyManager, Global global)
		{
			HostScreen = screen;

			ConfirmationWords = new ObservableCollection<RecoveryWord>();

			FinishCommand = ReactiveCommand.Create(() =>
			{
				global.WalletManager.AddWallet(keyManager);
				screen.Router.NavigationStack.Clear();
			});

			CancelCommand = ReactiveCommand.Create(() => HostScreen.Router.NavigateAndReset.Execute(new SettingsPageViewModel(screen)));

			SetConfirmationWords(mnemonicWords);

#if DEBUG
			IsConfirmationFinished = true;
#endif
		}

		public bool IsConfirmationFinished
		{
			get => _isConfirmationFinished;
			set => this.RaiseAndSetIfChanged(ref _isConfirmationFinished, value);
		}

		public string UrlPathSegment { get; } = null!;

		public IScreen HostScreen { get; }

		public ObservableCollection<RecoveryWord> ConfirmationWords { get; }

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

				mnemonicWords[index].Reset();
				unsortedConfWords.Add(mnemonicWords[index]);
			}

			foreach (RecoveryWord item in unsortedConfWords.OrderBy(x => x.Index))
			{
				ConfirmationWords.Add(item);
			}
		}
	}
}