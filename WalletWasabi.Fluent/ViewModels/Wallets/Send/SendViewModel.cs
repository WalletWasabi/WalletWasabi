using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Windows.Input;
using Avalonia;
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
		private WalletViewModel _owner;
		[AutoNotify] private string _to;
		[AutoNotify] private decimal _amountBtc;
		[AutoNotify] private decimal _exchangeRate;

		public SendViewModel(WalletViewModel walletVm)
		{
			_owner = walletVm;

			ExchangeRate = walletVm.Wallet.Synchronizer.UsdExchangeRate;

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var text =  await Application.Current.Clipboard.GetTextAsync();

				var wallet = walletVm.Wallet;

				if (AddressStringParser.TryParse(text, wallet.Network, out BitcoinUrlBuilder? url))
				{
					SmartLabel label = url.Label;

					if (!label.IsEmpty)
					{
						//LabelSuggestion.Label = label;
					}

					if (url.Amount is { })
					{
						AmountBtc = url.Amount.ToDecimal(MoneyUnit.BTC);
					}

					if (url.UnknowParameters.TryGetValue("pj", out var endPoint))
					{
						if (!wallet.KeyManager.IsWatchOnly)
						{
							//PayjoinEndPoint = endPoint;
							return;
						}
						//NotificationHelpers.Warning("PayJoin is not allowed here.");
					}
					//PayjoinEndPoint = null;
				}
			});
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