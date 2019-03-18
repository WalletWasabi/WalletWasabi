using System;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletViewModel : WasabiDocumentTabViewModel, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		private ObservableCollection<WalletActionViewModel> _actions;

		private string _title;

		public WalletViewModel(WalletService walletService, bool receiveDominant)
			: base(Path.GetFileNameWithoutExtension(walletService.KeyManager.FilePath))
		{
			Disposables = new CompositeDisposable();

			WalletService = walletService;
			Name = Path.GetFileNameWithoutExtension(WalletService.KeyManager.FilePath);
			var coinsChanged = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged));
			var coinSpent = Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed));

			coinsChanged
				.Merge(coinSpent)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o =>
				{
					SetBalance(Name);
				}).DisposeWith(Disposables);

			SetBalance(Name);

			Actions = new ObservableCollection<WalletActionViewModel>
			{
				new SendTabViewModel(this).DisposeWith(Disposables),
				new ReceiveTabViewModel(this).DisposeWith(Disposables),
				new CoinJoinTabViewModel(this).DisposeWith(Disposables),
				new HistoryTabViewModel(this).DisposeWith(Disposables),
				new WalletInfoViewModel(this).DisposeWith(Disposables)
			};

			Actions[0].DisplayActionTab();
			if (receiveDominant)
			{
				Actions[2].DisplayActionTab();
				Actions[3].DisplayActionTab();
				Actions[1].DisplayActionTab();
			}
			else
			{
				Actions[1].DisplayActionTab();
				Actions[2].DisplayActionTab();
				Actions[3].DisplayActionTab();
			}
		}

		public string Name { get; }

		public WalletService WalletService { get; }

		public override string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public ObservableCollection<WalletActionViewModel> Actions
		{
			get => _actions;
			set => this.RaiseAndSetIfChanged(ref _actions, value);
		}

		private void SetBalance(string walletName)
		{
			Money balance = Enumerable.Where(WalletService.Coins, c => c.Unspent && !c.IsDust && !c.SpentAccordingToBackend).Sum(c => (long?)c.Amount) ?? 0;
			Title = $"{walletName} ({balance.ToString(false, true)} BTC)";
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
