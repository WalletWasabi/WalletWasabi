using NBitcoin;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Optimise your privacy")]
	public partial class OptimisePrivacyViewModel : RoutableViewModel
	{
		private readonly TransactionInfo _transactionInfo;
		private readonly Wallet _wallet;
		private readonly BuildTransactionResult _requestedTransaction;

		[AutoNotify] private ObservableCollection<PrivacySuggestionControlViewModel> _privacySuggestions;
		[AutoNotify] private PrivacySuggestionControlViewModel? _selectedPrivacySuggestion;
		[AutoNotify] private bool _exactAmountWarningVisible;
		private PrivacySuggestionControlViewModel? _defaultSelection;

		public OptimisePrivacyViewModel(Wallet wallet,
			TransactionInfo transactionInfo, TransactionBroadcaster broadcaster, BuildTransactionResult requestedTransaction)
		{
			_wallet = wallet;
			_requestedTransaction = requestedTransaction;
			_transactionInfo = transactionInfo;

			this.WhenAnyValue(x => x.SelectedPrivacySuggestion)
				.Where(x => x is { })
				.Subscribe(x => ExactAmountWarningVisible = x != _defaultSelection);

			_privacySuggestions = new ObservableCollection<PrivacySuggestionControlViewModel>();

			NextCommand = ReactiveCommand.Create(
				() => NextExecute(wallet, transactionInfo, broadcaster),
				this.WhenAnyValue(x => x.SelectedPrivacySuggestion).Select(x => x is { }));
		}

		private void NextExecute(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			Navigate().To(new TransactionPreviewViewModel(wallet, transactionInfo, broadcaster,
				SelectedPrivacySuggestion!.TransactionResult));
		}

		protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
		{
			IsBusy = true;
			base.OnNavigatedTo(inHistory, disposables);

			if (!inHistory)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					var intent = new PaymentIntent(
						_transactionInfo.Address,
						MoneyRequest.CreateAllRemaining(subtractFee: true),
						_transactionInfo.Labels);

					if (_requestedTransaction.SpentCoins.Count() > 1)
					{
						var smallerTransaction = _wallet.BuildTransaction(
							_wallet.Kitchen.SaltSoup(),
							intent,
							FeeStrategy.CreateFromFeeRate(_transactionInfo.FeeRate),
							allowUnconfirmed: true,
							_requestedTransaction
								.SpentCoins
								.OrderBy(x => x.Amount)
								.Skip(1)
								.Select(x => x.OutPoint));

						_privacySuggestions.Add(new PrivacySuggestionControlViewModel(
							_transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), smallerTransaction,
							PrivacyOptimisationLevel.Better, "Improved Privacy"));
					}

					_defaultSelection = new PrivacySuggestionControlViewModel(
						_transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), _requestedTransaction,
						PrivacyOptimisationLevel.Standard);

					_privacySuggestions.Add(_defaultSelection);

					var largerTransaction = _wallet.BuildTransaction(
						_wallet.Kitchen.SaltSoup(),
						intent,
						FeeStrategy.CreateFromFeeRate(_transactionInfo.FeeRate),
						true,
						_requestedTransaction.SpentCoins.Select(x => x.OutPoint));

					_privacySuggestions.Add(new PrivacySuggestionControlViewModel(
						_transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), largerTransaction,
						PrivacyOptimisationLevel.Better, "Improved Privacy"));

					SelectedPrivacySuggestion = _defaultSelection;

					IsBusy = false;
				});
			}
		}
	}
}