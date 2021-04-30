using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
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
	[NavigationMetaData(Title = "Confirm recovery words")]
	public partial class ConfirmRecoveryWordsViewModel : RoutableViewModel
	{
		private readonly WalletManager _walletManager;
		private readonly ReadOnlyObservableCollection<RecoveryWordViewModel> _confirmationWords;
		[AutoNotify] private bool _isSkipEnable;

		public ConfirmRecoveryWordsViewModel(List<RecoveryWordViewModel> mnemonicWords, KeyManager keyManager, WalletManager walletManager)
		{
			_walletManager = walletManager;
			var confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();
			_isSkipEnable = walletManager.Network != Network.Main || System.Diagnostics.Debugger.IsAttached;

			var nextCommandCanExecute =
				confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.WhenValueChanged(x => x.IsConfirmed)
				.Select(_ => confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

			EnableBack = true;

			NextCommand = ReactiveCommand.Create(() => OnNext(keyManager, walletManager), nextCommandCanExecute);

			if (_isSkipEnable)
			{
				SkipCommand = ReactiveCommand.Create(() => NextCommand.Execute(null));
			}

			CancelCommand = ReactiveCommand.Create(OnCancel);

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

		private void OnNext(KeyManager keyManager, WalletManager walletManager)
		{
			Navigate().To(new AddedWalletPageViewModel(walletManager, keyManager));
		}

		private void OnCancel()
		{
			Navigate().Clear();
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			var enableCancel = _walletManager.HasWallet();
			SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
		}
	}
}
