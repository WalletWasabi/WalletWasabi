using Avalonia;
using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Helpers;

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
		private ObservableAsPropertyHelper<string> _cluster;
		public CoinListViewModel Owner { get; }
		public Global Global { get; set; }
		public bool InCoinJoinContainer { get; }

		public ReactiveCommand<Unit, Unit> DequeueCoin { get; }
		public ReactiveCommand<Unit, Unit> CopyClustersToClipboard { get; }

		public CoinViewModel(CoinListViewModel owner, Global global, SmartCoin model)
		{
			Model = model;
			Owner = owner;
			Global = global;
			InCoinJoinContainer = owner.CoinListContainerType == CoinListContainerType.CoinJoinTabViewModel;

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

			_cluster = Model
				.WhenAnyValue(x => x.Clusters, x => x.Clusters.Labels)
				.Select(x => x.Item2.ToString())
				.ToProperty(this, x => x.Clusters, scheduler: RxApp.MainThreadScheduler)
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

			DequeueCoin = ReactiveCommand.Create(() => Owner.PressDequeue(Model), this.WhenAnyValue(x => x.CoinJoinInProgress));

			CopyClustersToClipboard = ReactiveCommand.CreateFromTask(TryCopyClustersToClipboardAsync, 
																		this.WhenAnyValue(x => x.Clusters)
																			.Select(x => !string.IsNullOrEmpty(x)));

			Observable
				.Merge(DequeueCoin.ThrownExceptions) // Don't notify about it. Dequeue failure (and success) is notified by other mechanism.
				.Merge(CopyClustersToClipboard.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logging.Logger.LogError(ex));
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

		public int Height => Model.Height;

		public string TransactionId => Model.TransactionId.ToString();

		public uint OutputIndex => Model.Index;

		public int AnonymitySet => Model.AnonymitySet;

		public string InCoinJoin => Model.CoinJoinInProgress ? "Yes" : "No";

		public string Clusters => _cluster?.Value ?? "";

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

		public async Task TryCopyClustersToClipboardAsync()
		{
			try
			{
				await Application.Current.Clipboard.SetTextAsync(Clusters);
			}
			catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

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
