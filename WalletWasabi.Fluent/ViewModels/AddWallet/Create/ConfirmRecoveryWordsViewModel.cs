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

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create
{
	[NavigationMetaData(Title = "Confirm recovery words")]
	public partial class ConfirmRecoveryWordsViewModel : RoutableViewModel
	{
		private readonly ReadOnlyObservableCollection<RecoveryWordViewModel> _confirmationWords;
		[AutoNotify] private bool _isSkipEnable;

		public ConfirmRecoveryWordsViewModel(List<RecoveryWordViewModel> mnemonicWords, KeyManager keyManager)
		{
			var confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();
			_isSkipEnable = Services.WalletManager.Network != Network.Main || System.Diagnostics.Debugger.IsAttached;

			var nextCommandCanExecute =
				confirmationWordsSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.WhenValueChanged(x => x.IsConfirmed)
				.Select(_ => confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

			EnableBack = true;

			NextCommand = ReactiveCommand.Create(() => OnNext(keyManager), nextCommandCanExecute);

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

			// Select random words to confirm.
			confirmationWordsSourceList.AddRange(mnemonicWords.OrderBy(_ => new Random().NextDouble()).Take(3));
		}

		public ReadOnlyObservableCollection<RecoveryWordViewModel> ConfirmationWords => _confirmationWords;

		private void OnNext(KeyManager keyManager)
		{
			Navigate().To(new AddedWalletPageViewModel(keyManager));
		}

		private void OnCancel()
		{
			Navigate().Clear();
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			var enableCancel = Services.WalletManager.HasWallet();
			SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: enableCancel);
		}
	}
}
