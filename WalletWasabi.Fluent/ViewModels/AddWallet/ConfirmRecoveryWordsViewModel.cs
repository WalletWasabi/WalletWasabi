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
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConfirmRecoveryWordsViewModel : RoutableViewModel
	{
		private readonly ReadOnlyObservableCollection<RecoveryWordViewModel> _confirmationWords;
		private readonly SourceList<RecoveryWordViewModel> _confirmationWordsSourceList;

		public ConfirmRecoveryWordsViewModel(NavigationStateViewModel navigationState, List<RecoveryWordViewModel> mnemonicWords, KeyManager keyManager, WalletManager walletManager)
			: base(navigationState, NavigationTarget.Dialog)
		{
			_confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();

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
					navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear();
				},
				finishCommandCanExecute);

			CancelCommand = ReactiveCommand.Create(() => navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear());

			_confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.OnItemAdded(x => x.Reset())
				.Sort(SortExpressionComparer<RecoveryWordViewModel>.Ascending(x => x.Index))
				.Bind(out _confirmationWords)
				.Subscribe();

			SelectRandomConfirmationWords(mnemonicWords);
		}

		public ReadOnlyObservableCollection<RecoveryWordViewModel> ConfirmationWords => _confirmationWords;

		public ICommand FinishCommand { get; }

		public ICommand CancelCommand { get; }

		private void SelectRandomConfirmationWords(List<RecoveryWordViewModel> mnemonicWords)
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