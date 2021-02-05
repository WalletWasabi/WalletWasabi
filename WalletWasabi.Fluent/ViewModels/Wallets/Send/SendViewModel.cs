using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;
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
		[AutoNotify] private string _to;
		[AutoNotify] private decimal _amountBtc;
		[AutoNotify] private decimal _exchangeRate;
		[AutoNotify] private bool _isFixedAmount;
		[AutoNotify] private ObservableCollection<string> _labels;
		private string? _payJoinEndPoint;
		private bool _parsingUrl;

		public SendViewModel(WalletViewModel walletVm)
		{
			_to = "";
			_owner = walletVm;
			_labels = new ObservableCollection<string>();

			ExchangeRate = walletVm.Wallet.Synchronizer.UsdExchangeRate;

			this.WhenAnyValue(x => x.To)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(ParseToField);

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var text =  await Application.Current.Clipboard.GetTextAsync();

				_parsingUrl = true;

				if (!TryParseUrl(text))
				{
					To = text;
					// todo validation errors.
				}

				_parsingUrl = false;
			});
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
			}

			return result;
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposables)
		{
			_owner.Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.Subscribe(x => ExchangeRate = x)
				.DisposeWith(disposables);

			base.OnNavigatedTo(inStack, disposables);
		}

		public ICommand PasteCommand { get; }

		public double XAxisCurrentValue { get; set; } = 36;


		public double XAxisMinValue { get; set; } = 1;

		public double XAxisMaxValue { get; set; } = 1008;

		public List<string> XAxisLabels => new List<string>()
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

		public List<double> XAxisValues => new List<double>()
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

		public List<double> YAxisValues => new List<double>()
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