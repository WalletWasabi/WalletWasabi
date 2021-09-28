using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Aggregation;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Privacy Control")]
	public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;
		private readonly SourceList<PocketViewModel> _pocketSource;
		private readonly ReadOnlyObservableCollection<PocketViewModel> _pockets;
		private readonly IObservableList<PocketViewModel> _selectedPockets;

		[AutoNotify] private decimal _stillNeeded;
		[AutoNotify] private bool _enoughSelected;
		[AutoNotify] private bool _isWarningOpen;

		private bool _buildingTransaction;

		public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo)
		{
			_transactionInfo = transactionInfo;
			_wallet = wallet;

			_pocketSource = new SourceList<PocketViewModel>();

			_pocketSource.Connect()
				.Bind(out _pockets)
				.Subscribe();

			var selected = _pocketSource.Connect()
				.AutoRefresh()
				.Filter(x => x.IsSelected);

			_selectedPockets = selected.AsObservableList();

			selected.Sum(x => x.TotalBtc)
				.Subscribe(x =>
				{
					IsWarningOpen = _selectedPockets.Count > 1 && _selectedPockets.Items.Any(x => x.Labels == CoinPocketHelper.PrivateFundsText);

					StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC) - x;
					EnoughSelected = StillNeeded <= 0;
				});

			StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;

			NextCommand = ReactiveCommand.Create(Complete,
				this.WhenAnyValue(x => x.EnoughSelected));

			EnableAutoBusyOn(NextCommand);
		}

		private void Complete()
		{
			Close(DialogResultKind.Normal, _selectedPockets.Items.SelectMany(x => x.Coins).ToArray());
		}

		public ReadOnlyObservableCollection<PocketViewModel> Pockets => _pockets;

		private async Task OnNextAsync(TransactionInfo transactionInfo,
			IObservableList<PocketViewModel> selectedList)
		{
			Complete();

			return;

			try
			{
				_buildingTransaction = true;

				if (transactionInfo.PayJoinClient is { })
				{
					//await BuildTransactionAsPayJoinAsync(transactionInfo);
				}
				else
				{
					//await BuildTransactionAsNormalAsync(transactionInfo);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(), "Wasabi was unable to create your transaction.");
				Navigate().BackTo<SendViewModel>();
			}
			finally
			{
				_buildingTransaction = false;
			}
		}

		/*private async Task BuildTransactionAsNormalAsync(TransactionInfo transactionInfo)
		{
			try
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, transactionResult), _silentNavigation ? NavigationMode.Skip : NavigationMode.Normal);
			}
			catch (InsufficientBalanceException)
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo, subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Pocket, transactionResult, _wallet.Synchronizer.UsdExchangeRate);
				var result = await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{
					Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, transactionResult), _silentNavigation ? NavigationMode.Skip : NavigationMode.Normal);
				}
				else
				{
					Navigate().BackTo<SendViewModel>();
				}
			}
		}

		private async Task BuildTransactionAsPayJoinAsync(TransactionInfo transactionInfo)
		{
			try
			{
				// Do not add the PayJoin client yet, it will be added before broadcasting.
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new TransactionPreviewViewModel(_wallet, transactionInfo, transactionResult), _silentNavigation ? NavigationMode.Skip : NavigationMode.Normal);
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building", "There are not enough funds selected to cover the transaction fee.", "Wasabi was unable to create your transaction.");

				Navigate().BackTo<SendViewModel>();
			}
		}*/

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			if (_buildingTransaction)
			{
				return;
			}

			base.OnNavigatedTo(isInHistory, disposables);

			if (!isInHistory)
			{
				var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue()).Select(x => new PocketViewModel(x));

				_pocketSource.AddRange(pockets);
			}

			foreach (var pocket in _pockets)
			{
				pocket.IsSelected = false;
			}

			if (_pocketSource.Count == 1)
			{
				_pocketSource.Items.First().IsSelected = true;

				Complete();
			}
		}
	}
}
