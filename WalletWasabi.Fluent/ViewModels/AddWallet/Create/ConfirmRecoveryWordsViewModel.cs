using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create
{
	public partial class ConfirmRecoveryWordsViewModel : RoutableViewModel
	{
		[AutoNotify] private bool _isSkipEnable;
		private readonly ReadOnlyObservableCollection<RecoveryWordViewModel> _confirmationWords;

		public ConfirmRecoveryWordsViewModel(List<RecoveryWordViewModel> mnemonicWords, KeyManager keyManager, WalletManager walletManager)
		{
			Title = "Confirm recovery words";
			var confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();
			_isSkipEnable = walletManager.Network != Network.Main;

			var nextCommandCanExecute =
				confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.WhenValueChanged(x => x.IsConfirmed)
				.Select(_ => confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

			NextCommand = ReactiveCommand.Create(
				() =>
				{
					Navigate().To(new AddedWalletPageViewModel(walletManager, keyManager, WalletType.Normal));
				},
				nextCommandCanExecute);

			if (_isSkipEnable)
			{
				SkipCommand = ReactiveCommand.Create(() => NextCommand.Execute(null));
			}

			CancelCommand = ReactiveCommand.Create(() => Navigate().Clear());

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
