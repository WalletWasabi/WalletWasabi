using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Aggregation;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Privacy Control")]
	public partial class PrivacyControlViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly SourceList<PocketViewModel> _pocketSource;
		private readonly ReadOnlyObservableCollection<PocketViewModel> _pockets;

		[AutoNotify] private decimal _stillNeeded;
		[AutoNotify] private bool _enoughSelected;
		[AutoNotify] private bool _isWarningOpen;

		private bool _buildingTransaction;

		public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo)
		{
			_wallet = wallet;

			_pocketSource = new SourceList<PocketViewModel>();

			_pocketSource.Connect()
				.Bind(out _pockets)
				.Subscribe();

			var selected = _pocketSource.Connect()
				.AutoRefresh()
				.Filter(x => x.IsSelected);

			var selectedList = selected.AsObservableList();

			selected.Sum(x => x.TotalBtc)
				.Subscribe(x =>
				{
					IsWarningOpen = selectedList.Count > 1 && selectedList.Items.Any(x => x.Labels == CoinPocketHelper.PrivateFundsText);

					StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC) - x;
					EnoughSelected = StillNeeded <= 0;
				});

			StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;

			NextCommand = ReactiveCommand.CreateFromTask(
				async () => await OnNextAsync(transactionInfo, selectedList),
				this.WhenAnyValue(x => x.EnoughSelected));

			EnableAutoBusyOn(NextCommand);
		}

		public ReadOnlyObservableCollection<PocketViewModel> Pockets => _pockets;

		private async Task OnNextAsync(TransactionInfo transactionInfo,
			IObservableList<PocketViewModel> selectedList)
		{
			transactionInfo.Coins = selectedList.Items.SelectMany(x => x.Coins).ToArray();

			try
			{
				_buildingTransaction = true;

				if (transactionInfo.PayJoinClient is { })
				{
					await BuildTransactionAsPayJoinAsync(transactionInfo);
				}
				else
				{
					await BuildTransactionAsNormalAsync(transactionInfo);
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

		private async Task BuildTransactionAsNormalAsync(TransactionInfo transactionInfo)
		{
			try
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, transactionResult));
			}
			catch (InsufficientBalanceException)
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo, subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Pocket, transactionResult, _wallet.Synchronizer.UsdExchangeRate);
				var result = await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{
					Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, transactionResult));
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
				Navigate().To(new TransactionPreviewViewModel(_wallet, transactionInfo, transactionResult));
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building", "There are not enough funds selected to cover the transaction fee.", "Wasabi was unable to create your transaction.");
			}
		}

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

				if (isInHistory)
				{
					Navigate().Back();
				}
				else
				{
					if (NextCommand is {} cmd && cmd.CanExecute(default))
					{
						cmd.Execute(default);
					}
				}
			}
		}
	}
}
