using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinViewModel : ViewModelBase, IDisposable
	{
		private CompositeDisposable Disposables { get; set; }

		private bool _isSelected;
		private SmartCoinStatus _status;
		private ObservableAsPropertyHelper<bool> _coinJoinInProgress;
		private ObservableAsPropertyHelper<bool> _unspent;
		private ObservableAsPropertyHelper<bool> _confirmed;
		private ObservableAsPropertyHelper<bool> _unavailable;
		public Global Global { get; set; }
		private CoinListViewModel ParentViewModel { get; }

		public ReactiveCommand<Unit, Unit> EnqueueCoin { get; }
		public ReactiveCommand<Unit, Unit> DequeueCoin { get; }

		public CoinViewModel(Global global, SmartCoin model, CoinListViewModel coinListViewModel)
		{
			Model = model;
			Global = global;
			ParentViewModel = coinListViewModel;

			RefreshSmartCoinStatus();

			Disposables = new CompositeDisposable();

			_coinJoinInProgress = Model
				.WhenAnyValue(x => x.CoinJoinInProgress)
				.ToProperty(this, x => x.CoinJoinInProgress, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_unspent = Model
				.WhenAnyValue(x => x.Unspent)
				.ToProperty(this, x => x.Unspent, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_confirmed = Model
				.WhenAnyValue(x => x.Confirmed)
				.ToProperty(this, x => x.Confirmed, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_unavailable = Model
				.WhenAnyValue(x => x.Unavailable)
				.ToProperty(this, x => x.Unavailable, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Status)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(ToolTip)));

			Observable
				.Merge(Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend, x => x.Confirmed, x => x.CoinJoinInProgress).Select(_ => Unit.Default))
				.Merge(Observable.FromEventPattern(Global.ChaumianClient, nameof(Global.ChaumianClient.StateUpdated)).Select(_ => Unit.Default))
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
				.WhenAnyValue(x => x.LurkingWifeMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(AmountBtc));
					this.RaisePropertyChanged(nameof(Clusters));
				}).DisposeWith(Disposables);

			DequeueCoin = ReactiveCommand.Create(() => ParentViewModel.InvokeDequeueCoinsPressed(this), this.WhenAnyValue(x => x.CoinJoinInProgress));

			Observable
				.Merge(DequeueCoin.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					NotificationHelpers.Error(ex.ToTypeMessageString());
					Logger.LogWarning(ex);
				});
		}

		public SmartCoin Model { get; }

		public bool Confirmed => _confirmed?.Value ?? false;

		public bool CoinJoinInProgress => _coinJoinInProgress?.Value ?? false;

		public bool Unavailable => _unavailable?.Value ?? false;

		public bool Unspent => _unspent?.Value ?? false;

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

		public string Label => Model.Label;

		public int Height => Model.Height;

		public string TransactionId => Model.TransactionId.ToString();

		public uint OutputIndex => Model.Index;

		public int AnonymitySet => Model.AnonymitySet;

		public string InCoinJoin => Model.CoinJoinInProgress ? "Yes" : "No";

		public string Clusters => Model.Clusters.Labels; // If the value is null the bind do not update the view. It shows the previous state for example: ##### even if PrivMode false.

		public string PubKey => Model.HdPubKey?.PubKey?.ToString() ?? "";

		public string KeyPath => Model.HdPubKey?.FullKeyPath?.ToString() ?? "";

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

			if (Model.CoinJoinInProgress && Global.ChaumianClient != null)
			{
				ClientState clientState = Global.ChaumianClient.State;
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

		private volatile bool _disposedValue = false;

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
