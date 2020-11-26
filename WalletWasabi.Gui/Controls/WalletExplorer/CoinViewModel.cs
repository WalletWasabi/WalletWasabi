using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Logging;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Avalonia;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinViewModel : ViewModelBase, IDisposable
	{
		private bool _isSelected;
		private SmartCoinStatus _status;
		private ObservableAsPropertyHelper<bool> _coinJoinInProgress;
		private ObservableAsPropertyHelper<bool> _confirmed;
		private ObservableAsPropertyHelper<string> _cluster;
		private ObservableAsPropertyHelper<int> _anonymitySet;

		private volatile bool _disposedValue = false;

		public CoinViewModel(Wallet wallet, CoinListViewModel owner, SmartCoin model)
		{
			Global = Locator.Current.GetService<Global>();

			Model = model;
			Wallet = wallet;
			Owner = owner;

			RefreshSmartCoinStatus();

			Disposables = new CompositeDisposable();

			_coinJoinInProgress = Model
				.WhenAnyValue(x => x.CoinJoinInProgress)
				.ToProperty(this, x => x.CoinJoinInProgress, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_confirmed = Model
				.WhenAnyValue(x => x.Confirmed)
				.ToProperty(this, x => x.Confirmed, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_anonymitySet = Model
				.WhenAnyValue(x => x.HdPubKey.AnonymitySet)
				.ToProperty(this, x => x.AnonymitySet, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_cluster = Model
				.WhenAnyValue(x => x.HdPubKey.Cluster, x => x.HdPubKey.Cluster.Labels)
				.Select(x => x.Item2.ToString())
				.ToProperty(this, x => x.Cluster, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Status)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(ToolTip)));

			Observable
				.Merge(Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend, x => x.Confirmed, x => x.CoinJoinInProgress).Select(_ => Unit.Default))
				.Merge(Observable.FromEventPattern(Wallet.ChaumianClient, nameof(Wallet.ChaumianClient.StateUpdated)).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => RefreshSmartCoinStatus())
				.DisposeWith(Disposables);

			Global.BitcoinStore.SmartHeaderChain
				.WhenAnyValue(x => x.TipHeight).Select(_ => Unit.Default)
				.Merge(Model.WhenAnyValue(x => x.Height).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1)) // DO NOT TAKE THIS THROTTLE OUT, OTHERWISE SYNCING WITH COINS IN THE WALLET WILL STACKOVERFLOW!
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(Confirmations)))
				.DisposeWith(Disposables);

			Global.UiConfig
				.WhenAnyValue(x => x.PrivacyMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(AmountBtc));
					this.RaisePropertyChanged(nameof(Cluster));
				}).DisposeWith(Disposables);

			DequeueCoin = ReactiveCommand.Create(() => Owner.PressDequeue(Model), this.WhenAnyValue(x => x.CoinJoinInProgress));

			OpenCoinInfo = ReactiveCommand.Create(() =>
			{
				var shell = IoC.Get<IShell>();

				var coinInfo = shell.Documents?.OfType<CoinInfoTabViewModel>()?.FirstOrDefault(x => x.Coin?.Model == Model);

				if (coinInfo is null)
				{
					coinInfo = new CoinInfoTabViewModel(this);
					shell.AddDocument(coinInfo);
				}

				shell.Select(coinInfo);
			});

			CopyCluster = ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(Cluster));

			Observable
				.Merge(DequeueCoin.ThrownExceptions) // Don't notify about it. Dequeue failure (and success) is notified by other mechanism.
				.Merge(OpenCoinInfo.ThrownExceptions)
				.Merge(CopyCluster.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private CompositeDisposable Disposables { get; set; }

		private Wallet Wallet { get; }
		public CoinListViewModel Owner { get; }
		private Global Global { get; }
		public bool CanBeDequeued => Owner.CanDequeueCoins;
		public ReactiveCommand<Unit, Unit> DequeueCoin { get; }
		public ReactiveCommand<Unit, Unit> OpenCoinInfo { get; }
		public ReactiveCommand<Unit, Unit> CopyCluster { get; }

		public SmartCoin Model { get; }

		public bool Confirmed => _confirmed?.Value ?? false;

		public bool CoinJoinInProgress => _coinJoinInProgress?.Value ?? false;

		public string Address => Model.ScriptPubKey.GetDestinationAddress(Global.Network).ToString();

		public int Confirmations => Model.Height.Type == HeightType.Chain
			? (int)Global.BitcoinStore.SmartHeaderChain.TipHeight - Model.Height.Value + 1
			: 0;

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
		}

		public string ToolTip => Status switch
		{
			SmartCoinStatus.Confirmed => "This coin is confirmed.",
			SmartCoinStatus.Unconfirmed => "This coin is unconfirmed.",
			SmartCoinStatus.MixingOnWaitingList => "This coin is waiting for its turn to be coinjoined.",
			SmartCoinStatus.MixingBanned => $"The coordinator banned this coin from participation until {Model?.BannedUntilUtc?.ToString("yyyy - MM - dd HH: mm", CultureInfo.InvariantCulture)}.",
			SmartCoinStatus.MixingInputRegistration => "This coin is registered for coinjoin.",
			SmartCoinStatus.MixingConnectionConfirmation => "This coin is currently in Connection Confirmation phase.",
			SmartCoinStatus.MixingOutputRegistration => "This coin is currently in Output Registration phase.",
			SmartCoinStatus.MixingSigning => "This coin is currently in Signing phase.",
			SmartCoinStatus.SpentAccordingToBackend => "According to the Backend, this coin is spent. Wallet state will be corrected after confirmation.",
			SmartCoinStatus.MixingWaitingForConfirmation => "Coinjoining unconfirmed coins is not allowed, unless the coin is a coinjoin output itself.",
			_ => "This is impossible."
		};

		public Money Amount => Model.Amount;

		public string AmountBtc => Model.Amount.ToString(false, true);

		public int Height => Model.Height;

		public string TransactionId => Model.TransactionId.ToString();

		public uint OutputIndex => Model.Index;

		public int AnonymitySet => _anonymitySet?.Value ?? 1;

		public string InCoinJoin => Model.CoinJoinInProgress ? "Yes" : "No";

		public string Cluster => _cluster?.Value ?? "";

		public string PubKey => Model.HdPubKey.PubKey.ToString();

		public string KeyPath => Model.HdPubKey.FullKeyPath.ToString();

		public SmartCoinStatus Status
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		private void RefreshSmartCoinStatus()
		{
			Status = GetSmartCoinStatus();
		}

		private SmartCoinStatus GetSmartCoinStatus()
		{
			Model.SetIsBanned(); // Recheck if the coin's ban has expired.
			if (Model.IsBanned)
			{
				return SmartCoinStatus.MixingBanned;
			}

			if (Model.CoinJoinInProgress && Wallet.ChaumianClient is { })
			{
				ClientState clientState = Wallet.ChaumianClient.State;
				foreach (var round in clientState.GetAllMixingRounds())
				{
					if (round.CoinsRegistered.Contains(Model))
					{
						if (round.State.Phase == RoundPhase.InputRegistration)
						{
							return SmartCoinStatus.MixingInputRegistration;
						}
						else if (round.State.Phase == RoundPhase.ConnectionConfirmation)
						{
							return SmartCoinStatus.MixingConnectionConfirmation;
						}
						else if (round.State.Phase == RoundPhase.OutputRegistration)
						{
							return SmartCoinStatus.MixingOutputRegistration;
						}
						else if (round.State.Phase == RoundPhase.Signing)
						{
							return SmartCoinStatus.MixingSigning;
						}
					}
				}
			}

			if (Model.SpentAccordingToBackend)
			{
				return SmartCoinStatus.SpentAccordingToBackend;
			}

			if (Model.Confirmed)
			{
				if (Model.CoinJoinInProgress)
				{
					return SmartCoinStatus.MixingOnWaitingList;
				}
				else
				{
					return SmartCoinStatus.Confirmed;
				}
			}
			else // Unconfirmed
			{
				if (Model.CoinJoinInProgress)
				{
					return SmartCoinStatus.MixingWaitingForConfirmation;
				}
				else
				{
					return SmartCoinStatus.Unconfirmed;
				}
			}
		}

		public CompositeDisposable GetDisposables() => Disposables;

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				Disposables = null;
				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
