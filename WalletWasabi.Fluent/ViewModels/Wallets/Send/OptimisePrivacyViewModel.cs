using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
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
			TransactionInfo transactionInfo, BuildTransactionResult requestedTransaction)
		{
			_wallet = wallet;
			_requestedTransaction = requestedTransaction;
			_transactionInfo = transactionInfo;

			this.WhenAnyValue(x => x.SelectedPrivacySuggestion)
				.Where(x => x is { })
				.Subscribe(x => ExactAmountWarningVisible = x != _defaultSelection);

			_privacySuggestions = new ObservableCollection<PrivacySuggestionControlViewModel>();

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;

			Tuple<bool, string> s = new Tuple<bool, string>(false, "");
			var h = s.Item1;

			NextCommand = ReactiveCommand.Create(
				() => OnNext(transactionInfo),
				this.WhenAnyValue(x => x.SelectedPrivacySuggestion).Select(x => x is { }));
		}

		private void OnNext(TransactionInfo transactionInfo)
		{
			Navigate().To(new TransactionPreviewViewModel(_wallet, transactionInfo,
				SelectedPrivacySuggestion!.TransactionResult));
		}

		protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(inHistory, disposables);

			if (!inHistory)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					IsBusy = true;

					var intent = new PaymentIntent(
						_transactionInfo.Address,
						MoneyRequest.CreateAllRemaining(subtractFee: true),
						_transactionInfo.Labels);

					PrivacySuggestionControlViewModel? smallerSuggestion = null;

					if (_requestedTransaction.SpentCoins.Count() > 1)
					{
						var smallerTransaction = await Task.Run(() => _wallet.BuildTransaction(
							_wallet.Kitchen.SaltSoup(),
							intent,
							FeeStrategy.CreateFromFeeRate(_transactionInfo.FeeRate),
							allowUnconfirmed: true,
							_requestedTransaction
								.SpentCoins
								.OrderBy(x => x.Amount)
								.Skip(1)
								.Select(x => x.OutPoint)));

						smallerSuggestion = new PrivacySuggestionControlViewModel(
							_transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), smallerTransaction,
							PrivacyOptimisationLevel.Better, _wallet.Synchronizer.UsdExchangeRate, new PrivacySuggestionBenefit(true, "Improved Privacy"));
					}

					_defaultSelection = new PrivacySuggestionControlViewModel(
						_transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), _requestedTransaction,
						PrivacyOptimisationLevel.Standard, _wallet.Synchronizer.UsdExchangeRate, new PrivacySuggestionBenefit(false, "As Requested"));

					var largerTransaction = await Task.Run(() => _wallet.BuildTransaction(
						_wallet.Kitchen.SaltSoup(),
						intent,
						FeeStrategy.CreateFromFeeRate(_transactionInfo.FeeRate),
						true,
						_requestedTransaction.SpentCoins.Select(x => x.OutPoint)));

					var largerSuggestion = new PrivacySuggestionControlViewModel(
						_transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), largerTransaction,
						PrivacyOptimisationLevel.Better, _wallet.Synchronizer.UsdExchangeRate, new PrivacySuggestionBenefit(true, "Improved Privacy"));

					// There are several scenarios, both the alternate suggestions are <, or >, or 1 < and 1 >.
					// We sort them and add the suggestions accordingly.
					var suggestions = new List<PrivacySuggestionControlViewModel> { _defaultSelection, largerSuggestion };

					if (smallerSuggestion is { })
					{
						suggestions.Add(smallerSuggestion);
					}

					foreach (var suggestion in NormalizeSuggestions(suggestions, _defaultSelection))
					{
						_privacySuggestions.Add(suggestion);
					}

					SelectedPrivacySuggestion = _defaultSelection;

					IsBusy = false;
				});
			}
		}

		private IEnumerable<PrivacySuggestionControlViewModel> NormalizeSuggestions(
			IEnumerable<PrivacySuggestionControlViewModel> suggestions, PrivacySuggestionControlViewModel defaultSuggestion)
		{
			var normalized = suggestions
				.OrderBy(x => x.TransactionResult.CalculateDestinationAmount())
				.ToList();

			if (normalized.Count == 3)
			{
				var index = normalized.IndexOf(defaultSuggestion);

				switch (index)
				{
					case 1:
						break;

					case 0:
						normalized = normalized.Take(2).ToList();
						break;

					case 2:
						normalized = normalized.Skip(1).ToList();
						break;
				}
			}

			return normalized;
		}
	}
}
