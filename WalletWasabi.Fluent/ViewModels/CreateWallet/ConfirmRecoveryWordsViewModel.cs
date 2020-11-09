using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.CreateWallet
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

			NextCommand = ReactiveCommand.Create(
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

			// Select 4 random words to confirm.
			_confirmationWordsSourceList.AddRange(mnemonicWords.OrderBy(x => new Random().NextDouble()).Take(4));
		}

		public ReadOnlyObservableCollection<RecoveryWordViewModel> ConfirmationWords => _confirmationWords;

		public ICommand NextCommand { get; }

		public ICommand CancelCommand { get; }
	}
}