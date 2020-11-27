using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConfirmRecoveryWordsViewModel : RoutableViewModel
	{
		private readonly ReadOnlyObservableCollection<RecoveryWordViewModel> _confirmationWords;

		public ConfirmRecoveryWordsViewModel(NavigationStateViewModel navigationState, List<RecoveryWordViewModel> mnemonicWords, KeyManager keyManager, WalletManager walletManager)
			: base(navigationState, NavigationTarget.DialogScreen)
		{
			var confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();

			var finishCommandCanExecute =
				confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.WhenValueChanged(x => x.IsConfirmed)
				.Select(_ => confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

			NextCommand = ReactiveCommand.Create(
				() =>
				{
					walletManager.AddWallet(keyManager);

					NavigateTo(new AddedWalletPageViewModel(navigationState, keyManager.WalletName, WalletType.Normal), NavigationTarget.DialogScreen);					
				},
				finishCommandCanExecute);

			CancelCommand = ReactiveCommand.Create(() => ClearNavigation(NavigationTarget.DialogScreen));

			confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.OnItemAdded(x => x.Reset())
				.Sort(SortExpressionComparer<RecoveryWordViewModel>.Ascending(x => x.Index))
				.Bind(out _confirmationWords)
				.Subscribe();

			// Select 4 random words to confirm.
			confirmationWordsSourceList.AddRange(mnemonicWords.OrderBy(_ => new Random().NextDouble()).Take(4));
		}

		public ReadOnlyObservableCollection<RecoveryWordViewModel> ConfirmationWords => _confirmationWords;
	}
}