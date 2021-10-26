using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class PrivacyControlTileViewModel : TileViewModel
	{
		private readonly IObservable<Unit> _balanceChanged;
		private readonly Wallet _wallet;
		[AutoNotify] private bool _isAutoCoinJoinEnabled;
		[AutoNotify] private bool _isBoosting;
		[AutoNotify] private bool _showBoostingAnimation;
		[AutoNotify] private bool _boostButtonVisible;
		[AutoNotify] private IList<(string color, double percentShare)>? _testDataPoints;
		[AutoNotify] private IList<DataLegend>? _testDataPointsLegend;
		[AutoNotify] private string _percentText;
		[AutoNotify] private double _percent;
		private readonly DispatcherTimer _animationTimer;

		public PrivacyControlTileViewModel(WalletViewModel walletVm, IObservable<Unit> balanceChanged)
		{
			_wallet = walletVm.Wallet;
			_balanceChanged = balanceChanged;

			_animationTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(30)
			};

			_animationTimer.Tick += (sender, args) =>
			{
				ShowBoostingAnimation = !ShowBoostingAnimation;

				_animationTimer.Interval = ShowBoostingAnimation ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
			};

			walletVm.Settings.WhenAnyValue(x => x.AutoCoinJoin).Subscribe(x => IsAutoCoinJoinEnabled = x);

			this.WhenAnyValue(x => x.IsAutoCoinJoinEnabled, x => x.IsBoosting)
				.Subscribe(x =>
				{
					var (autoCjEnabled, isBoosting) = x;

					BoostButtonVisible = !autoCjEnabled && !isBoosting;

					if (autoCjEnabled && isBoosting)
					{
						IsBoosting = false;
					}
				});

			BoostPrivacyCommand = ReactiveCommand.Create(() =>
			{
				var isBoosting = IsBoosting = _wallet.AllowManualCoinJoin = !IsBoosting;

				ShowBoostingAnimation = isBoosting;

				_animationTimer.Interval = TimeSpan.FromSeconds(5);
				_animationTimer.IsEnabled = isBoosting;
			});
		}

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_balanceChanged
				.Subscribe(_ => Update())
				.DisposeWith(disposables);

			if (Services.HostedServices.GetOrDefault<CoinJoinManager>() is { } coinJoinManager)
			{
				Observable
					.FromEventPattern<WalletStatusChangedEventArgs>(coinJoinManager, nameof(CoinJoinManager.WalletStatusChanged))
					.Select(args => args.EventArgs)
					.Where(e => e.Wallet == _wallet)
					.Subscribe(x => { })
					.DisposeWith(disposables);
			}
		}

		private void Update()
		{
			var privateThreshold = _wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();

			var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
			var normalAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet < privateThreshold).TotalAmount();

			var privateDecimalAmount = privateAmount.ToDecimal(MoneyUnit.BTC);
			var normalDecimalAmount = normalAmount.ToDecimal(MoneyUnit.BTC);
			var totalDecimalAmount = privateDecimalAmount + normalDecimalAmount;

			var pcPrivate = totalDecimalAmount == 0M ? 0d : (double)(privateDecimalAmount / totalDecimalAmount);
			var pcNormal = 1 - pcPrivate;

			PercentText = $"{pcPrivate:P}";

			Percent = pcPrivate * 100;

			TestDataPoints = new List<(string, double)>
			{
				("#78A827", pcPrivate),
				("#D8DED7", pcNormal)
			};

			TestDataPointsLegend = new List<DataLegend>
			{
				new(privateAmount, "Private", "#78A827", pcPrivate),
				new(normalAmount, "Not Private", "#D8DED7", pcNormal)
			};
		}

		public ICommand BoostPrivacyCommand { get; }
	}
}
