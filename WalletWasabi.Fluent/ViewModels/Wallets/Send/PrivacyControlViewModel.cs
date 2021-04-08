using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Aggregation;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Model;
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
		private PocketViewModel? _privatePocket;
		private readonly IObservableList<PocketViewModel> _selectedList;

		[AutoNotify] private decimal _stillNeeded;
		[AutoNotify] private bool _enoughSelected;

		public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			_wallet = wallet;

			_pocketSource = new SourceList<PocketViewModel>();

			_pocketSource.Connect()
				.Bind(out _pockets)
				.Subscribe();

			var selected = _pocketSource.Connect()
				.AutoRefresh()
				.Filter(x => x.IsSelected);

			_selectedList = selected.AsObservableList();

			selected.Sum(x => x.TotalBtc)
				.Subscribe(x =>
				{
					if (_privatePocket is { })
					{
						_privatePocket.IsWarningOpen = _privatePocket.IsSelected && _selectedList.Count > 1;
					}

					StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC) - x;
					EnoughSelected = StillNeeded <= 0;
				});

			_pocketSource
				.Connect()
				.WhenValueChanged(x => x.IsSelected)
				.Subscribe(_ =>
				{
					var selectedPocketLabels = Pockets.Where(x => x.IsSelected)
													  .Select(x => x.Labels)
													  .Where(label => label != CoinPocketHelper.PrivateFundsText && label != CoinPocketHelper.UnlabelledFundsText);
					transactionInfo.PocketLabels = SmartLabel.Merge(selectedPocketLabels);
				});

			StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);

			EnableBack = true;

			NextCommand = ReactiveCommand.CreateFromTask(
				async () => await OnNext(wallet, transactionInfo, broadcaster, _selectedList),
				this.WhenAnyValue(x => x.EnoughSelected));

			EnableAutoBusyOn(NextCommand);
		}

		public ReadOnlyObservableCollection<PocketViewModel> Pockets => _pockets;

		private async Task OnNext(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster, IObservableList<PocketViewModel> selectedList)
		{
			transactionInfo.Coins = selectedList.Items.SelectMany(x => x.Coins).ToArray();

			if (_privatePocket != null)
			{
				_privatePocket.IsSelected = false;
			}

			try
			{
				if (transactionInfo.PayJoinClient is { })
				{
					await BuildTransactionAsPayJoinAsync(wallet, transactionInfo, broadcaster);
				}
				else
				{
					await BuildTransactionAsNormalAsync(wallet, transactionInfo, broadcaster);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(), "Wasabi was unable to create your transaction.");
				Navigate().BackTo<SendViewModel>();
			}
		}

		private async Task BuildTransactionAsNormalAsync(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			try
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new OptimisePrivacyViewModel(wallet, transactionInfo, broadcaster, transactionResult));
			}
			catch (InsufficientBalanceException)
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo, subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Pocket, transactionResult, wallet.Synchronizer.UsdExchangeRate);
				var result = await NavigateDialog(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{
					Navigate().To(new OptimisePrivacyViewModel(wallet, transactionInfo, broadcaster, transactionResult));
				}
				else
				{
					Navigate().BackTo<SendViewModel>();
				}
			}
		}

		private async Task BuildTransactionAsPayJoinAsync(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			try
			{
				// Do not add the PayJoin client yet, it will be added before broadcasting.
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new TransactionPreviewViewModel(wallet, transactionInfo, broadcaster, transactionResult));
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building", "There are not enough funds selected to cover the transaction fee.", "Wasabi was unable to create your transaction.");
			}
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			if (!isInHistory)
			{
				var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue());

				foreach (var pocket in pockets)
				{
					if (pocket.SmartLabel.Labels.Any(x => x == CoinPocketHelper.PrivateFundsText))
					{
						_privatePocket = new PocketViewModel(pocket)
						{
							WarningMessage =
								"Warning, using both private and non-private funds in the same transaction can destroy your privacy."
						};

						_pocketSource.Add(_privatePocket);
					}
					else
					{
						_pocketSource.Add(new PocketViewModel(pocket));
					}
				}
			}

			foreach (var pocket in _pockets)
			{
				pocket.IsSelected = false;
			}

			if (_pocketSource.Count == 1)
			{
				_pocketSource.Items.First().IsSelected = true;
			}
		}
	}
}
