using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConfirmRecoveryWordsViewModel : ViewModelBase, IRoutableViewModel
	{
		private readonly ReadOnlyObservableCollection<RecoveryWord> _confirmationWords;
		private readonly SourceList<RecoveryWord> _confirmationWordsSourceList;

		public ConfirmRecoveryWordsViewModel(IScreen screen, List<RecoveryWord> mnemonicWords, KeyManager keyManager, WalletManager walletManager)
		{
			HostScreen = screen;

			_confirmationWordsSourceList = new SourceList<RecoveryWord>();

			var finishCommandCanExecute =
				_confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.WhenValueChanged(x => x.IsConfirmed)
				.Select(x => !_confirmationWordsSourceList.Items.Any(x => !x.IsConfirmed));

			FinishCommand = ReactiveCommand.Create(
				() =>
				{
					walletManager.AddWallet(keyManager);
					screen.Router.NavigationStack.Clear();
				},
				finishCommandCanExecute);

			_confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.OnItemAdded(x => x.Reset())
				.Sort(SortExpressionComparer<RecoveryWord>.Ascending(x => x.Index))
				.Bind(out _confirmationWords)
				.Subscribe();

			SelectRandomConfirmationWords(mnemonicWords);
		}

		public string UrlPathSegment { get; } = "";
		public IScreen HostScreen { get; }

		public ReadOnlyObservableCollection<RecoveryWord> ConfirmationWords => _confirmationWords;

		public ICommand FinishCommand { get; }

		private void SelectRandomConfirmationWords(List<RecoveryWord> mnemonicWords)
		{
			var random = new Random();

			while (_confirmationWordsSourceList.Count != 4)
			{
				var word = mnemonicWords[random.Next(0, 12)];

				if (!_confirmationWordsSourceList.Items.Contains(word))
				{
					_confirmationWordsSourceList.Add(word);
				}
			}
		}
	}
}