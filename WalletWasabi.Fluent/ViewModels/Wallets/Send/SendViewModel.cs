using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.MathNet;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class SendViewModel : NavBarItemViewModel
	{
		private readonly WalletViewModel _owner;
		private readonly TransactionInfo _transactionInfo;
		[AutoNotify] private string _to;
		[AutoNotify] private decimal _amountBtc;
		[AutoNotify] private decimal _exchangeRate;
		[AutoNotify] private bool _isFixedAmount;
		[AutoNotify] private ObservableCollection<string> _priorLabels;
		[AutoNotify] private ObservableCollection<string> _labels;
		[AutoNotify] private bool _isPayJoin;
		[AutoNotify] private double[] _xAxisValues;
		[AutoNotify] private double[] _yAxisValues;
		[AutoNotify] private string[] _xAxisLabels;

		private string? _payJoinEndPoint;
		private bool _parsingUrl;

		public SendViewModel(WalletViewModel walletVm)
		{
			_to = "";
			_owner = walletVm;
			_transactionInfo = new TransactionInfo();
			_labels = new ObservableCollection<string>();

			ExchangeRate = walletVm.Wallet.Synchronizer.UsdExchangeRate;
			PriorLabels = new();

			this.ValidateProperty(x => x.To, ValidateToField);
			this.ValidateProperty(x => x.AmountBtc, ValidateAmount);

			this.WhenAnyValue(x => x.To)
				.Subscribe(ParseToField);

			this.WhenAnyValue(x => x.AmountBtc)
				.Subscribe(x => _transactionInfo.Amount = new Money(x, MoneyUnit.BTC));

			Labels.ToObservableChangeSet().Subscribe(x =>
			{
				_transactionInfo.Labels = new SmartLabel(_labels.ToArray());
			});

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var text = await Application.Current.Clipboard.GetTextAsync();

				_parsingUrl = true;

				if (!TryParseUrl(text))
				{
					To = text;
					// todo validation errors.
				}

				_parsingUrl = false;
			});

			NextCommand = ReactiveCommand.Create(() =>
			{
				var transactionInfo = _transactionInfo;
				var wallet = _owner.Wallet;
				var targetAnonset = wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
				var mixedCoins = wallet.Coins.Where(x => x.HdPubKey.AnonymitySet >= targetAnonset);

				var intent = new PaymentIntent(
					destination: transactionInfo.Address,
					amount: transactionInfo.Amount,
					subtractFee: false,
					label: transactionInfo.Labels);

				try
				{
					var txRes = wallet.BuildTransaction(
						wallet.Kitchen.SaltSoup(),
						intent,
						FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
						allowUnconfirmed: true,
						mixedCoins.Select(x => x.OutPoint));

					// Private coins are enough.
					Navigate().To(new OptimisePrivacyViewModel(txRes, mixedCoins, wallet, transactionInfo));
				}
				catch (NotEnoughFundsException)
				{
					// Not enough private coins.
					Navigate().To(new PrivacyControlViewModel());
				}
			});
		}

		private void ValidateAmount(IValidationErrors errors)
		{
			if (AmountBtc > Constants.MaximumNumberOfBitcoins)
			{
				errors.Add(ErrorSeverity.Error, "Amount must be less than the total supply of BTC.");
			}
			else if (AmountBtc > _owner.Wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC))
			{
				errors.Add(ErrorSeverity.Error, "Insufficient funds to cover the amount requested.");
			}
			else if (AmountBtc <= 0)
			{
				errors.Add(ErrorSeverity.Error, "Amount must be more than 0 BTC");
			}
		}

		private void ValidateToField(IValidationErrors errors)
		{
			if (!string.IsNullOrWhiteSpace(To) &&
				!AddressStringParser.TryParse(To, _owner.Wallet.Network, out BitcoinUrlBuilder? url))
			{
				errors.Add(ErrorSeverity.Error, "Input a valid BTC address or URL.");
			}
		}

		private void ParseToField(string s)
		{
			if (_parsingUrl)
			{
				return;
			}

			_parsingUrl = true;

			Dispatcher.UIThread.Post(() =>
			{
				TryParseUrl(s);

				_parsingUrl = false;
			});
		}

		private bool TryParseUrl(string text)
		{
			bool result = false;

			var wallet = _owner.Wallet;

			if (AddressStringParser.TryParse(text, wallet.Network, out BitcoinUrlBuilder? url))
			{
				result = true;
				SmartLabel label = url.Label;

				if (!label.IsEmpty)
				{
					Labels.Clear();

					foreach (var labelString in label.Labels)
					{
						Labels.Add(labelString);
					}
				}

				if (url.Address is { })
				{
					_transactionInfo.Address = url.Address;
					To = url.Address.ToString();
				}

				if (url.Amount is { })
				{
					AmountBtc = url.Amount.ToDecimal(MoneyUnit.BTC);
					IsFixedAmount = true;
				}
				else
				{
					IsFixedAmount = false;
				}

				if (url.UnknowParameters.TryGetValue("pj", out var endPoint))
				{
					if (!wallet.KeyManager.IsHardwareWallet)
					{
						_payJoinEndPoint = endPoint;
					}
					else
					{
						// Validation error... "Payjoin not available! for hw wallets."
					}
				}
				else
				{
					_payJoinEndPoint = null;
				}
			}
			else
			{
				IsFixedAmount = false;
				_payJoinEndPoint = null;
			}

			IsPayJoin = _payJoinEndPoint is { };

			return result;
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposables)
		{
			_owner.Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => ExchangeRate = x)
				.DisposeWith(disposables);

			_owner.Wallet.TransactionProcessor.WhenAnyValue(x => x.Coins)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					PriorLabels.Clear();
					PriorLabels.AddRange(x.SelectMany(coin => coin.HdPubKey.Label.Labels).Distinct());
				})
				.DisposeWith(disposables);

			_owner.Wallet.Synchronizer.WhenAnyValue(x => x.AllFeeEstimate)
				.Where(x => x is { })
				.Select(x => x!.Estimations)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(UpdateFeeEstimates)
				.DisposeWith(disposables);

			if (_owner.Wallet.Synchronizer.AllFeeEstimate is { })
			{
				UpdateFeeEstimates(_owner.Wallet.Synchronizer.AllFeeEstimate.Estimations);
			}

			base.OnNavigatedTo(inStack, disposables);
		}

		private void UpdateFeeEstimates(Dictionary<int, int> feeEstimates)
		{
			if (_owner.Wallet.Network != Network.TestNet)
			{
				XAxisValues = feeEstimates.Select(x => (double)x.Key).Reverse().ToArray();

				XAxisLabels = feeEstimates.Select(x => x.Key)
					.Select(x => FeeTargetTimeConverter.Convert(x, "m", "h", "h", "d", "d"))
					.Reverse()
					.ToArray();

				YAxisValues = feeEstimates.Select(x => (double)x.Value).Reverse().ToArray();
			}
			else
			{
				XAxisLabels = new string[]
				{
					"1w",
					"3d",
					"1d",
					"12h",
					"6h",
					"3h",
					"1h",
					"30m",
					"20m",
					"fastest"
				};

				XAxisValues = new double[]
				{
					1008,
					432,
					144,
					72,
					36,
					18,
					6,
					3,
					2,
					1,
				};

				YAxisValues = new double[]
				{
					4,
					4,
					7,
					22,
					57,
					97,
					102,
					123,
					123,
					185
				};
			}
		}

		private double GetYAxisValueFromXAxisCurrentValue()
		{
			var x = _xAxisValues.Reverse().ToArray();
			var y = _yAxisValues;
			double t = XAxisCurrentValue;
			var spline = CubicSpline.InterpolateNaturalSorted(x, y);
			return spline.Interpolate(t);
		}

		public ICommand PasteCommand { get; }

		public double XAxisCurrentValue { get; set; } = 36;

		public double XAxisMinValue { get; set; } = 1;

		public double XAxisMaxValue { get; set; } = 1008;
	}
}
